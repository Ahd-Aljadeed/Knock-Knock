using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KnockKnock
{
    class TrayApp : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "KnockKnock";

        public Settings Settings { get; private set; }
        public readonly Engine Engine;
        readonly NotifyIcon tray = new NotifyIcon();
        readonly SynchronizationContext ui;
        readonly Icon iconOn, iconOff;
        readonly ToolStripMenuItem listenItem;
        Mic mic;
        bool locked;
        SettingsWindow window;

        public event Action<ActivityEntry> ActivityAdded;
        public event Action StateChanged;

        public TrayApp(bool firstRun, bool openWindow)
        {
            // Always bind to this (UI) thread. When hosted by PowerShell, SynchronizationContext.Current
            // can be a generic context that runs callbacks on worker threads, freezing windows they create.
            ui = new WindowsFormsSynchronizationContext();
            MigrateFromTapShot();

            Settings = Settings.Load();
            Engine = new Engine(Settings);
            Engine.Fire += (b, n) => ui.Post(_ => RunAction(b, n, true), null);
            Engine.Activity += e => ui.Post(_ => { var h = ActivityAdded; if (h != null) h(e); }, null);

            iconOn = AppIcon.Tray(true);
            iconOff = AppIcon.Tray(false);

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open Knock Knock", null, (s, e) => ShowWindow());
            listenItem = new ToolStripMenuItem("Listening", null, (s, e) => SetListening(!Settings.Listening));
            menu.Items.Add(listenItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Delete last screenshot", null, (s, e) => DeleteLastScreenshot());
            menu.Items.Add("Open screenshots folder", null, (s, e) => OpenScreenshotFolder());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitThread());
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowWindow(); };
            tray.BalloonTipClicked += (s, e) =>
            {
                if (Actions.LastScreenshot != null && File.Exists(Actions.LastScreenshot)) System.Diagnostics.Process.Start(Actions.LastScreenshot);
            };
            tray.Visible = true;

            if (firstRun) SetStartup(true);
            else if (IsStartup()) SetStartup(true); // keep the startup entry pointing at this copy

            SystemEvents.SessionSwitch += OnSessionSwitch;
            UpdateMic();
            if (firstRun || openWindow) ShowWindow();
            if (firstRun) Settings.Save();

            // Test hook: KNOCK_SELFTEST=<knocks> fires that binding through the same path as real knocks.
            int selfTest;
            if (int.TryParse(Environment.GetEnvironmentVariable("KNOCK_SELFTEST"), out selfTest))
            {
                new Thread(() =>
                {
                    Thread.Sleep(3000);
                    var b = Settings.Find(selfTest);
                    if (b != null) ui.Post(_ => RunAction(b, selfTest, true), null);
                }) { IsBackground = true }.Start();
            }
        }

        public SynchronizationContext UI { get { return ui; } }

        // ---- Settings ----

        public void UpdateSettings(Settings s)
        {
            s.Fix();
            Settings = s;
            Settings.Save();
            Engine.Apply(s);
            UpdateMic();
        }

        public void SetListening(bool on)
        {
            Settings.Listening = on;
            Settings.Save();
            UpdateMic();
        }

        public bool IsListening { get { return mic != null; } }

        // The mic is open only while listening and unlocked; otherwise Windows shows no mic in use.
        void UpdateMic()
        {
            bool listen = Settings.Listening && !locked;
            if (listen && mic == null)
            {
                var m = new Mic(Engine.Process);
                m.OnError = msg => ui.Post(_ => { Engine.Log(msg, "error"); tray.ShowBalloonTip(5000, "Knock Knock", msg, ToolTipIcon.Error); }, null);
                m.Start();
                mic = m;
            }
            else if (!listen && mic != null)
            {
                mic.Dispose(); mic = null;
            }
            listenItem.Checked = Settings.Listening;
            tray.Icon = listen ? iconOn : iconOff;
            tray.Text = listen ? "Knock Knock: listening" : Settings.Listening ? "Knock Knock: mic off while locked" : "Knock Knock: mic off";
            var h = StateChanged; if (h != null) h();
        }

        void OnSessionSwitch(object s, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock) locked = true;
            else if (e.Reason == SessionSwitchReason.SessionUnlock) locked = false;
            else return;
            ui.Post(_ => UpdateMic(), null);
        }

        // ---- Actions ----

        public void RunAction(Binding b, int knocks, bool fromKnock)
        {
            string what = Actions.Describe(b);
            try
            {
                Actions.Run(b, Settings);
                Engine.Log((fromKnock ? knocks + " knocks: " : "Test: ") + what, "ok");
            }
            catch (Exception ex)
            {
                Engine.Log(what + " failed: " + ex.Message, "error");
                tray.ShowBalloonTip(4000, "Knock Knock", what + " failed: " + ex.Message, ToolTipIcon.Error);
            }
        }

        void DeleteLastScreenshot()
        {
            var p = Actions.LastScreenshot;
            if (p == null || !File.Exists(p)) { tray.ShowBalloonTip(2000, "Knock Knock", "No screenshot to delete.", ToolTipIcon.None); return; }
            File.Delete(p);
            Actions.LastScreenshot = null;
            Engine.Log("Deleted " + Path.GetFileName(p), "info");
            tray.ShowBalloonTip(2000, "Knock Knock", "Deleted " + Path.GetFileName(p), ToolTipIcon.None);
        }

        public void OpenScreenshotFolder()
        {
            Directory.CreateDirectory(Settings.ScreenshotFolder);
            System.Diagnostics.Process.Start(Settings.ScreenshotFolder);
        }

        // ---- Window ----

        public void ShowWindow()
        {
            if (window == null || window.IsDisposed)
            {
                window = new SettingsWindow(this);
                window.FormClosed += (s, e) => { window = null; GC.Collect(); };
                window.Show();
            }
            if (window.WindowState == FormWindowState.Minimized) window.WindowState = FormWindowState.Normal;
            window.Activate();
        }

        // ---- Startup ----

        public static bool IsStartup()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(RunName) != null;
        }

        public void SetStartup(bool on)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue(RunName, Program.StartCommand ?? "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue(RunName, false);
            }
        }

        // Knock Knock grew out of a prototype called TapShot; remove its leftovers.
        static void MigrateFromTapShot()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true)) if (k != null) k.DeleteValue("TapShot", false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\TapShot", false);
            }
            catch { }
        }

        protected override void ExitThreadCore()
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            if (window != null && !window.IsDisposed) window.Close();
            if (mic != null) mic.Dispose();
            tray.Visible = false; tray.Dispose();
            base.ExitThreadCore();
        }
    }
}
