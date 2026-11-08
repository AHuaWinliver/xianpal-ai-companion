// SPDX-License-Identifier: Apache-2.0
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace XianPal.Linux;

internal static class X11Overlay
{
    [DllImport("libX11.so.6")] private static extern IntPtr XOpenDisplay(IntPtr name);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(IntPtr display);
    [DllImport("libX11.so.6")] private static extern nuint XDefaultRootWindow(IntPtr display);
    [DllImport("libX11.so.6")] private static extern int XDefaultScreen(IntPtr display);
    [DllImport("libX11.so.6")] private static extern nuint XGetSelectionOwner(IntPtr display, nuint selection);
    [DllImport("libX11.so.6")] private static extern nuint XInternAtom(IntPtr display, string name, bool onlyIfExists);
    [DllImport("libX11.so.6")] private static extern int XSendEvent(IntPtr display, nuint window, bool propagate, nint mask, ref Message message);
    [DllImport("libX11.so.6")] private static extern int XSync(IntPtr display, bool discard);
    [DllImport("libX11.so.6")] private static extern int XFree(IntPtr pointer);
    [DllImport("libXfixes.so.3")] private static extern int XFixesQueryVersion(IntPtr display, ref int major, ref int minor);
    [DllImport("libXfixes.so.3")] private static extern nuint XFixesCreateRegion(IntPtr display, IntPtr rectangles, int count);
    [DllImport("libXfixes.so.3")] private static extern void XFixesSetWindowShapeRegion(IntPtr display, nuint window, int kind, int x, int y, nuint region);
    [DllImport("libXfixes.so.3")] private static extern void XFixesDestroyRegion(IntPtr display, nuint region);
    [DllImport("libXext.so.6")] private static extern IntPtr XShapeGetRectangles(IntPtr display, nuint window, int kind, out int count, out int ordering);
    [DllImport("libXss.so.1")] private static extern int XScreenSaverQueryInfo(IntPtr display, nuint window, ref SaverInfo info);
    [StructLayout(LayoutKind.Sequential)] private struct SaverInfo
    {
        internal nuint Window;
        internal int State, Kind;
        internal nuint Time, Idle, EventMask;
    }
    private static bool screenQueryAvailable = true;

    internal static bool HasCompositor()
    {
        IntPtr display = IntPtr.Zero;
        try
        {
            display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero) return false;
            nuint selection = XInternAtom(display, "_NET_WM_CM_S" + XDefaultScreen(display), true);
            return selection != 0 && XGetSelectionOwner(display, selection) != 0;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) { return false; }
        finally { if (display != IntPtr.Zero) XCloseDisplay(display); }
    }

    internal static bool? ScreenActive()
    {
        if (!screenQueryAvailable) return null;
        IntPtr display = IntPtr.Zero;
        try
        {
            display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero) return null;
            var info = new SaverInfo();
            return XScreenSaverQueryInfo(display, XDefaultRootWindow(display), ref info) == 0 ? null : info.State != 1;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        { screenQueryAvailable = false; return null; }
        finally { if (display != IntPtr.Zero) XCloseDisplay(display); }
    }

    // XEvent / XClientMessageEvent layout for the supported LP64 x64/ARM64 builds.
    [StructLayout(LayoutKind.Explicit, Size = 192)] private struct Message
    {
        [FieldOffset(0)] internal int Type;
        [FieldOffset(16)] internal int SendEvent;
        [FieldOffset(24)] internal IntPtr Display;
        [FieldOffset(32)] internal nuint Window;
        [FieldOffset(40)] internal nuint MessageType;
        [FieldOffset(48)] internal int Format;
        [FieldOffset(56)] internal nint Source;
        [FieldOffset(64)] internal nint Sibling;
        [FieldOffset(72)] internal nint Detail;
    }

    internal static bool MakeClickThrough(Window overlay, Window companion)
    {
        var handle = overlay.TryGetPlatformHandle();
        var pet = companion.TryGetPlatformHandle();
        if (handle?.HandleDescriptor != "XID" || pet?.HandleDescriptor != "XID") return false;
        IntPtr display = IntPtr.Zero;
        try
        {
            display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero) return false;
            int major = 5, minor = 0;
            if (XFixesQueryVersion(display, ref major, ref minor) == 0 || major < 2) return false;
            nuint region = XFixesCreateRegion(display, IntPtr.Zero, 0);
            try { XFixesSetWindowShapeRegion(display, (nuint)handle.Handle, 2, 0, 0, region); }
            finally { XFixesDestroyRegion(display, region); }
            // Ask the window manager to stack the decoration below the pet,
            // without activating either window or changing keyboard focus.
            var message = new Message { Type = 33, SendEvent = 1, Display = display, Window = (nuint)handle.Handle,
                MessageType = XInternAtom(display, "_NET_RESTACK_WINDOW", false), Format = 32, Source = 2, Sibling = (nint)pet.Handle, Detail = 1 };
            XSendEvent(display, XDefaultRootWindow(display), false, (1 << 20) | (1 << 19), ref message);
            XSync(display, false);
            IntPtr rectangles = XShapeGetRectangles(display, (nuint)handle.Handle, 2, out int count, out _);
            if (rectangles != IntPtr.Zero) XFree(rectangles);
            return count == 0;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        { Console.Error.WriteLine("X11 overlay unavailable: " + error.Message); return false; }
        finally { if (display != IntPtr.Zero) XCloseDisplay(display); }
    }
}
