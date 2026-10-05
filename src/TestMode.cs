using System;
using System.Collections.Generic;
using System.IO;

namespace KnockKnock
{
    // Offline check used by tests/run-tests.ps1:
    //   KnockKnock.exe --test file.wav [maxKnocks] [--learn N]
    // Replays a 16-bit mono WAV through the detector, the rhythm check and (with --learn)
    // a profile learned from the first N knocks, printing one line per result.
    public static class TestMode
    {
        public static void Run(string[] args)
        {
            Native.AllocConsole();
            foreach (var line in Analyze(args)) Console.WriteLine(line);
        }

        public static List<string> Analyze(string[] args)
        {
            var lines = new List<string>();
            var bytes = File.ReadAllBytes(args[1]);
            int maxKnocks = args.Length > 2 && args[2] != "--learn" ? int.Parse(args[2]) : 2;
            int learnN = 0;
            int li = Array.IndexOf(args, "--learn");
            if (li >= 0) learnN = int.Parse(args[li + 1]);

            int rate = BitConverter.ToInt32(bytes, 24), frame = rate / 100;
            var det = new KnockDetector();
            var seq = new Sequencer { MaxTaps = maxKnocks };
            var training = new List<Knock>();
            KnockProfile profile = null;
            var buf = new float[frame];
            int fired = 0;
            long lastFire = -1000; // same 1.5 s cooldown as Engine

            for (int pos = 44; pos + frame * 2 <= bytes.Length; pos += frame * 2)
            {
                for (int i = 0; i < frame; i++) buf[i] = BitConverter.ToInt16(bytes, pos + i * 2) / 32768f;
                Knock k = det.Process(buf, frame);
                List<Knock> done = null;
                if (k != null)
                {
                    if (training.Count < learnN)
                    {
                        training.Add(k);
                        if (training.Count == learnN) { profile = KnockProfile.Learn(training); lines.Add("LEARNED " + profile.Quality()); }
                        continue;
                    }
                    string why = profile != null ? profile.Mismatch(k, 1.0) : null;
                    if (why != null) { lines.Add("IGNORED " + (k.Frame * 10) + "ms " + why); continue; }
                    done = seq.Add(k);
                }
                else done = seq.Tick(det.Frame);

                if (done == null || done.Count < 2) continue;
                string rhythm = Sequencer.RhythmProblem(done);
                if (rhythm != null) { lines.Add("REJECTED " + done.Count + " at " + (done[0].Frame * 10) + "ms: " + rhythm); continue; }
                if (det.Frame - lastFire < 150) continue;
                lastFire = det.Frame;
                fired++;
                lines.Add("FIRED " + done.Count + " at " + (done[0].Frame * 10) + "ms");
            }
            lines.Add("TOTAL " + fired);
            return lines;
        }
    }
}
