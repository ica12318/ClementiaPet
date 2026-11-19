using System;
using System.Drawing;

namespace ClementiaPet
{
    internal sealed class LiftMotion
    {
        private Point previous;
        private double angle, angularVelocity, stretch;
        internal int Angle { get { return (int)Math.Round(angle); } }
        internal int Stretch { get { return (int)Math.Round(stretch * 100); } }
        // The first nine 50-ms frames only play on pickup.
        internal static long ClipTime(long ms) { return ms < 450 ? ms : 450 + (ms - 450) % 1950; }
        private const int WaistX = 108, WaistY = 124;

        internal static Point Grip(int side)
        {
            return new Point((int)Math.Round(WaistX * side / 288.0), (int)Math.Round(WaistY * side / 288.0));
        }

        internal void Begin(Point grab)
        {
            previous = grab;
            angle = angularVelocity = stretch = 0;
        }

        internal void Draw(Graphics g, Bitmap sprite, int size)
        {
            float gripX = WaistX * size / 288f;
            float gripY = WaistY * size / 288f;
            var state = g.Save();
            g.TranslateTransform(gripX, gripY);
            g.RotateTransform(Angle);
            g.ScaleTransform(1, 1 + Stretch / 100f);
            g.TranslateTransform(-gripX, -gripY);
            g.DrawImage(sprite, new Rectangle(0, 0, size, size));
            g.Restore(state);
        }

        internal void Step(Point grab, int side, double dt)
        {
            if (dt <= 0) return;
            double speedScale = 192.0 / (side * dt);
            double vx = (grab.X - previous.X) * speedScale;
            double vy = (grab.Y - previous.Y) * speedScale;
            previous = grab;
            // Positive screen rotation sends the feet left when the waist moves right.
            double target = Math.Max(-14, Math.Min(14, vx * .07));
            double extension = Math.Max(-.035, Math.Min(.035, -vy * .00012));
            int steps = Math.Max(1, (int)Math.Ceiling(dt / .01));
            double h = dt / steps;
            for (int i = 0; i < steps; ++i)
            {
                angularVelocity += (145 * (target - angle) - 11 * angularVelocity) * h;
                angle += angularVelocity * h;
            }
            stretch += (extension - stretch) * (1 - Math.Exp(-10 * dt));
        }
    }
}
