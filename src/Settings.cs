using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace KnockKnock
{
    // One rule: this many knocks runs this action.
    public class Binding
    {
        public int Knocks;
        public string Action; // screenshot | open | media | lock | hotkey | command
        public string Arg;    // screen number or "all", path/URL, media key, key combo, command line
    }

    // Everything the user can change. Saved as JSON in %APPDATA%\KnockKnock\settings.json.
    public class Settings
    {
        public int Version = 1;
        public bool Listening = true;
        public int Sensitivity = 1;        // 0 low, 1 medium, 2 high
        public bool RhythmCheck = true;
        public bool IgnoreWhileTyping = true;
        public int AwayMinutes = 3;        // 0 = off
        public bool Flash = true;
        public bool Sound = true;
        public bool Clipboard = true;
        public string ScreenshotFolder;
        public string ScreenshotName;
        public KnockProfile Profile       // null until the user teaches their knock
        public List<Binding> Bindings;

        public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KnockKnock");
        static readonly string FilePath = Path.Combine(Dir, "settings.json");

        public static Settings Defaults()
        {
            return new Settings
            {
                ScreenshotFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots"),
                ScreenshotName = "Knock_{date}_{time}",
                Bindings = new List<Binding>
                {
                    new Binding { Knocks = 2, Action = "screenshot", Arg = "1" },
                    new Binding { Knocks = 3, Action = "screenshot", Arg = "2" },
                    new Binding { Knocks = 4, Action = "screenshot", Arg = "all" },
                }
            };
        }

        public static bool Exists() { return File.Exists(FilePath); }

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(FilePath));
                    if (s != null) { s.Fix(); return s; }
                }
            }
            catch { } // unreadable settings: fall back to defaults
            return Defaults();
        }

        public void Save()
        {
            Directory.CreateDirectory(Dir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, ToJson());
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);
        }

        public string ToJson() { return new JavaScriptSerializer().Serialize(this); }

        // Repairs values from older or hand-edited files.
        public void Fix()
        {
            var d = Defaults();
            if (string.IsNullOrEmpty(ScreenshotFolder)) ScreenshotFolder = d.ScreenshotFolder;
            if (string.IsNullOrWhiteSpace(ScreenshotName)) ScreenshotName = d.ScreenshotName;
            if (Bindings == null) Bindings = d.Bindings;
            Bindings.RemoveAll(b => b == null || b.Knocks < 2 || b.Knocks > 8 || string.IsNullOrEmpty(b.Action));
            Sensitivity = Math.Max(0, Math.Min(2, Sensitivity));
            AwayMinutes = Math.Max(0, Math.Min(120, AwayMinutes));
        }

        public int MaxKnocks()
        {
            int m = 2;
            foreach (var b in Bindings) m = Math.Max(m, b.Knocks);
            return m;
        }

        public Binding Find(int knocks)
        {
            return Bindings.Find(b => b.Knocks == knocks);
        }
    }
}
