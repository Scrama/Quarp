using OpenTK;

namespace Quarp.Extensions
{
    internal static class VidExtensions
    {
        public static bool Is(this mode_t mode, DisplayResolution res)
        {
            return mode.width == res.Width
                   && mode.height == res.Height
                   && mode.bpp == res.BitsPerPixel;
        }
    }
}
