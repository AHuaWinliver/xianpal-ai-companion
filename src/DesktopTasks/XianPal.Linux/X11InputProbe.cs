// SPDX-License-Identifier: Apache-2.0
using System.Runtime.InteropServices;
using Avalonia;

namespace XianPal.Linux;

// Validation only. CI explicitly opts in while running its isolated Xvfb server.
// ABI: https://www.x.org/docs/Xext/xtestlib.pdf
internal sealed class X11InputProbe : IDisposable
{
    [DllImport("libX11.so.6")] private static extern IntPtr XOpenDisplay(IntPtr name);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(IntPtr display);
    [DllImport("libX11.so.6")] private static extern int XSync(IntPtr display, bool discard);
    [DllImport("libXtst.so.6")] private static extern int XTestQueryExtension(IntPtr display, out int events, out int errors, out int major, out int minor);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeMotionEvent(IntPtr display, int screen, int x, int y, nuint delay);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeButtonEvent(IntPtr display, uint button, int pressed, nuint delay);
    private IntPtr display;

    internal X11InputProbe()
    {
        if (Environment.GetEnvironmentVariable("FUFU_TEST_MODE") != "1" || Environment.GetEnvironmentVariable("XIANPAL_CI_INPUT") != "1")
            throw new InvalidOperationException("Native input validation requires the explicit CI opt-in.");
        display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero) throw new InvalidOperationException("Native input validation has no X11 display.");
        if (XTestQueryExtension(display, out _, out _, out _, out _) == 0) { Dispose(); throw new InvalidOperationException("XTEST extension is unavailable."); }
    }

    internal void Move(PixelPoint point)
    {
        if (XTestFakeMotionEvent(display, -1, point.X, point.Y, 0) == 0) throw new IOException("XTEST pointer movement failed.");
        XSync(display, false);
    }

    internal void Button(uint button, bool down)
    {
        if (XTestFakeButtonEvent(display, button, down ? 1 : 0, 0) == 0) throw new IOException("XTEST button event failed.");
        XSync(display, false);
    }

    public void Dispose()
    {
        if (display == IntPtr.Zero) return;
        XTestFakeButtonEvent(display, 1, 0, 0); XTestFakeButtonEvent(display, 3, 0, 0); XSync(display, false);
        XCloseDisplay(display); display = IntPtr.Zero;
    }
}
