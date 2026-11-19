using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ClementiaPet
{
    internal sealed class HeartBubble : NativeWindow, IDisposable
    {
        private double amount;
        internal bool Visible { get { return lastAlpha > 0; } }
        private int lastAlpha, lastSize;
        private Point lastLocation;

        internal static Rectangle Placement(Rectangle pet, double visibility)
        {
            double scale = pet.Width / 192.0;
            int size = Math.Max(1, (int)Math.Round(32 * scale));
            return new Rectangle(pet.Left + (int)Math.Round(139 * scale),
                pet.Top + (int)Math.Round((44 + 8 * (1 - visibility)) * scale), size, size);
        }

        internal static void Draw(Graphics g, int size, int alpha)
        {
            var state = g.Save();
            g.ScaleTransform(size / 32f, size / 32f);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var balloon = new GraphicsPath())
            using (var heart = new GraphicsPath())
            using (var paper = new SolidBrush(Color.FromArgb(alpha, 255, 250, 246)))
            using (var ink = new Pen(Color.FromArgb(alpha, 119, 102, 116), 1.2f))
            using (var pink = new SolidBrush(Color.FromArgb(alpha, 219, 108, 131)))
            {
                balloon.AddBezier(2, 14, 2, -2, 30, -2, 30, 13);
                balloon.AddBezier(30, 13, 30, 22, 22, 25, 14, 25);
                balloon.AddBezier(14, 25, 12, 27, 8, 29, 5, 29);
                balloon.AddLine(5, 29, 7, 23);
                balloon.AddBezier(7, 23, 3, 21, 2, 18, 2, 14);
                balloon.CloseFigure();
                g.FillPath(paper, balloon);
                g.DrawPath(ink, balloon);
                heart.AddBezier(16, 10, 11, 4, 5, 10, 10, 15);
                heart.AddBezier(10, 15, 12, 17, 14, 19, 16, 20);
                heart.AddBezier(16, 20, 19, 18, 24, 14, 24, 11);
                heart.AddBezier(24, 11, 24, 6, 19, 6, 16, 10);
                heart.CloseFigure();
                g.FillPath(pink, heart);
            }
            g.Restore(state);
        }

        internal void Update(Rectangle pet, bool held, double dt)
        {
            amount = Math.Max(0, Math.Min(1, amount + dt * (held ? 3.5 : -5)));
            int alpha = (int)Math.Round(255 * amount);
            if (alpha == 0) { Hide(); return; }
            Rectangle bounds = Placement(pet, amount);
            if (Handle == IntPtr.Zero)
                CreateHandle(new CreateParams { Caption = "ClementiaPet.Heart", Style = unchecked((int)0x80000000),
                    ExStyle = 0x80000 | 0x08000000 | 0x80 | 0x20 | 8 });
            if (bounds.Size.Width != lastSize || alpha != lastAlpha)
            {
                using (var image = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb))
                {
                    using (var graphics = Graphics.FromImage(image)) Draw(graphics, bounds.Width, alpha);
                    Native.Paint(Handle, bounds.Location, image);
                }
                if (lastAlpha == 0) Native.ShowWindow(Handle, 4);
            }
            else if (bounds.Location != lastLocation)
                Native.MoveAndResize(Handle, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            lastLocation = bounds.Location;
            lastAlpha = alpha;
            lastSize = bounds.Width;
        }

        internal void Hide()
        {
            if (lastAlpha != 0) Native.ShowWindow(Handle, 0);
            amount = 0;
            lastAlpha = 0;
        }

        public void Dispose() { if (Handle != IntPtr.Zero) DestroyHandle(); }
    }
}
