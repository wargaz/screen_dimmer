using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenDimmer
{
    // The color filter does not reach the mouse pointer, which Windows normally draws on its own
    // hardware layer. Setting a gamma ramp on the screens seems to make Windows draw the pointer
    // together with the rest of the screen, so it gets dimmed and tinted too. The ramp itself is
    // the normal straight line and changes nothing on its own.
    static class Gamma
    {
        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr CreateDC(string driver, string device, string output, IntPtr initData);

        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern bool SetDeviceGammaRamp(IntPtr hdc, ushort[] ramp);

        public static void Apply()
        {
            var ramp = new ushort[3 * 256];
            for (int i = 0; i < 256; i++)
                ramp[i] = ramp[256 + i] = ramp[512 + i] = (ushort)(i * 257);

            foreach (var screen in Screen.AllScreens)
            {
                IntPtr hdc = CreateDC(null, screen.DeviceName, null, IntPtr.Zero);
                if (hdc == IntPtr.Zero) continue;
                SetDeviceGammaRamp(hdc, ramp);
                DeleteDC(hdc);
            }
        }
    }
}
