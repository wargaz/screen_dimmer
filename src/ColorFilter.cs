using System;
using System.Runtime.InteropServices;

namespace ScreenDimmer
{
    // Applies a color matrix to the whole screen through the Windows Magnification API.
    // Dimming and red tint are combined into one diagonal matrix: R' = R*b, G' = G*b*g, B' = B*b*bl.
    static class ColorFilter
    {
        [StructLayout(LayoutKind.Sequential)]
        struct MAGCOLOREFFECT
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
            public float[] transform;
        }

        [DllImport("Magnification.dll", SetLastError = true)]
        static extern bool MagInitialize();

        [DllImport("Magnification.dll", SetLastError = true)]
        static extern bool MagUninitialize();

        [DllImport("Magnification.dll", SetLastError = true)]
        static extern bool MagSetFullscreenColorEffect(ref MAGCOLOREFFECT pEffect);

        static bool initialized;
        static double lastBrightness = 1.0;
        static double lastRed = 0.0;

        static bool EnsureInitialized()
        {
            if (!initialized) initialized = MagInitialize();
            return initialized;
        }

        /// <param name="brightness">0..1, where 1 leaves the screen unchanged.</param>
        /// <param name="red">0..1, where 0 is no tint and 1 is fully red.</param>
        public static bool Apply(double brightness, double red)
        {
            lastBrightness = Clamp01(brightness);
            lastRed = Clamp01(red);
            if (!EnsureInitialized()) return false;

            // Blue fades out linearly, green more slowly. Values in between give a warm
            // orange and the maximum is fully red.
            double green = 1.0 - lastRed * lastRed;
            double blue = 1.0 - lastRed;

            var effect = new MAGCOLOREFFECT { transform = new float[25] };
            effect.transform[0] = (float)lastBrightness;
            effect.transform[6] = (float)(lastBrightness * green);
            effect.transform[12] = (float)(lastBrightness * blue);
            effect.transform[18] = 1f;
            effect.transform[24] = 1f;
            return MagSetFullscreenColorEffect(ref effect);
        }

        public static bool Reapply()
        {
            return Apply(lastBrightness, lastRed);
        }

        public static void Shutdown()
        {
            if (!initialized) return;
            Apply(1.0, 0.0);
            MagUninitialize();
            initialized = false;
        }

        static double Clamp01(double v)
        {
            return v < 0 ? 0 : (v > 1 ? 1 : v);
        }
    }
}
