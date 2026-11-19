using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ClementiaPet
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct XY
        {
            internal int X, Y;
            internal XY(int x, int y) { X = x; Y = y; }
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public uint Size;
            public int Width, Height;
            public ushort Planes, Bits;
            public uint Compression, ImageSize;
            public int XPels, YPels;
            public uint Colors, Important;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Blend { public byte Op, Flags, Alpha, Format; }

        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr w, int command);
        [DllImport("user32.dll", SetLastError=true)] internal static extern bool SetWindowPos(IntPtr w, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] internal static extern IntPtr SetCapture(IntPtr w);
        [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr w);
        [DllImport("user32.dll")] internal static extern int GetWindowLongW(IntPtr w, int index);
        [DllImport("user32.dll")] internal static extern int SetWindowLongW(IntPtr w, int index, int value);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr w);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr w);
        [DllImport("user32.dll", SetLastError=true)] private static extern bool UpdateLayeredWindow(IntPtr w, IntPtr screen, ref XY pt, ref XY size, IntPtr dc, ref XY origin, int key, ref Blend blend, int flags);
        [DllImport("gdi32.dll", SetLastError=true)] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError=true)] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
        [DllImport("kernel32.dll", EntryPoint="RtlMoveMemory")] private static extern void CopyPixels(IntPtr dst, IntPtr src, UIntPtr bytes);

        internal static void Paint(IntPtr window, Point location, Bitmap frame)
        {
            int width = frame.Width, height = frame.Height;
            BitmapInfo info = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, Bits = 32 };
            IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
            if (dc == IntPtr.Zero) throw new Win32Exception();
            IntPtr dib = IntPtr.Zero, old = IntPtr.Zero, bits;
            try
            {
                dib = CreateDIBSection(dc, ref info, 0, out bits, IntPtr.Zero, 0);
                if (dib == IntPtr.Zero) throw new Win32Exception();
                old = SelectObject(dc, dib);
                var data = frame.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    // Both buffers are top-down 32-bit DIBs created here.
                    CopyPixels(bits, data.Scan0, (UIntPtr)(uint)(width * height * 4));
                }
                finally { frame.UnlockBits(data); }
                XY dst = new XY(location.X, location.Y), size = new XY(width, height), origin = new XY();
                Blend blend = new Blend { Alpha = 255, Format = 1 };
                if (!UpdateLayeredWindow(window, IntPtr.Zero, ref dst, ref size, dc, ref origin, 0, ref blend, 2))
                    throw new Win32Exception();
            }
            finally
            {
                if (old != IntPtr.Zero) SelectObject(dc, old);
                if (dib != IntPtr.Zero) DeleteObject(dib);
                DeleteDC(dc);
            }
        }

        internal static void MoveAndResize(IntPtr window, int x, int y, int width, int height)
        {
            SetWindowPos(window, new IntPtr(-1), x, y, width, height, 0x0014);
        }
    }
}
