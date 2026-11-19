using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace ClementiaPet
{
    internal sealed class AnimationClip : IDisposable
    {
        private readonly Bitmap[] frames;
        private readonly int[] ends;
        internal int Duration { get { return ends[ends.Length - 1]; } }

        internal AnimationClip(string sheet, int size, int[] durations)
        {
            frames = new Bitmap[durations.Length];
            ends = new int[frames.Length];
            using (var stream = typeof(AnimationClip).Assembly.GetManifestResourceStream(sheet))
            using (var image = new Bitmap(stream))
            using (var decoded = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb))
            {
                using (var graphics = Graphics.FromImage(decoded))
                    graphics.DrawImageUnscaled(image, 0, 0);
                int cols = image.Width / size;
                for (int i = 0; i < frames.Length; ++i)
                {
                    var tile = new Rectangle(i % cols * size, i / cols * size, size, size);
                    frames[i] = decoded.Clone(tile, PixelFormat.Format32bppPArgb);
                    ends[i] = durations[i] + (i == 0 ? 0 : ends[i - 1]);
                }
            }
        }

        internal static AnimationClip Human(string name, int count, int duration)
        {
            int[] delays = new int[count];
            for (int i = 0; i < count; ++i)
                delays[i] = (i + 1) * duration / count - i * duration / count;
            return new AnimationClip(name + ".png", 288, delays);
        }

        internal static AnimationClip Ray()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RayTiming.txt"))
            using (var reader = new StreamReader(stream))
                return new AnimationClip("RayMotion.png", 256, Array.ConvertAll(reader.ReadToEnd().Trim().Split(','), int.Parse));
        }

        internal Bitmap FrameAt(long elapsed)
        {
            int index = Array.BinarySearch(ends, (int)(elapsed % Duration) + 1);
            return frames[index < 0 ? ~index : index];
        }

        public void Dispose() { foreach (Bitmap frame in frames) frame.Dispose(); }
    }
}
