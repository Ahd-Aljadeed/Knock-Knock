using System;
using System.Collections.Generic;
using System.Linq;

namespace KnockKnock
{
    // One detected knock and the features used to tell knocks from other thumps.
    public class Knock
    {
        public long Frame;   // 10 ms frame index of the onset
        public float Loud;   // peak energy relative to the background noise floor
        public float Bright; // high-frequency content of the attack (energy of the first difference / energy)
        public int Decay;    // frames until it faded to 15% of its peak
    }

    // Finds short, sharp, quickly decaying sounds in 10 ms frames of 16 kHz audio.
    public class KnockDetector
    {
        public float Ratio = 15f;     // onset must be this many times louder than the noise floor
        public float MinPeak = 0.02f; // and have at least this absolute peak (0..1)
        public long Frame { get { return frame; } }
        public float LevelDb;         // current level above the noise floor, for meters

        const int MaxKnockFrames = 20; // a knock must die down within 200 ms
        const int AttackFrames = 3;    // frames used to measure the attack's tone

        float floor = -1, e1, e2, peakE, hpX, hpY, prevHp, attackE, attackD;
        long frame, onsetFrame = -1;

        public Knock Process(float[] buf, int n)
        {
            frame++;
            double sum = 0, diff = 0; float peak = 0;
            for (int i = 0; i < n; i++)
            {
                float x = buf[i];
                hpY = x - hpX + 0.995f * hpY; hpX = x; // remove DC
                float d = hpY - prevHp; prevHp = hpY;
                sum += hpY * hpY; diff += d * d;
                float a = Math.Abs(hpY); if (a > peak) peak = a;
            }
            float e = (float)(sum / n);
            if (floor < 0) floor = Math.Max(e, 1e-9f);
            LevelDb = (float)(10 * Math.Log10(Math.Max(e, 1e-12f) / floor));
            Knock found = null;

            if (onsetFrame >= 0)
            {
                // Tracking a candidate: a real knock is a short spike that decays fast.
                if (e > peakE) peakE = e;
                if (frame - onsetFrame < AttackFrames) { attackE += e; attackD += (float)(diff / n); }
                if (e < peakE * 0.15f || e < floor * 3)
                {
                    found = new Knock
                    {
                        Frame = onsetFrame, Loud = peakE / floor,
                        Bright = attackD / Math.Max(attackE, 1e-12f), Decay = (int)(frame - onsetFrame)
                    };
                    onsetFrame = -1;
                }
                else if (frame - onsetFrame > MaxKnockFrames) onsetFrame = -1; // sustained sound, not a knock
            }
            else if (e > floor * Ratio && peak > MinPeak && e2 < floor * Ratio / 3)
            {
                onsetFrame = frame; peakE = e; attackE = e; attackD = (float)(diff / n);
            }
            else
            {
                floor = e < floor ? floor * 0.95f + e * 0.05f : floor * 0.995f + e * 0.005f;
                if (floor < 1e-9f) floor = 1e-9f;
            }
            e2 = e1; e1 = e;
            return found;
        }
    }

    // What this user's knock on this desk sounds like, learned from a few samples.
    public class KnockProfile
    {
        public double BrightMean, BrightStd; // of ln(Bright)
        public double DecayMean, DecayStd;
        public double LoudMin;
        public int Samples;

        public static KnockProfile Learn(IList<Knock> knocks)
        {
            var lb = knocks.Select(k => Math.Log(Math.Max(k.Bright, 1e-6f))).ToList();
            var dc = knocks.Select(k => (double)k.Decay).ToList();
            return new KnockProfile
            {
                BrightMean = lb.Average(), BrightStd = Std(lb),
                DecayMean = dc.Average(), DecayStd = Std(dc),
                LoudMin = knocks.Min(k => k.Loud), Samples = knocks.Count
            };
        }

        static double Std(IList<double> v)
        {
            double m = v.Average();
            return Math.Sqrt(v.Sum(x => (x - m) * (x - m)) / Math.Max(1, v.Count - 1));
        }

        // How consistent the training knocks were: great | good | inconsistent.
        public string Quality()
        {
            if (BrightStd < 0.2 && DecayStd < 2) return "great";
            if (BrightStd < 0.4 && DecayStd < 4) return "good";
            return "inconsistent";
        }

        // Returns null if the knock matches, otherwise a short reason.
        // tolerance > 1 is more forgiving (High sensitivity), < 1 stricter.
        public string Mismatch(Knock k, double tolerance)
        {
            double db = Math.Abs(Math.Log(Math.Max(k.Bright, 1e-6f)) - BrightMean);
            if (db > Math.Max(2.5 * BrightStd, 0.25) * tolerance) return "doesn't sound like your knock";
            double dd = Math.Abs(k.Decay - DecayMean);
            if (dd > Math.Max(2.5 * DecayStd, 3) * tolerance) return "rings too long or too short for your knock";
            if (k.Loud < LoudMin * 0.2 / tolerance) return "much softer than your knock";
            return null;
        }
    }

    // Groups knocks into sequences: knocks 80-600 ms apart belong together.
    public class Sequencer
    {
        public const int MinGap = 8, MaxGap = 60;
        public int MaxTaps = 2; // complete immediately when this many knocks are counted
        readonly List<Knock> cur = new List<Knock>();

        // Returns a completed sequence or null.
        public List<Knock> Add(Knock k)
        {
            if (cur.Count > 0)
            {
                long gap = k.Frame - cur[cur.Count - 1].Frame;
                if (gap < MinGap) return null; // rebound of the same knock
                if (gap > MaxGap) cur.Clear();
            }
            cur.Add(k);
            return cur.Count >= MaxTaps ? Take() : null;
        }

        public List<Knock> Tick(long frame)
        {
            if (cur.Count > 0 && frame - cur[cur.Count - 1].Frame > MaxGap) return Take();
            return null;
        }

        public void Reset() { cur.Clear(); }

        List<Knock> Take() { var r = new List<Knock>(cur); cur.Clear(); return r; }

        // Real knocks are evenly spaced and similarly strong; bumps and sitting down are not.
        public static string RhythmProblem(List<Knock> seq)
        {
            if (seq.Count < 2) return null;
            float lmax = seq.Max(k => k.Loud), lmin = seq.Min(k => k.Loud);
            if (lmax / lmin > 12) return "knocks were very uneven in strength";
            if (seq.Count >= 3)
            {
                var gaps = new List<long>();
                for (int i = 1; i < seq.Count; i++) gaps.Add(seq[i].Frame - seq[i - 1].Frame);
                if ((double)gaps.Max() / gaps.Min() > 2.0) return "knocks were not evenly spaced";
            }
            return null;
        }
    }
}
