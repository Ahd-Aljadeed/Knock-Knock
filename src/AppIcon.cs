using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace KnockKnock
{
    // The app icon: a knock with two ripples on a rounded square. Drawn in code so the
    // tray, the window and the .ico file (tools/make-icon.ps1) all match.
    public static class AppIcon
    {
        public static Bitmap Draw(int size, bool on)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float s = size, r = s * 0.24f, inset = s <= 16 ? 0 : s * 0.03f;
                var rect = new RectangleF(inset, inset, s - 2 * inset, s - 2 * inset);
                using (var path = Rounded(rect, r))
                using (var bg = new LinearGradientBrush(rect,
                    on ? Color.FromArgb(255, 255, 122, 69) : Color.FromArgb(255, 120, 120, 128),
                    on ? Color.FromArgb(255, 226, 61, 90) : Color.FromArgb(255, 82, 82, 92), 45f))
                    g.FillPath(bg, path);

                float cx = s * 0.36f, cy = s * 0.5f, dot = s * 0.1f;
                g.FillEllipse(Brushes.White, cx - dot, cy - dot, dot * 2, dot * 2);
                float w = Math.Max(1.5f, s * 0.075f);
                using (var p1 = new Pen(Color.White, w) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                using (var p2 = new Pen(Color.FromArgb(170, 255, 255, 255), w) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    float a = s * 0.22f, b = s * 0.36f;
                    g.DrawArc(p1, cx - a, cy - a, a * 2, a * 2, -50, 100);
                    g.DrawArc(p2, cx - b, cy - b, b * 2, b * 2, -45, 90);
                }
                if (!on)
                    using (var slash = new Pen(Color.White, Math.Max(2f, s * 0.09f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLine(slash, s * 0.2f, s * 0.8f, s * 0.8f, s * 0.2f);
            }
            return bmp;
        }

        static GraphicsPath Rounded(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Icon Tray(bool on)
        {
            using (var bmp = Draw(32, on)) return Icon.FromHandle(bmp.GetHicon());
        }

        // Writes a multi-size .ico (PNG-compressed entries).
        public static void WriteIco(string path)
        {
            var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
            var pngs = new List<byte[]>();
            foreach (var sz in sizes)
                using (var bmp = Draw(sz, true))
                using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }

            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                    w.Write(pngs[i].Length); w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (var p in pngs) w.Write(p);
            }
        }
    }
}
