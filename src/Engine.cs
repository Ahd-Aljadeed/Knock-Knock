using System;
using System.Collections.Generic;

namespace KnockKnock
{
    public class ActivityEntry
    {
        public string Time;
        public string Text;
        public string Kind; // ok | reject | info | error
    }

    // Turns microphone audio into actions. Process() runs on the audio thread;
    // events are raised there too, so subscribers must marshal to the UI thread.
    public class Engine
    {
        public const int CalibrationKnocks = 6;
        static readonly float[] Ratios = { 40f, 15f, 7f };
        static readonly float[] MinPeaks = { 0.05f, 0.02f, 0.008f };
        static readonly double[] Tolerances = { 0.75, 1.0, 1.4 };
        const int CooldownFrames = 150; // 1.5 s between actions

        public event Action<Binding, int> Fire;
        public event Action<ActivityEntry> Activity;
        public event Action<Knock, int> CalibrationKnock;
        public event Action<KnockProfile> CalibrationDone;

        readonly KnockDetector det = new KnockDetector();
        readonly Sequencer seq = new Sequencer();
        readonly List<ActivityEntry> log = new List<ActivityEntry>();
        volatile Settings settings;
        volatile bool calibrating;
        List<Knock> calib = new List<Knock>();
        long lastFire = -1000;

        public Engine(Settings s) { Apply(s); }

        public float LevelDb { get { return det.LevelDb; } }
        public bool Calibrating { get { return calibrating; } }

        // Swap in new settings; the audio thread picks them up on the next frame.
        public void Apply(Settings s) { settings = s; }

        public void StartCalibration() { lock (calib) { calib = new List<Knock>(); } seq.Reset(); calibrating = true; }
        public void CancelCalibration() { calibrating = false; }

        public List<ActivityEntry> RecentActivity()
        {
            lock (log) return new List<ActivityEntry>(log);
        }

        public void Log(string text, string kind)
        {
            var e = new ActivityEntry { Time = DateTime.Now.ToString("HH:mm:ss"), Text = text, Kind = kind };
            lock (log) { log.Add(e); if (log.Count > 60) log.RemoveAt(0); }
            var h = Activity; if (h != null) h(e);
        }

        public void Process(float[] buf, int n)
        {
            var s = settings;
            det.Ratio = Ratios[s.Sensitivity];
            det.MinPeak = MinPeaks[s.Sensitivity];
            seq.MaxTaps = s.MaxKnocks();

            Knock k = det.Process(buf, n);
            if (calibrating) { if (k != null) Learn(k); return; }

            List<Knock> done = null;
            if (k != null)
            {
                string why = s.Profile != null ? s.Profile.Mismatch(k, Tolerances[s.Sensitivity]) : null;
                if (why == null) done = seq.Add(k);
                else if (Native.MsSinceInput() > 1500) Log("Ignored a sound: " + why, "reject"); // don't log typing noise
            }
            else done = seq.Tick(det.Frame);

            if (done != null) Evaluate(done, s);
        }

        void Learn(Knock k)
        {
            List<Knock> list;
            lock (calib)
            {
                // Ask for separate knocks: skip anything within 150 ms of the previous one.
                if (calib.Count > 0 && k.Frame - calib[calib.Count - 1].Frame < 15) return;
                calib.Add(k);
                list = new List<Knock>(calib);
            }
            var h = CalibrationKnock; if (h != null) h(k, list.Count);
            if (list.Count >= CalibrationKnocks)
            {
                calibrating = false;
                var d = CalibrationDone; if (d != null) d(KnockProfile.Learn(list));
            }
        }

        void Evaluate(List<Knock> knocks, Settings s)
        {
            int n = knocks.Count;
            if (n < 2) return;
            long durMs = (det.Frame - knocks[0].Frame) * 10;
            uint idle = Native.MsSinceInput();
            string count = n + " knocks";

            if (s.IgnoreWhileTyping && idle < durMs + 1000) { Log(count + " ignored: you were typing or using the mouse", "reject"); return; }
            if (s.AwayMinutes > 0 && idle - durMs > s.AwayMinutes * 60000L)
            {
                Log(count + " ignored: you seemed away. Touch the mouse or keyboard once, then knock", "reject");
                return;
            }
            if (s.RhythmCheck)
            {
                string why = Sequencer.RhythmProblem(knocks);
                if (why != null) { Log(count + " ignored: " + why, "reject"); return; }
            }
            if (det.Frame - lastFire < CooldownFrames) return;
            var b = s.Find(n);
            if (b == null) { Log(count + " heard, but no action is set for " + n, "info"); return; }
            lastFire = det.Frame;
            var h = Fire; if (h != null) h(b, n);
        }
    }
}
