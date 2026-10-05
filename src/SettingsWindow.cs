using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace KnockKnock
{
    // The settings window: a WebView2 control showing the React UI (ui/), talking to the
    // app through JSON messages. It exists only while open, so it costs nothing when closed.
    class SettingsWindow : Form
    {
        readonly TrayApp app;
        readonly WebView2 web;
        readonly Timer levelTimer = new Timer { Interval = 66 };
        readonly JavaScriptSerializer json = new JavaScriptSerializer();
        readonly bool dark = IsDarkMode();
        bool ready;

        public SettingsWindow(TrayApp app)
        {
            this.app = app;
            Text = "Knock Knock";
            using (var bmp = AppIcon.Draw(64, true)) Icon = Icon.FromHandle(bmp.GetHicon());
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(720, 520);
            // 1060x740 when there is room; otherwise fit the screen it opens on (minus the taskbar).
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Size = new Size(Math.Min(1080, area.Width * 92 / 100), Math.Min(780, area.Height * 94 / 100));
            BackColor = dark ? Color.FromArgb(17, 17, 19) : Color.FromArgb(246, 245, 243);
            web = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = BackColor };
            Controls.Add(web);

            app.ActivityAdded += OnActivity;
            app.StateChanged += SendState;
            app.Engine.CalibrationKnock += OnCalibrationKnock;
            app.Engine.CalibrationDone += OnCalibrationDone;
            levelTimer.Tick += (s, e) => { if (ready && app.IsListening) Send(new Dictionary<string, object> { { "type", "level" }, { "db", Math.Round(app.Engine.LevelDb, 1) } }); };
            Load += async (s, e) => await Init();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int on = dark ? 1 : 0;
            Native.DwmSetWindowAttribute(Handle, 20 /*DWMWA_USE_IMMERSIVE_DARK_MODE*/, ref on, 4);
        }

        static bool IsDarkMode()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                return k != null && Convert.ToInt32(k.GetValue("AppsUseLightTheme", 1)) == 0;
        }

        async Task Init()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Program.LocalDir, "WebView2"));
                await web.EnsureCoreWebView2Async(env);
                var cw = web.CoreWebView2;
                cw.Settings.AreDevToolsEnabled = Program.Debug;
                cw.Settings.AreDefaultContextMenusEnabled = Program.Debug;
                cw.Settings.IsStatusBarEnabled = false;
                cw.Settings.IsZoomControlEnabled = false;
                cw.Settings.AreBrowserAcceleratorKeysEnabled = Program.Debug;
                cw.WebMessageReceived += OnMessage;
                cw.NewWindowRequested += (s, e) => { e.Handled = true; OpenUrl(e.Uri); };
                web.NavigateToString(Program.ReadText("ui.html"));
            }
            catch (Exception ex)
            {
                Controls.Remove(web);
                Controls.Add(new Label
                {
                    Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = dark ? Color.White : Color.Black,
                    Font = new Font("Segoe UI", 11f),
                    Text = "The settings window needs the Microsoft Edge WebView2 Runtime.\n" +
                           "It is built into Windows 11; on Windows 10 install it from\nhttps://developer.microsoft.com/microsoft-edge/webview2/\n\n" + ex.Message
                });
            }
        }

        // ---- Messages from the UI ----

        void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            Dictionary<string, object> msg;
            try { msg = json.DeserializeObject(e.TryGetWebMessageAsString()) as Dictionary<string, object>; }
            catch { return; }
            if (msg == null || !msg.ContainsKey("type")) return;
            try { HandleMessage(msg); }
            catch (Exception ex) { app.Engine.Log("Settings error: " + ex.Message, "error"); }
        }

        T Get<T>(Dictionary<string, object> msg, string key) { return json.Deserialize<T>(json.Serialize(msg[key])); }

        void HandleMessage(Dictionary<string, object> msg)
        {
            switch ((string)msg["type"])
            {
                case "ready":
                    ready = true;
                    SendState();
                    levelTimer.Start();
                    break;
                case "save":
                {
                    var s = Get<Settings>(msg, "settings");
                    s.Profile = app.Settings.Profile;     // changed only through the teach flow
                    s.Listening = app.Settings.Listening; // changed only through "listening"
                    app.UpdateSettings(s);
                    break;
                }
                case "listening": app.SetListening((bool)msg["value"]); break;
                case "startup": app.SetStartup((bool)msg["value"]); SendState(); break;
                case "calibrate.start": app.Engine.StartCalibration(); break;
                case "calibrate.cancel": app.Engine.CancelCalibration(); break;
                case "profile.save":
                    app.Settings.Profile = Get<KnockProfile>(msg, "profile");
                    app.UpdateSettings(app.Settings);
                    app.Engine.Log("Learned your knock", "info");
                    break;
                case "profile.clear":
                    app.Settings.Profile = null;
                    app.UpdateSettings(app.Settings);
                    app.Engine.Log("Forgot the learned knock", "info");
                    break;
                case "test": app.RunAction(Get<Binding>(msg, "binding"), 0, false); break;
                case "openFolder": app.OpenScreenshotFolder(); break;
                case "openUrl": OpenUrl((string)msg["url"]); break;
                case "browse": Browse((string)msg["kind"], Convert.ToInt32(msg["id"])); break;
            }
        }

        void Browse(string kind, int id)
        {
            string path = null;
            if (kind == "folder")
            {
                using (var d = new FolderBrowserDialog { SelectedPath = app.Settings.ScreenshotFolder })
                    if (d.ShowDialog(this) == DialogResult.OK) path = d.SelectedPath;
            }
            else
            {
                using (var d = new OpenFileDialog { Title = "Choose a file or app", Filter = "Apps and files|*.*" })
                    if (d.ShowDialog(this) == DialogResult.OK) path = d.FileName;
            }
            Send(new Dictionary<string, object> { { "type", "browse.result" }, { "id", id }, { "path", path } });
        }

        static void OpenUrl(string url)
        {
            if (url != null && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) Process.Start(url);
        }

        // ---- Messages to the UI ----

        void Send(object o)
        {
            if (!ready || IsDisposed || web.CoreWebView2 == null) return;
            web.CoreWebView2.PostWebMessageAsJson(json.Serialize(o));
        }

        void SendState()
        {
            var screens = new List<object>();
            var sorted = Actions.SortedScreens();
            for (int i = 0; i < sorted.Length; i++)
                screens.Add(new Dictionary<string, object> {
                    { "n", i + 1 }, { "width", sorted[i].Bounds.Width }, { "height", sorted[i].Bounds.Height }, { "primary", sorted[i].Primary } });
            Send(new Dictionary<string, object>
            {
                { "type", "state" },
                { "settings", app.Settings },
                { "startup", TrayApp.IsStartup() },
                { "listening", app.IsListening },
                { "screens", screens },
                { "version", Program.Version },
                { "activity", app.Engine.RecentActivity() },
                { "calibrationKnocks", Engine.CalibrationKnocks },
            });
        }

        void OnActivity(ActivityEntry e) { Send(new Dictionary<string, object> { { "type", "activity" }, { "entry", e } }); }

        void OnCalibrationKnock(Knock k, int count)
        {
            app.UI.Post(_ => Send(new Dictionary<string, object> { { "type", "calib.knock" }, { "count", count } }), null);
        }

        void OnCalibrationDone(KnockProfile p)
        {
            app.UI.Post(_ => Send(new Dictionary<string, object> { { "type", "calib.done" }, { "profile", p }, { "quality", p.Quality() } }), null);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            app.Engine.CancelCalibration();
            app.ActivityAdded -= OnActivity;
            app.StateChanged -= SendState;
            app.Engine.CalibrationKnock -= OnCalibrationKnock;
            app.Engine.CalibrationDone -= OnCalibrationDone;
            levelTimer.Dispose();
            web.Dispose();
            base.OnFormClosed(e);
        }
    }
}
