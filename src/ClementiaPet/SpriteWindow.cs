using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ClementiaPet
{
    internal sealed class JournalRequestEventArgs : EventArgs
    {
        internal bool Advance { get; private set; }
        internal JournalRequestEventArgs(bool advance) { Advance = advance; }
    }

    internal sealed class SpriteWindow : NativeWindow, IDisposable
    {
        private enum PetPart { HumanHead, HumanHand, HumanBody, HumanManuscript, RayHead, RayBody }

        private const double MorphDuration = .7;
        private const double BubbleDuration = 3.0;
        private readonly AnimationClip idle = AnimationClip.Human("Relax", 49, 2400);
        private readonly AnimationClip touch = AnimationClip.Human("Interact", 45, 2200);
        private readonly AnimationClip walk = AnimationClip.Human("Move", 49, 2400);
        private readonly AnimationClip lifted = AnimationClip.Human("Lift", 48, 2400);
        private readonly AnimationClip swim = AnimationClip.Ray();
        private readonly LiftMotion lift = new LiftMotion();
        private readonly Stopwatch hold = new Stopwatch();
        private readonly Stopwatch playback = new Stopwatch();
        private readonly Stopwatch motionClock = new Stopwatch();
        private readonly Stopwatch gestureClock = new Stopwatch();
        private readonly Stopwatch morphClock = new Stopwatch();
        private readonly Stopwatch reactionClock = new Stopwatch();
        private readonly Timer animation = new Timer { Interval = 15 };
        private readonly PetMotion motion = new PetMotion(new Random());
        private readonly ContentStore content;
        private readonly BubbleWindow bubble = new BubbleWindow();
        private readonly HeartBubble heart = new HeartBubble();
        private readonly int ownPid = Process.GetCurrentProcess().Id;
        private readonly bool testHooks = Environment.GetEnvironmentVariable("CLEMENTIAPET_TEST_HOOKS") == "1";
        private readonly ContextMenuStrip menu;
        private double targetPoll;
        private double bubbleUntil;
        private double pendingTime;
        private Rectangle client;
        private int lastPose, lastStretch, lastReaction;
        private int side = 192;
        private int reactionSign = 1;
        private AnimationClip current;
        private Bitmap lastFrame;
        private Point position, press, start, pointer, pendingPoint;
        private bool pressed, dragging, rayForm, reading, morphing, morphTargetRay;
        private bool pendingClick, secondClickCandidate, lastPressed;
        private PetPart pendingPart;

        internal bool Through { get; private set; }
        internal bool Visible { get; private set; }
        internal string FormName { get { return rayForm ? "ray" : "human"; } }
        internal bool CanSpeak { get { return Visible && !reading && !menu.Visible && !pressed &&
            !pendingClick && !dragging && !morphing && bubbleUntil == 0 && !heart.Visible && current != touch; } }
        private bool locked, suspended;
        internal bool Unavailable { get { return locked || suspended || !Visible; } }
        internal event Action AvailabilityChanged;
        [System.Runtime.InteropServices.DllImport("wtsapi32.dll")]
        private static extern bool WTSRegisterSessionNotification(IntPtr hwnd, int flags);
        [System.Runtime.InteropServices.DllImport("wtsapi32.dll")]
        private static extern bool WTSUnRegisterSessionNotification(IntPtr hwnd);
        internal event EventHandler CloseRequested;
        internal event EventHandler<JournalRequestEventArgs> JournalRequested;
        internal event EventHandler SettingsChanged;

        internal Rectangle Bounds { get { return new Rectangle(position, new Size(side, side)); } }

        internal SpriteWindow(ContextMenuStrip menu, ContentStore content, PetSettings settings)
        {
            this.menu = menu;
            this.content = content;
            rayForm = settings != null && string.Equals(settings.Form, "ray", StringComparison.Ordinal);
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            if (settings != null && settings.HasPosition)
                position = new Point(settings.X, settings.Y);
            else
                position = new Point(area.Right - 244, area.Bottom - 228);
            if (settings != null && settings.Through) Through = true;
            CreateHandle(new CreateParams
            {
                Caption = "ClementiaPet.Sprite",
                Style = unchecked((int)0x80000000),
                ExStyle = 0x80000 | 0x08000000 | 0x80 | 8,
                X = position.X,
                Y = position.Y,
                Width = side,
                Height = side
            });
            animation.Tick += delegate { Tick(); };
            WTSRegisterSessionNotification(Handle, 0);
            gestureClock.Start();
        }

        internal void RefreshInitialScale()
        {
            side = (int)Math.Round(192 * Native.GetDpiForWindow(Handle) / 96.0);
            Rectangle area = Screen.FromPoint(position).WorkingArea;
            if (!IsPositionUsable(position))
                position = new Point(area.Right - side - 48, area.Bottom - side - 28);
            Move(position, area);
            if (Through) ApplyThroughStyle(true);
            ShowCore();
        }

        internal void CaptureSettings(PetSettings settings)
        {
            if (settings == null) return;
            settings.Form = (morphing ? morphTargetRay : rayForm) ? "ray" : "human";
            settings.X = position.X;
            settings.Y = position.Y;
            settings.Scale = side / 192.0;
            settings.Through = Through;
            settings.HasPosition = true;
        }

        internal void ShowHint()
        {
            ShowBubble(content.FirstRunHint);
        }

        internal void ReadJournal()
        {
            HideBubble();
            reading = true;
            if (JournalRequested != null)
                JournalRequested(this, new JournalRequestEventArgs(false));
        }

        internal void SetReading(bool value)
        {
            reading = value;
            motionClock.Restart();
            HideBubble();
            if (!value && Visible && !morphing)
            {
                motion.Reset(position);
                targetPoll = 0;
                Play(rayForm ? swim : idle);
            }
        }

        internal void Recall()
        {
            SetThrough(false);
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            position = new Point(area.Right - side - 48, area.Bottom - side - 28);
            Move(position, area);
            ShowCore();
            SettingsChangedRaised();
        }

        internal void Hide()
        {
            DiscardPendingClick();
            CancelPress();
            HideBubble();
            reading = false;
            animation.Stop();
            heart.Hide();
            playback.Stop();
            morphClock.Reset();
            morphing = false;
            Visible = false;
            if (AvailabilityChanged != null) AvailabilityChanged();
            Native.ShowWindow(Handle, 0);
        }

        internal void Swap()
        {
            if (morphing) return;
            if (!Visible)
            {
                DiscardPendingClick();
                rayForm = !rayForm;
                motion.Reset(position);
                Play(rayForm ? swim : idle);
                SettingsChangedRaised();
                return;
            }
            BeginMorph(!rayForm);
        }

        internal void SetThrough(bool enable)
        {
            if (Through == enable)
            {
                ApplyThroughStyle(enable);
                return;
            }
            DiscardPendingClick();
            CancelPress();
            ApplyThroughStyle(enable);
            Through = enable;
            SettingsChangedRaised();
        }

        internal bool ShowAutomatic(string text)
        {
            if (!CanSpeak || Unavailable) return false;
            ShowBubble(text, SpeechTiming.BubbleSeconds);
            return true;
        }

        internal void ShowBubble(string text, double duration = BubbleDuration)
        {
            if (!Visible || string.IsNullOrEmpty(text)) return;
            bubble.ShowMessage(text, Bounds);
            bubbleUntil = gestureClock.Elapsed.TotalSeconds + duration;
        }

        internal void HideBubble()
        {
            bubble.Hide();
            bubbleUntil = 0;
        }

        private void ShowCore()
        {
            Visible = true;
            if (AvailabilityChanged != null) AvailabilityChanged();
            motion.Reset(position);
            targetPoll = 0;
            motionClock.Restart();
            Play(rayForm ? swim : idle);
            animation.Start();
            Native.ShowWindow(Handle, 4); // SW_SHOWNOACTIVATE
        }

        private void ApplyThroughStyle(bool enable)
        {
            const int exStyle = -20, transparent = 0x20;
            int style = Native.GetWindowLongW(Handle, exStyle);
            Native.SetWindowLongW(Handle, exStyle, enable ? style | transparent : style & ~transparent);
        }

        private void Tick()
        {
            double now = gestureClock.Elapsed.TotalSeconds;
            double dt = Math.Min(.05, motionClock.Elapsed.TotalSeconds);
            motionClock.Restart();
            PollPendingClick();
            if (bubbleUntil > 0 && now >= bubbleUntil) HideBubble();
            heart.Update(Bounds, pressed && dragging && !rayForm && !morphing, dt);

            if (morphing)
            {
                if (morphClock.Elapsed.TotalSeconds >= MorphDuration) CommitMorph();
                else Render();
                return;
            }

            if (pressed && !rayForm)
            {
                if (!dragging && hold.ElapsedMilliseconds >= 260) BeginDrag();
                if (dragging)
                {
                    Point waist = LiftMotion.Grip(side);
                    lift.Step(new Point(position.X + waist.X, position.Y + waist.Y), side, dt);
                }
            }

            if (!pressed && !menu.Visible && !pendingClick)
            {
                Rectangle work = Screen.FromRectangle(Bounds).WorkingArea;
                if (reading)
                {
                    Play(rayForm ? swim : idle);
                }
                else if (rayForm)
                {
                    targetPoll -= dt;
                    if (targetPoll <= 0)
                    {
                        client = FlightTarget.Read(ownPid);
                        targetPoll = .18;
                        if (!client.IsEmpty && !client.IntersectsWith(work))
                        {
                            Rectangle next = Screen.FromRectangle(client).WorkingArea;
                            Move(new Point(client.Left, client.Top), next);
                            motion.Reset(position);
                        }
                    }
                    motion.FlyStep(client, work, side, dt);
                }
                else
                {
                    motion.GroundStep(work, side, dt, current == touch);
                    if (current != touch)
                    {
                        AnimationClip desired = motion.Walking ? walk : idle;
                        if (desired != current) Play(desired);
                    }
                }
                Place(motion.Position);
            }
            Render();
        }

        private void Play(AnimationClip clip)
        {
            if (clip == null || (current == clip && playback.IsRunning)) return;
            current = clip;
            lastFrame = null;
            playback.Restart();
            Render();
        }

        private void BeginDrag()
        {
            if (pendingClick)
            {
                DispatchPendingClick();
                secondClickCandidate = false;
            }
            dragging = true;
            if (!rayForm)
            {
                lift.Begin(press);
                Play(lifted);
            }
            DragToPointer();
        }

        private void DragToPointer()
        {
            Point offset = rayForm ? new Point(press.X - start.X, press.Y - start.Y) : LiftMotion.Grip(side);
            Move(new Point(pointer.X - offset.X, pointer.Y - offset.Y), Screen.FromPoint(pointer).WorkingArea);
        }

        private void EndDrag()
        {
            motion.Reset(position);
            targetPoll = 0;
            if (!rayForm && current == lifted) Play(idle);
            SettingsChangedRaised();
        }

        private void Move(Point desired, Rectangle area)
        {
            Place(new Point(Math.Max(area.Left, Math.Min(desired.X, area.Right - side)),
                Math.Max(area.Top, Math.Min(desired.Y, area.Bottom - side))));
        }

        private void Place(Point desired)
        {
            if (position == desired) return;
            position = desired;
            Native.SetWindowPos(Handle, new IntPtr(-1), position.X, position.Y, 0, 0, 0x0011);
            if (bubbleUntil > 0) bubble.RepositionTo(Bounds);
        }

        private void CancelPress()
        {
            hold.Reset();
            if (pressed) Native.ReleaseCapture();
            pressed = false;
            dragging = false;
            secondClickCandidate = false;
        }

        private void DiscardPendingClick()
        {
            pendingClick = false;
            secondClickCandidate = false;
        }

        private void QueueClick(Point cursor, PetPart part)
        {
            pendingClick = true;
            pendingTime = gestureClock.Elapsed.TotalSeconds;
            pendingPoint = cursor;
            pendingPart = part;
        }

        private bool SameDoubleClickArea(Point cursor)
        {
            Size size = SystemInformation.DoubleClickSize;
            var area = new Rectangle(pendingPoint.X - size.Width / 2, pendingPoint.Y - size.Height / 2,
                size.Width, size.Height);
            return area.Contains(cursor);
        }

        private void PollPendingClick()
        {
            if (!pendingClick || pressed || secondClickCandidate) return;
            double age = gestureClock.Elapsed.TotalSeconds - pendingTime;
            if (age * 1000.0 >= SystemInformation.DoubleClickTime) DispatchPendingClick();
        }

        private void DispatchPendingClick()
        {
            if (!pendingClick) return;
            PetPart part = pendingPart;
            Point cursor = pendingPoint;
            pendingClick = false;
            secondClickCandidate = false;
            React(part, cursor);
        }

        private void React(PetPart part, Point cursor)
        {
            string key;
            switch (part)
            {
                case PetPart.HumanHead: key = "human.head"; break;
                case PetPart.HumanHand: key = "human.hand"; break;
                case PetPart.HumanBody: key = "human.body"; break;
                case PetPart.HumanManuscript: key = "human.manuscript"; break;
                case PetPart.RayHead: key = "ray.head"; break;
                default: key = "ray.body"; break;
            }
            ShowBubble(content.NextBubble(key));
            if (rayForm)
            {
                reactionSign = cursor.X < position.X + side / 2 ? -1 : 1;
                reactionClock.Restart();
            }
            else
            {
                motion.StopWalking();
                Play(touch);
            }
            if (part == PetPart.HumanManuscript)
            {
                HideBubble();
                reading = true;
                if (JournalRequested != null)
                    JournalRequested(this, new JournalRequestEventArgs(true));
            }
        }

        private PetPart HitTest(Point cursor)
        {
            if (rayForm)
            {
                double y = (cursor.Y - position.Y) * 192.0 / side;
                return y < 132 ? PetPart.RayHead : PetPart.RayBody;
            }
            double x = (cursor.X - position.X) * 288.0 / side;
            double hy = (cursor.Y - position.Y) * 288.0 / side;
            if (x >= 176 && hy >= 90 && hy <= 205) return PetPart.HumanManuscript;
            if (hy < 100) return PetPart.HumanHead;
            if (hy >= 90 && hy <= 205 && (x < 112 || x > 174)) return PetPart.HumanHand;
            return PetPart.HumanBody;
        }

        private void BeginMorph(bool targetRay)
        {
            if (morphing) return;
            DiscardPendingClick();
            CancelPress();
            HideBubble();
            morphing = true;
            morphTargetRay = targetRay;
            morphClock.Restart();
            Render();
        }

        private void CommitMorph()
        {
            morphing = false;
            rayForm = morphTargetRay;
            motion.Reset(position);
            targetPoll = 0;
            Play(rayForm ? swim : idle);
            SettingsChangedRaised();
        }

        private void Render()
        {
            if (!Visible) return;
            if (morphing)
            {
                RenderMorph();
                return;
            }
            if (current == touch && playback.ElapsedMilliseconds >= touch.Duration)
            {
                current = rayForm ? swim : idle;
                playback.Restart();
            }
            bool carried = current == lifted;
            long time = playback.ElapsedMilliseconds;
            Bitmap sprite = current.FrameAt(carried ? LiftMotion.ClipTime(time) : time);
            int pose = 0;
            if (rayForm) pose = (int)Math.Round(motion.Bank * 24);
            else if (carried) pose = lift.Angle;
            else if (current == walk && motion.FacingLeft) pose = 1;
            int stretch = carried ? lift.Stretch : 0;
            int reaction = ReactionValue();
            bool squash = pressed && !dragging && !rayForm;
            if (sprite == lastFrame && pose == lastPose && stretch == lastStretch &&
                reaction == lastReaction && squash == lastPressed) return;

            using (var frame = new Bitmap(side, side, PixelFormat.Format32bppPArgb))
            {
                using (var graphics = Graphics.FromImage(frame))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.ScaleTransform(side / 192f, side / 192f);
                    if (squash) ApplySquash(graphics, 192);
                    if (rayForm)
                    {
                        double bank = pose / 24.0 + reaction / 1000.0;
                        RayPose.Draw(graphics, sprite, 192, bank);
                    }
                    else if (carried)
                    {
                        lift.Draw(graphics, sprite, 192);
                    }
                    else
                    {
                        if (pose == 1)
                        {
                            graphics.TranslateTransform(192, 0);
                            graphics.ScaleTransform(-1, 1);
                        }
                        graphics.DrawImage(sprite, new Rectangle(0, 0, 192, 192));
                    }
                }
                Native.Paint(Handle, position, frame);
                lastFrame = sprite;
                lastPose = pose;
                lastStretch = stretch;
                lastReaction = reaction;
                lastPressed = squash;
            }
        }

        private int ReactionValue()
        {
            if (!rayForm || !reactionClock.IsRunning) return 0;
            double t = reactionClock.Elapsed.TotalSeconds / .75;
            if (t >= 1)
            {
                reactionClock.Reset();
                return 0;
            }
            return (int)Math.Round(Math.Sin(t * Math.PI) * reactionSign * 210);
        }

        private static void ApplySquash(Graphics graphics, int size)
        {
            graphics.TranslateTransform(size / 2f, size);
            graphics.ScaleTransform(1.012f, .984f);
            graphics.TranslateTransform(-size / 2f, -size);
        }

        private void RenderMorph()
        {
            double raw = Math.Min(1, morphClock.Elapsed.TotalSeconds / MorphDuration);
            double t = raw * raw * (3 - 2 * raw);
            Bitmap source = current == null ? idle.FrameAt(0) : current.FrameAt(playback.ElapsedMilliseconds);
            Bitmap destination = (morphTargetRay ? swim : idle).FrameAt((int)(morphClock.ElapsedMilliseconds % 1000));
            using (var frame = new Bitmap(side, side, PixelFormat.Format32bppPArgb))
            using (var graphics = Graphics.FromImage(frame))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.ScaleTransform(side / 192f, side / 192f);
                DrawMorphForm(graphics, source, rayForm, 1 - t, 1 - .035 * t);
                DrawMorphForm(graphics, destination, morphTargetRay, t, .965 + .035 * t);
                double radius = 13 + 73 * t;
                int alpha = (int)Math.Round(150 + (0 - 150) * t);
                using (var ripple = new Pen(Color.FromArgb(alpha, 70, 175, 196), 2.4f))
                {
                    graphics.DrawEllipse(ripple, (float)(96 - radius), (float)(96 - radius * .55),
                        (float)(radius * 2), (float)(radius * 1.1));
                    graphics.DrawEllipse(ripple, (float)(96 - radius * .72), (float)(96 - radius * .4),
                        (float)(radius * 1.44), (float)(radius * .8));
                }
                Native.Paint(Handle, position, frame);
            }
        }

        private static void DrawMorphForm(Graphics graphics, Bitmap sprite, bool ray, double alpha, double scale)
        {
            using (var layer = new Bitmap(192, 192, PixelFormat.Format32bppPArgb))
            {
                using (var layerGraphics = Graphics.FromImage(layer))
                {
                    layerGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                    layerGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    if (ray) RayPose.Draw(layerGraphics, sprite, 192, 0);
                    else layerGraphics.DrawImage(sprite, new Rectangle(0, 0, 192, 192));
                }
                var state = graphics.Save();
                graphics.TranslateTransform(96, 96);
                graphics.ScaleTransform((float)scale, (float)scale);
                graphics.TranslateTransform(-96, -96);
                var matrix = new ColorMatrix { Matrix33 = (float)alpha };
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    graphics.DrawImage(layer, new Rectangle(0, 0, 192, 192), 0, 0, 192, 192,
                        GraphicsUnit.Pixel, attributes);
                }
                graphics.Restore(state);
            }
        }

        protected override void WndProc(ref Message m)
        {
            Point cursor;
            switch (m.Msg)
            {
                case 0x02b1: // WM_WTSSESSION_CHANGE
                    if (m.WParam.ToInt32() == 7) locked = true;
                    if (m.WParam.ToInt32() == 8) locked = false;
                    if (AvailabilityChanged != null) AvailabilityChanged();
                    break;
                case 0x0218: // WM_POWERBROADCAST
                    if (m.WParam.ToInt32() == 4) suspended = true;
                    if (m.WParam.ToInt32() == 7 || m.WParam.ToInt32() == 18) suspended = false;
                    if (AvailabilityChanged != null) AvailabilityChanged();
                    break;
                case 0x21:
                    m.Result = new IntPtr(3);
                    return;
                case 0x201:
                case 0x203:
                    if (morphing) return;
                    cursor = PointerPosition(m.LParam);
                    if (pendingClick)
                    {
                        double age = gestureClock.Elapsed.TotalSeconds - pendingTime;
                        if (age * 1000.0 < SystemInformation.DoubleClickTime && SameDoubleClickArea(cursor))
                            secondClickCandidate = true;
                        else DispatchPendingClick();
                    }
                    pressed = true;
                    dragging = false;
                    pointer = press = cursor;
                    start = position;
                    hold.Restart();
                    Native.SetCapture(Handle);
                    Render();
                    return;
                case 0x200:
                    if (!pressed) break;
                    cursor = PointerPosition(m.LParam);
                    pointer = cursor;
                    Size threshold = SystemInformation.DragSize;
                    if (!dragging && !new Rectangle(press.X - threshold.Width / 2,
                        press.Y - threshold.Height / 2, threshold.Width, threshold.Height).Contains(cursor))
                        BeginDrag();
                    else if (dragging) DragToPointer();
                    return;
                case 0x202:
                    if (!pressed) return;
                    bool wasSecond = secondClickCandidate;
                    bool wasDragging = dragging;
                    cursor = PointerPosition(m.LParam);
                    CancelPress();
                    if (wasDragging)
                    {
                        EndDrag();
                    }
                    else if (wasSecond)
                    {
                        pendingClick = false;
                        BeginMorph(!rayForm);
                    }
                    else
                    {
                        QueueClick(cursor, HitTest(cursor));
                    }
                    return;
                case 0x215:
                    CancelPress();
                    motion.Reset(position);
                    targetPoll = 0;
                    if (!rayForm && (current == walk || current == lifted)) Play(idle);
                    return;
                case 0x1f:
                    CancelPress();
                    return;
                case 0x205:
                    DiscardPendingClick();
                    HideBubble();
                    menu.Show(Cursor.Position);
                    return;
                case 0x8001: // automation hook: close the shared menu without changing form
                    if (testHooks) { menu.Close(); return; }
                    break;
                case 0x8002: // automation hook: inspect controller state
                    if (testHooks)
                    {
                        int state = (rayForm ? 1 : 0) | (reading ? 2 : 0) | (morphing ? 4 : 0) |
                            (menu.Visible ? 8 : 0) | (pressed ? 16 : 0) | (!client.IsEmpty ? 32 : 0) |
                            (animation.Enabled ? 64 : 0) | (current == swim ? 128 : 0) |
                            (pendingClick ? 256 : 0) | (secondClickCandidate ? 512 : 0);
                        m.Result = new IntPtr(state);
                        return;
                    }
                    break;
                case 0x8003: // automation hook: invoke the first menu command
                    if (testHooks) { menu.Close(); Swap(); return; }
                    break;
                case 0x8004: // automation hook: open the journal through the real view event
                    if (testHooks) { ReadJournal(); return; }
                    break;
                case 0x10:
                    if (CloseRequested != null) CloseRequested(this, EventArgs.Empty);
                    return;
                case 0x02e0:
                    side = (int)Math.Round(192 * (m.WParam.ToInt64() & 65535) / 96.0);
                    lastFrame = null;
                    HideBubble();
                    if (Visible)
                    {
                        Move(position, Screen.FromPoint(position).WorkingArea);
                        motion.Reset(position);
                        targetPoll = 0;
                        Render();
                    }
                    return;
                case 0x007e:
                    HideBubble();
                    if (Visible)
                    {
                        Move(position, Screen.FromPoint(position).WorkingArea);
                        motion.Reset(position);
                        targetPoll = 0;
                        Render();
                    }
                    return;
            }
            base.WndProc(ref m);
        }

        private Point PointerPosition(IntPtr coordinates)
        {
            long packed = coordinates.ToInt64();
            return new Point(position.X + unchecked((short)packed),
                position.Y + unchecked((short)(packed >> 16)));
        }

        private static bool IsPositionUsable(Point point)
        {
            foreach (Screen screen in Screen.AllScreens)
                if (screen.WorkingArea.IntersectsWith(new Rectangle(point, new Size(32, 32)))) return true;
            return false;
        }

        private void SettingsChangedRaised()
        {
            if (SettingsChanged != null) SettingsChanged(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            DiscardPendingClick();
            Hide();
            animation.Dispose();
            bubble.Dispose();
            heart.Dispose();
            WTSUnRegisterSessionNotification(Handle);
            DestroyHandle();
            idle.Dispose();
            touch.Dispose();
            walk.Dispose();
            lifted.Dispose();
            swim.Dispose();
        }
    }
}
