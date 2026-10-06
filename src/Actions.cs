using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace KnockKnock
{
    // Runs the action bound to a knock count. Must be called on the UI thread.
    static class Actions
    {
        public static string LastScreenshot;

        public static string Describe(Binding b)
        {
            switch (b.Action)
            {
                case "screenshot": return b.Arg == "all" ? "Screenshot of all screens" : "Screenshot of screen " + b.Arg;
                case "open": return "Open " + b.Arg;
                case "media": return MediaName(b.Arg);
                case "lock": return "Lock the PC";
                case "hotkey": return "Press " + b.Arg;
                case "command": return "Run " + b.Arg;
                default: return b.Action;
            }
        }

        public static void Run(Binding b, Settings s)
        {
            switch (b.Action)
            {
                case "screenshot": Screenshot(b.Arg, s); break;
                case "open":
                    if (string.IsNullOrWhiteSpace(b.Arg)) throw new Exception("No file, app or website chosen");
                    Process.Start(Environment.ExpandEnvironmentVariables(b.Arg.Trim().Trim('"')));
                    break;
                case "media": Tap(MediaKey(b.Arg), true); break;
                case "lock": Native.LockWorkStation(); break;
                case "hotkey": SendCombo(b.Arg); break;
                case "command":
                    if (string.IsNullOrWhiteSpace(b.Arg)) throw new Exception("No command entered");
                    Process.Start(new ProcessStartInfo("cmd.exe", "/c " + b.Arg) { CreateNoWindow = true, UseShellExecute = false });
                    break;
                default: throw new Exception("Unknown action: " + b.Action);
            }
        }

        // ---- Screenshots ----

        // Screens are numbered left to right. A missing screen number falls back to all screens.
        public static Rectangle ScreenArea(string arg)
        {
            var screens = SortedScreens();
            int n;
            if (screens.Length > 1 && int.TryParse(arg, out n) && n >= 1 && n <= screens.Length) return screens[n - 1].Bounds;
            return SystemInformation.VirtualScreen;
        }

        public static Screen[] SortedScreens()
        {
            var screens = Screen.AllScreens;
            Array.Sort(screens, (a, b) => a.Bounds.Left != b.Bounds.Left ? a.Bounds.Left.CompareTo(b.Bounds.Left) : a.Bounds.Top.CompareTo(b.Bounds.Top));
            return screens;
        }

        static void Screenshot(string arg, Settings s)
        {
            Rectangle r = ScreenArea(arg);
            using (var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppRgb))
            {
                using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(r.Left, r.Top, 0, 0, r.Size);
                Directory.CreateDirectory(s.ScreenshotFolder);
                DateTime capturedAt = DateTime.Now;
                string pattern = string.IsNullOrWhiteSpace(s.ScreenshotName) ? "Knock_{date}_{time}" : s.ScreenshotName;
                string screen = string.IsNullOrEmpty(arg) ? "all" : arg;
                string fileName = pattern
                    .Replace("{date}", capturedAt.ToString("yyyy-MM-dd"))
                    .Replace("{time}", capturedAt.ToString("HH-mm-ss"))
                    .Replace("{screen}", screen);
                foreach (char invalid in Path.GetInvalidFileNameChars())
                    fileName = fileName.Replace(invalid, '_');
                for (int i = 0; i < fileName.Length; i++)
                    if (char.IsControl(fileName[i])) fileName = fileName.Replace(fileName[i], '_');
                fileName = fileName.Trim();
                if (string.IsNullOrWhiteSpace(fileName)) fileName = "Knock_" + capturedAt.ToString("yyyy-MM-dd_HH-mm-ss");

                string path = Path.Combine(s.ScreenshotFolder, fileName + ".png");
                for (int i = 2; File.Exists(path); i++)
                    path = Path.Combine(s.ScreenshotFolder, fileName + "_" + i + ".png");
                bmp.Save(path, ImageFormat.Png);
                LastScreenshot = path;
                if (s.Clipboard) { try { Clipboard.SetImage(bmp); } catch { } }
            }
            if (s.Flash) Flash(r);
            if (s.Sound) System.Media.SystemSounds.Asterisk.Play();
        }

        // Brief camera-style white flash over the captured area.
        static void Flash(Rectangle area)
        {
            var f = new Form
            {
                FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, TopMost = true,
                BackColor = Color.White, Opacity = 0.45, StartPosition = FormStartPosition.Manual,
                Bounds = area
            };
            var t = new Timer { Interval = 25 };
            t.Tick += (o, e) => { f.Opacity -= 0.07; if (f.Opacity <= 0.05) { t.Dispose(); f.Close(); f.Dispose(); } };
            f.Show(); t.Start();
        }

        // ---- Keys ----

        static string MediaName(string arg)
        {
            switch (arg)
            {
                case "playpause": return "Play / pause media";
                case "next": return "Next track";
                case "prev": return "Previous track";
                case "mute": return "Mute / unmute";
                case "volup": return "Volume up";
                case "voldown": return "Volume down";
                default: return "Media key";
            }
        }

        static Keys MediaKey(string arg)
        {
            switch (arg)
            {
                case "playpause": return Keys.MediaPlayPause;
                case "next": return Keys.MediaNextTrack;
                case "prev": return Keys.MediaPreviousTrack;
                case "mute": return Keys.VolumeMute;
                case "volup": return Keys.VolumeUp;
                case "voldown": return Keys.VolumeDown;
                default: throw new Exception("Unknown media key: " + arg);
            }
        }

        static readonly HashSet<Keys> Extended = new HashSet<Keys>
        {
            Keys.Left, Keys.Right, Keys.Up, Keys.Down, Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown,
            Keys.Insert, Keys.Delete, Keys.LWin, Keys.RWin, Keys.RMenu, Keys.RControlKey, Keys.Divide, Keys.NumLock,
            Keys.PrintScreen, Keys.MediaPlayPause, Keys.MediaNextTrack, Keys.MediaPreviousTrack,
            Keys.VolumeMute, Keys.VolumeUp, Keys.VolumeDown
        };

        static void Down(Keys k, bool ext) { Native.keybd_event((byte)k, 0, ext || Extended.Contains(k) ? Native.KEYEVENTF_EXTENDEDKEY : 0, UIntPtr.Zero); }
        static void Up(Keys k, bool ext) { Native.keybd_event((byte)k, 0, (ext || Extended.Contains(k) ? Native.KEYEVENTF_EXTENDEDKEY : 0) | Native.KEYEVENTF_KEYUP, UIntPtr.Zero); }
        static void Tap(Keys k, bool ext) { Down(k, ext); Up(k, ext); }

        // Parses combos like "Ctrl+Shift+S", "Win+D", "Alt+F4", "Ctrl+Alt+Left".
        public static List<Keys> ParseCombo(string combo)
        {
            if (string.IsNullOrWhiteSpace(combo)) throw new Exception("No key combination recorded");
            var keys = new List<Keys>();
            foreach (var raw in combo.Split('+'))
            {
                string p = raw.Trim();
                Keys k;
                switch (p.ToLowerInvariant())
                {
                    case "ctrl": case "control": k = Keys.ControlKey; break;
                    case "shift": k = Keys.ShiftKey; break;
                    case "alt": k = Keys.Menu; break;
                    case "win": case "windows": case "meta": k = Keys.LWin; break;
                    default:
                        if (p.Length == 1 && char.IsDigit(p[0])) p = "D" + p;
                        if (!Enum.TryParse(p, true, out k)) throw new Exception("Unknown key: " + raw);
                        break;
                }
                keys.Add(k);
            }
            return keys;
        }

        static void SendCombo(string combo)
        {
            var keys = ParseCombo(combo);
            foreach (var k in keys) Down(k, false);
            for (int i = keys.Count - 1; i >= 0; i--) Up(keys[i], false);
        }
    }
}
