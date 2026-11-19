using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ClementiaPet
{
    internal sealed class BubbleWindow : NativeWindow, IDisposable
    {
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private readonly Font textFont = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
        private bool handleCreated;
        private int bubbleWidth, bubbleHeight;

        internal void ShowMessage(string text, Rectangle anchor)
        {
            if (string.IsNullOrEmpty(text)) return;
            Rectangle work = Screen.FromRectangle(anchor).WorkingArea;
            double scale = anchor.Width / 192.0;
            if (scale < 1) scale = 1;

            int maxWidth = Math.Min((int)Math.Round(360 * scale), Math.Max(180, work.Width - 24));
            Size measured = TextRenderer.MeasureText(text, textFont,
                new Size(maxWidth - (int)Math.Round(36 * scale), int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
            int width = Math.Max((int)Math.Round(188 * scale), measured.Width + (int)Math.Round(40 * scale));
            width = Math.Min(width, maxWidth);
            int height = Math.Max((int)Math.Round(58 * scale), measured.Height + (int)Math.Round(24 * scale));

            int x, y;
            Position(anchor, width, height, work, scale, out x, out y);
            bubbleWidth = width;
            bubbleHeight = height;

            if (!handleCreated)
            {
                CreateHandle(new CreateParams
                {
                    Caption = "ClementiaPet.Bubble",
                    Style = unchecked((int)0x80000000),
                    ExStyle = WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE,
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height
                });
                handleCreated = true;
            }
            Native.MoveAndResize(Handle, x, y, width, height);

            using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.Clear(Color.Transparent);
                    var bounds = new Rectangle(1, 1, width - 2, height - 2);
                    using (var path = RoundedRectangle(bounds, (int)Math.Round(13 * scale)))
                    using (var fill = new SolidBrush(Color.FromArgb(242, 247, 249, 251)))
                    using (var border = new Pen(Color.FromArgb(180, 91, 145, 161), (float)Math.Max(1, scale)))
                    {
                        graphics.FillPath(fill, path);
                        graphics.DrawPath(border, path);
                    }
                    using (var accent = new SolidBrush(Color.FromArgb(190, 59, 139, 157)))
                        graphics.FillRectangle(accent, (int)Math.Round(13 * scale), (int)Math.Round(14 * scale),
                            (int)Math.Round(3 * scale), height - (int)Math.Round(28 * scale));
                    var textBounds = new Rectangle((int)Math.Round(24 * scale), (int)Math.Round(10 * scale),
                        width - (int)Math.Round(36 * scale), height - (int)Math.Round(20 * scale));
                    TextRenderer.DrawText(graphics, text, textFont, textBounds, Color.FromArgb(238, 36, 57, 66),
                        TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl |
                        TextFormatFlags.VerticalCenter);
                }
                Native.Paint(Handle, new Point(x, y), bitmap);
            }
            Native.ShowWindow(Handle, 4);
        }

        internal void RepositionTo(Rectangle anchor)
        {
            if (!handleCreated) return;
            Rectangle work = Screen.FromRectangle(anchor).WorkingArea;
            double scale = Math.Max(1, anchor.Width / 192.0);
            int x, y;
            Position(anchor, bubbleWidth, bubbleHeight, work, scale, out x, out y);
            Native.MoveAndResize(Handle, x, y, bubbleWidth, bubbleHeight);
        }

        private static void Position(Rectangle anchor, int width, int height, Rectangle work,
            double scale, out int x, out int y)
        {
            int gap = (int)Math.Round(8 * scale);
            x = anchor.Right + gap;
            if (x + width > work.Right) x = anchor.Left - width - gap;
            y = anchor.Top + anchor.Height / 3 - height / 2;
            x = Math.Max(work.Left + 8, Math.Min(x, work.Right - width - 8));
            y = Math.Max(work.Top + 8, Math.Min(y, work.Bottom - height - 8));
        }

        internal void Hide()
        {
            if (handleCreated) Native.ShowWindow(Handle, 0);
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(1, radius * 2);
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        public void Dispose()
        {
            Hide();
            if (handleCreated)
            {
                DestroyHandle();
                handleCreated = false;
            }
            textFont.Dispose();
        }
    }
}
