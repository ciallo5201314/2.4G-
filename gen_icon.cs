// gen_icon.cs -- generate app.ico (battery shape) - one-shot utility
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class GenIcon
{
    static void Main(string[] args)
    {
        string outPath = args.Length > 0 ? args[0] : "app.ico";
        int[] sizes = { 16, 32, 48, 64, 256 };
        var bmps = new Bitmap[sizes.Length];
        for (int i = 0; i < sizes.Length; i++)
        {
            bmps[i] = MakeBattery(sizes[i]);
        }
        using (var fs = File.Create(outPath))
        {
            BinaryWriter w = new BinaryWriter(fs);
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                byte dim = (byte)(s >= 256 ? 0 : s);
                w.Write(dim); w.Write(dim);
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                byte[] png = PngBytes(bmps[i]);
                w.Write(png.Length); w.Write(offset);
                offset += png.Length;
            }
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write(PngBytes(bmps[i]));
            }
        }
        Console.WriteLine("written: " + outPath);
    }

    static byte[] PngBytes(Bitmap b)
    {
        using (var ms = new MemoryStream())
        {
            b.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }

    static Bitmap MakeBattery(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float k = size / 64f;
            // body rect + cap rect (same proportions as tray icon)
            RectangleF body = new RectangleF(14 * k, 10 * k, 36 * k, 50 * k);
            RectangleF cap = new RectangleF(26 * k, 2 * k, 12 * k, 8 * k);
            float radius = 6 * k;
            // cap
            using (var p = new Pen(Color.White, 4 * k))
            {
                g.DrawLine(p, body.Left + body.Width / 2 - 6 * k, body.Top, body.Left + body.Width / 2 + 6 * k, body.Top);
            }
            // fill level 60% from bottom
            RectangleF fill = new RectangleF(body.Left, body.Top + body.Height * 0.4f, body.Width, body.Height * 0.6f);
            using (var bodyPath = RoundedRect(body, radius))
            {
                using (var b = new SolidBrush(Color.FromArgb(0x5D, 0xDC, 0x8A)))
                {
                    g.FillPath(b, RoundedRect(fill, radius));
                }
                using (var p = new Pen(Color.White, 4 * k))
                {
                    g.DrawPath(p, bodyPath);
                }
            }
        }
        return bmp;
    }

    static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}