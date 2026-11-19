using System;
using System.Drawing;

internal static class MotionContractTests
{
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }

    private static Point Run(int seconds, Rectangle client, Rectangle work, Point start)
    {
        var motion = new ClementiaPet.PetMotion(new Random(7341));
        motion.Reset(start);
        int frames = seconds * 30;
        for (int i = 0; i < frames; ++i) motion.FlyStep(client, work, 192, 1.0 / 30.0);
        return motion.Position;
    }

    private static bool Inside(Point point, Rectangle client, int side)
    {
        const int margin = 12;
        return point.X >= client.Left + margin && point.X + side <= client.Right - margin &&
            point.Y >= client.Top + margin && point.Y + side <= client.Bottom - margin;
    }

    private static void RunAll()
    {
        var client = new Rectangle(1280, 440, 640, 600);
        var start = new Point(1676, 858);
        bool allInside = true;
        for (int i = 0; i < 1800; ++i)
        {
            var motion = new ClementiaPet.PetMotion(new Random(181));
            motion.Reset(start);
            for (int frame = 0; frame <= i; ++frame) motion.FlyStep(client, client, 192, .03);
            if (!Inside(motion.Position, client, 192)) allInside = false;
        }
        Check(allInside, "1800 trajectory samples stay in the safe area");

        var top = Run(40, client, client, start);
        Check(top.Y <= client.Top + 12 + (int)((client.Height - 192 - 24) * .45),
            "cruise settles into the upper flight band");

        var left = new Rectangle(-1800, 260, 900, 640);
        var negative = Run(20, left, left, new Point(-900, 620));
        Check(Inside(negative, left, 192), "negative monitor coordinates remain legal");

        var small = new Rectangle(0, 0, 300, 200);
        var narrow = Run(20, small, small, new Point(1648, 858));
        Console.WriteLine("Narrow fallback position: " + narrow);
        Check(narrow.X >= small.Left && narrow.Y >= small.Top &&
            narrow.X + 192 <= small.Right && narrow.Y + 192 <= small.Bottom,
            "narrow window docks at the work-area corner");

        var noTarget = Run(20, Rectangle.Empty, client, start);
        Check(Inside(noTarget, client, 192), "missing target docks inside the work area");

        var first = Run(17, client, client, start);
        var second = Run(17, client, client, start);
        Check(first == second, "fixed random seed produces deterministic trajectories");
    }

    private static int Main()
    {
        try
        {
            RunAll();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL " + ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }
}
