using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ClementiaPet
{
    internal static class RayPose
    {
        // Bank the head into the direction of travel, flatten the belly and keep
        // the original asymmetric hair and fins on their correct sides.
        internal static void Draw(Graphics g, Bitmap source, int size, double bank)
        {
            var state = g.Save();
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TranslateTransform(size / 2f, size / 2f);
            g.RotateTransform((float)(bank * 53));
            g.ScaleTransform((float)(1 - .12 * Math.Abs(bank)), (float)(1 - .29 * Math.Abs(bank)));
            g.DrawImage(source, new Rectangle(-size/2, -size/2, size, size));
            g.Restore(state);
        }
    }
}
