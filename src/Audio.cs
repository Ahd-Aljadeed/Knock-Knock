using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace KnockKnock
{
    // Minimal WASAPI (Core Audio) interop.
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, int mask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct AudioClientProperties { public int cbSize, bIsOffload, eCategory, Options; }

    [ComImport, Guid("726778CD-F60A-4eda-82DE-E47610CD78AA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClient2
    {
        [PreserveSig] int Initialize(int mode, int flags, long bufferDuration, long periodicity, IntPtr format, IntPtr session);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint frames);
        [PreserveSig] int IsFormatSupported(int mode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long def, out long min);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr h);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object o);
        [PreserveSig] int IsOffloadCapable(int category, out int capable);
        [PreserveSig] int SetClientProperties(ref AudioClientProperties props);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out long devPos, out long qpcPos);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }

    // Captures the default mic in RAW mode (bypasses the driver's noise suppression,
    // which otherwise erases knocks), downmixes to mono and resamples to 16 kHz.
    // Polls every 50 ms and reopens the device if it disappears.
    // Audio is handed out 10 ms at a time and never stored.
    class Mic : IDisposable
    {
        public const int Rate = 16000, Slice = 160;
        const int AUDCLNT_BUFFERFLAGS_SILENT = 2;
        Thread thread; volatile bool running;
        readonly Action<float[], int> onFrame;
        public Action<string> OnError;

        public Mic(Action<float[], int> onFrame) { this.onFrame = onFrame; }

        public void Start()
        {
            running = true;
            thread = new Thread(Run) { IsBackground = true, Priority = ThreadPriority.AboveNormal, Name = "Knock Knock audio" };
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }

        void Run()
        {
            string lastError = null;
            while (running)
            {
                try { Capture(); lastError = null; }
                catch (Exception ex)
                {
                    if (ex.Message != lastError && OnError != null) OnError(ex.Message);
                    lastError = ex.Message;
                }
                for (int i = 0; i < 20 && running; i++) Thread.Sleep(100); // retry in 2 s
            }
        }

        static void Check(int hr, string what)
        {
            if (hr < 0) throw new Exception("Microphone error: " + what + " failed (0x" + hr.ToString("X8") + ")");
        }

        void Capture()
        {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            IMMDevice dev;
            Check(en.GetDefaultAudioEndpoint(1 /*capture*/, 0, out dev), "finding a microphone");
            Guid iid = typeof(IAudioClient2).GUID; object o;
            Check(dev.Activate(ref iid, 23, IntPtr.Zero, out o), "opening the microphone");
            var ac = (IAudioClient2)o;
            var props = new AudioClientProperties { cbSize = 16, Options = 1 /*RAW*/ };
            ac.SetClientProperties(ref props); // best effort: older drivers may not support raw

            IntPtr fmt;
            Check(ac.GetMixFormat(out fmt), "reading the mic format");
            int ch = Marshal.ReadInt16(fmt, 2), rate = Marshal.ReadInt32(fmt, 4), bits = Marshal.ReadInt16(fmt, 14);
            int tag = (ushort)Marshal.ReadInt16(fmt, 0);
            if (tag == 0xFFFE) tag = Marshal.ReadInt16(fmt, 24); // WAVEFORMATEXTENSIBLE subformat
            bool isFloat = tag == 3 && bits == 32, isPcm16 = tag == 1 && bits == 16;
            try
            {
                if (!isFloat && !isPcm16) throw new Exception("Unsupported mic format (" + bits + "-bit, type " + tag + ")");
                Check(ac.Initialize(0 /*shared*/, 0, 2000000 /*200 ms*/, 0, fmt, IntPtr.Zero), "starting the microphone");
            }
            finally { Native.CoTaskMemFree(fmt); }

            Guid cid = typeof(IAudioCaptureClient).GUID; object co;
            Check(ac.GetService(ref cid, out co), "opening the capture stream");
            var cc = (IAudioCaptureClient)co;
            Check(ac.Start(), "starting capture");

            var slice = new float[Slice];
            int fill = 0, phase = 0, cnt = 0; float acc = 0;
            var tmp = new float[0]; var tmp16 = new short[0];
            try
            {
                while (running)
                {
                    Thread.Sleep(50);
                    uint n;
                    Check(cc.GetNextPacketSize(out n), "reading audio");
                    while (n > 0)
                    {
                        IntPtr data; uint frames, flags; long p1, p2;
                        Check(cc.GetBuffer(out data, out frames, out flags, out p1, out p2), "reading audio");
                        int count = (int)frames * ch;
                        if (tmp.Length < count) { tmp = new float[count]; tmp16 = new short[count]; }
                        if ((flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0) Array.Clear(tmp, 0, count);
                        else if (isFloat) Marshal.Copy(data, tmp, 0, count);
                        else { Marshal.Copy(data, tmp16, 0, count); for (int i = 0; i < count; i++) tmp[i] = tmp16[i] / 32768f; }
                        cc.ReleaseBuffer(frames);

                        // Downmix and box-filter resample to 16 kHz.
                        for (int f = 0; f < frames; f++)
                        {
                            float s = 0;
                            for (int c = 0; c < ch; c++) s += tmp[f * ch + c];
                            acc += s / ch; cnt++;
                            phase += Rate;
                            if (phase >= rate)
                            {
                                phase -= rate;
                                slice[fill++] = acc / cnt; acc = 0; cnt = 0;
                                if (fill == Slice) { onFrame(slice, Slice); fill = 0; }
                            }
                        }
                        Check(cc.GetNextPacketSize(out n), "reading audio");
                    }
                }
            }
            finally
            {
                ac.Stop();
                Marshal.ReleaseComObject(cc); Marshal.ReleaseComObject(ac);
                Marshal.ReleaseComObject(dev); Marshal.ReleaseComObject(en);
            }
        }

        public void Dispose()
        {
            running = false;
            if (thread != null) thread.Join(2000);
        }
    }
}
