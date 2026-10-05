using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace KnockKnock
{
    public static class Program
    {
        // Set by run-from-source.ps1: relaunch command for "Start with Windows", and the folder
        // holding ui.html and the WebView2 files that the .exe otherwise carries inside itself.
        public static string StartCommand;
        public static string ResourceDir;
        public static string UiFile;
        public static string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
        public static bool Debug;
        public static readonly string LocalDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KnockKnock");

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbedded;
            if (args.Length >= 2 && args[0] == "--test") { TestMode.Run(args); return; }
            if (args.Length == 2 && args[0] == "--make-icon") { AppIcon.WriteIco(args[1]); return; }
            Run(args);
        }

        // Kept separate from Main so WebView2 types load only after the resolver is registered.
        static void Run(string[] args)
        {
            bool created;
            using (new Mutex(true, "KnockKnock_SingleInstance", out created))
            using (var show = new EventWaitHandle(false, EventResetMode.AutoReset, "KnockKnock_Show"))
            {
                if (!created) { show.Set(); return; } // already running: ask it to open its window
                Debug = Array.IndexOf(args, "--debug") >= 0;
                Native.EnableDpiAwareness();
                PrepareWebView2Loader();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var app = new TrayApp(!Settings.Exists(), Array.IndexOf(args, "--open") >= 0);
                new Thread(() => { while (show.WaitOne()) app.UI.Post(_ => app.ShowWindow(), null); }) { IsBackground = true }.Start();
                Application.Run(app);
            }
        }

        static string Arch()
        {
            switch ((Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "").ToUpperInvariant())
            {
                case "AMD64": return "win-x64";
                case "ARM64": return "win-arm64";
                default: return "win-x86";
            }
        }

        // WebView2 needs its native loader DLL on disk; the .exe carries it and unpacks it once.
        static void PrepareWebView2Loader()
        {
            string dir;
            if (ResourceDir != null) dir = Path.Combine(ResourceDir, "runtimes", Arch());
            else
            {
                dir = Path.Combine(LocalDir, "runtime", Version, Arch());
                string dll = Path.Combine(dir, "WebView2Loader.dll");
                if (!File.Exists(dll))
                {
                    Directory.CreateDirectory(dir);
                    using (var src = Assembly.GetExecutingAssembly().GetManifestResourceStream("WebView2Loader." + Arch() + ".dll"))
                    using (var dst = File.Create(dll + ".tmp")) src.CopyTo(dst);
                    File.Move(dll + ".tmp", dll);
                }
            }
            CoreWebView2Environment.SetLoaderDllFolderPath(dir);
        }

        static Assembly ResolveEmbedded(object sender, ResolveEventArgs e)
        {
            string name = new AssemblyName(e.Name).Name;
            if (!name.StartsWith("Microsoft.Web.WebView2.")) return null;
            using (var s = Open(name + ".dll"))
            {
                if (s == null) return null;
                var ms = new MemoryStream(); s.CopyTo(ms);
                return Assembly.Load(ms.ToArray());
            }
        }

        // Embedded resource, or a file in ResourceDir when running from source.
        public static Stream Open(string name)
        {
            if (ResourceDir != null)
            {
                string p = name == "ui.html" && UiFile != null ? UiFile : Path.Combine(ResourceDir, name);
                return File.Exists(p) ? File.OpenRead(p) : null;
            }
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        }

        public static string ReadText(string name)
        {
            using (var s = Open(name))
            {
                if (s == null) throw new FileNotFoundException("Missing " + name + ". Run build.ps1 first.");
                using (var r = new StreamReader(s)) return r.ReadToEnd();
            }
        }
    }
}
