using System;
using System.Drawing;

namespace ClementiaPet
{
    // Positions are physical screen pixels. Speeds are specified at 96 DPI.
    internal sealed class PetMotion
    {
        private readonly Random random;
        private readonly double[] curveLengths = new double[33];
        private double x, y, verticalSpeed, phaseTime, walkingSpeed;
        private int direction;
        private Rectangle route;
        private bool onCurve;
        private bool leftEndpoint;
        private double curveElapsed, curveDuration, curveDistance, hoverTime, flightSpeed;
        private PointF curveStart, curveControl1, curveControl2, curveEnd;
        internal bool Walking { get; private set; }
        internal bool FacingLeft { get { return direction < 0; } }
        internal double Bank { get; private set; }
        internal Point Position { get { return new Point((int)Math.Round(x), (int)Math.Round(y)); } }

        internal PetMotion(Random random) { this.random = random; }
        internal void Reset(Point at)
        {
            x = at.X; y = at.Y; verticalSpeed = 0; Walking = false;
            phaseTime = 2 + random.NextDouble() * 2;
            route = Rectangle.Empty; onCurve = false; Bank = 0;
            curveElapsed = curveDuration = curveDistance = hoverTime = 0;
        }

        internal void StopWalking()
        {
            Walking = false;
            phaseTime = 1.3;
        }

        internal void GroundStep(Rectangle work, int side, double dt, bool interacting)
        {
            double scale = side / 192.0;
            double floor = Math.Max(work.Top, work.Bottom - side + Math.Floor(side * 15.0 / 288));
            double right = Math.Max(work.Left, work.Right - side);
            x = Math.Max(work.Left, Math.Min(right, x));
            y = Math.Max(work.Top, Math.Min(floor, y));
            if (y < floor || verticalSpeed < 0)
            {
                Walking = false;
                verticalSpeed = Math.Min(1000 * scale, verticalSpeed + 1260 * scale * dt);
                y += verticalSpeed * dt;
                if (y >= floor)
                {
                    y = floor;
                    verticalSpeed = verticalSpeed > 165 * scale ? -verticalSpeed * 0.18 : 0;
                }
                return;
            }
            if (interacting) { Walking = false; phaseTime = 1.3; return; }
            phaseTime -= dt;
            if (phaseTime <= 0)
            {
                Walking = !Walking;
                phaseTime = Walking ? 1.8 + random.NextDouble() * 3 : 1.6 + random.NextDouble() * 3;
                if (Walking)
                {
                    direction = random.Next(2) == 0 ? -1 : 1;
                    walkingSpeed = 30 + random.NextDouble() * 26;
                }
            }
            if (!Walking) return;
            x += walkingSpeed * scale * direction * dt;
            if (x <= work.Left) { x = work.Left; direction = 1; }
            if (x >= right) { x = right; direction = -1; }
        }

        internal void FlyStep(Rectangle client, Rectangle work, int side, double dt)
        {
            double scale = side / 192.0;
            double margin = 12 * scale;
            double safeLeft = client.Left + margin;
            double safeRight = client.Right - side - margin;
            double safeTop = client.Top + margin;
            double safeBottom = client.Bottom - side - margin;
            double before = x;

            if (client.IsEmpty || safeRight - safeLeft < 1 || safeBottom - safeTop < 1)
            {
                route = Rectangle.Empty;
                onCurve = false;
                double targetX = Math.Max(work.Left, work.Right - side - 16 * scale);
                double targetY = Math.Max(work.Top, work.Bottom - side - 16 * scale);
                double step = 180 * scale * dt;
                double dx = targetX - x, dy = targetY - y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                if (length > .1)
                {
                    double move = Math.Min(length, step);
                    x += dx * move / length;
                    y += dy * move / length;
                }
                UpdateBank(before, scale, dt);
                return;
            }

            var safe = new Rectangle((int)Math.Round(safeLeft), (int)Math.Round(safeTop),
                Math.Max(0, (int)Math.Round(safeRight - safeLeft)),
                Math.Max(0, (int)Math.Round(safeBottom - safeTop)));
            if (safe != route)
            {
                route = safe;
                x = Math.Max(safe.Left, Math.Min(safe.Right, x));
                y = Math.Max(safe.Top, Math.Min(safe.Bottom, y));
                onCurve = false;
                hoverTime = 0;
            }

            if (!onCurve)
            {
                if (hoverTime > 0)
                {
                    hoverTime -= dt;
                    UpdateBank(before, scale, dt);
                    return;
                }
                leftEndpoint = x > route.Left + route.Width / 2.0;
                StartCurve(scale);
            }

            curveElapsed += dt;
            double progress = Math.Min(1, curveElapsed / Math.Max(.001, curveDuration));
            double eased = progress * progress * (3 - 2 * progress);
            double wanted = curveDistance * eased;
            PointF next = PointAlongCurve(wanted);
            x = next.X;
            y = next.Y;
            if (progress >= 1)
            {
                x = curveEnd.X;
                y = curveEnd.Y;
                onCurve = false;
                hoverTime = 1.0 + random.NextDouble();
                leftEndpoint = !leftEndpoint;
            }
            UpdateBank(before, scale, dt);
        }

        private void StartCurve(double scale)
        {
            double bandDepth = route.Height * .32;
            if (bandDepth < 24 * scale) bandDepth = route.Height;
            double endpointX = leftEndpoint ? route.Left : route.Right;
            double endpointY = route.Top + bandDepth * (.35 + random.NextDouble() * .3);
            curveStart = new PointF((float)x, (float)y);
            curveEnd = new PointF((float)endpointX, (float)Math.Min(route.Bottom, endpointY));
            double dx = curveEnd.X - curveStart.X;
            double dy = curveEnd.Y - curveStart.Y;
            curveControl1 = new PointF((float)(curveStart.X + dx * .28),
                (float)(curveStart.Y + dy * .55 - route.Height * .05));
            curveControl2 = new PointF((float)(curveStart.X + dx * .72),
                (float)(curveEnd.Y - route.Height * .08));
            curveControl1.X = (float)Math.Max(route.Left, Math.Min(route.Right, curveControl1.X));
            curveControl1.Y = (float)Math.Max(route.Top, Math.Min(route.Bottom, curveControl1.Y));
            curveControl2.X = (float)Math.Max(route.Left, Math.Min(route.Right, curveControl2.X));
            curveControl2.Y = (float)Math.Max(route.Top, Math.Min(route.Bottom, curveControl2.Y));
            curveLengths[0] = 0;
            PointF previous = curveStart;
            for (int i = 1; i < curveLengths.Length; ++i)
            {
                PointF point = CurvePoint(i / (double)(curveLengths.Length - 1));
                double lx = point.X - previous.X, ly = point.Y - previous.Y;
                curveLengths[i] = curveLengths[i - 1] + Math.Sqrt(lx * lx + ly * ly);
                previous = point;
            }
            curveDistance = curveLengths[curveLengths.Length - 1];
            flightSpeed = 45 + random.NextDouble() * 40;
            curveDuration = Math.Max(.45, curveDistance / Math.Max(1, flightSpeed * scale));
            curveElapsed = 0;
            onCurve = true;
        }

        private PointF PointAlongCurve(double distance)
        {
            int segment = 1;
            while (segment < curveLengths.Length - 1 && curveLengths[segment] < distance) ++segment;
            double low = curveLengths[segment - 1], high = curveLengths[segment];
            double fraction = high > low ? (distance - low) / (high - low) : 0;
            double t = (segment - 1 + fraction) / (curveLengths.Length - 1);
            return CurvePoint(t);
        }

        private PointF CurvePoint(double t)
        {
            double u = 1 - t;
            double a = u * u * u, b = 3 * u * u * t, c = 3 * u * t * t, d = t * t * t;
            return new PointF(
                (float)(a * curveStart.X + b * curveControl1.X + c * curveControl2.X + d * curveEnd.X),
                (float)(a * curveStart.Y + b * curveControl1.Y + c * curveControl2.Y + d * curveEnd.Y));
        }

        private void UpdateBank(double before, double scale, double dt)
        {
            double desired = Math.Max(-1, Math.Min(1, (x - before) / Math.Max(.001, 90 * scale * dt)));
            Bank += (desired - Bank) * (1 - Math.Exp(-7 * dt));
        }
    }
}
