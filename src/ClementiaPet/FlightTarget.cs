using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ClementiaPet
{
    internal static class FlightTarget
    {
        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref Native.XY point);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int size);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int bytes);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int pid);

        internal static Rectangle Read(int ownPid)
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || IsIconic(hwnd) || !IsWindowVisible(hwnd)) return Rectangle.Empty;
            int pid, cloaked;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid == ownPid || (DwmGetWindowAttribute(hwnd, 14, out cloaked, 4) == 0 && cloaked != 0)) return Rectangle.Empty;
            var name = new StringBuilder(80);
            GetClassName(hwnd, name, name.Capacity);
            switch (name.ToString())
            {
                case "Progman": case "WorkerW": case "Shell_TrayWnd": case "Shell_SecondaryTrayWnd": return Rectangle.Empty;
            }
            Rect rect;
            Native.XY origin = new Native.XY();
            if (!GetClientRect(hwnd, out rect) || !ClientToScreen(hwnd, ref origin)) return Rectangle.Empty;
            Rectangle client = new Rectangle(origin.X, origin.Y, rect.Right, rect.Bottom);
            return Rectangle.Intersect(client, Screen.FromRectangle(client).WorkingArea);
        }
    }
}
