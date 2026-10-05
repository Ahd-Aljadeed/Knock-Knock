using System;
using System.Runtime.InteropServices;

namespace KnockKnock
{
    static class Native
    {
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
        [DllImport("kernel32.dll")] public static extern bool AllocConsole();
        [DllImport("ole32.dll")] public static extern void CoTaskMemFree(IntPtr p);
        [DllImport("user32.dll")] public static extern bool LockWorkStation();
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public const uint KEYEVENTF_EXTENDEDKEY = 1, KEYEVENTF_KEYUP = 2;

        [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public int cbSize; public uint dwTime; }
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO lii);

        // Milliseconds since the last keyboard/mouse/touchpad input.
        public static uint MsSinceInput()
        {
            var lii = new LASTINPUTINFO { cbSize = 8 };
            return GetLastInputInfo(ref lii) ? (uint)Environment.TickCount - lii.dwTime : uint.MaxValue;
        }

        public static void EnableDpiAwareness()
        {
            // Per-monitor aware so every screen is captured at its real pixel size.
            if (!SetProcessDpiAwarenessContext((IntPtr)(-4))) SetProcessDPIAware();
        }
    }
}
