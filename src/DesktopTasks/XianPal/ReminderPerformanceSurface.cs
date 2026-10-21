// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace XianPal
{
    // Per-pixel alpha, rather than a magenta colour key: glow and soft sprite edges
    // remain translucent. A persistent DIB avoids allocating/copying a full-screen
    // HBITMAP on every frame. The surface is owned, non-activating and click-through.
    internal sealed class ReminderPerformanceSurface : Form
    {
        private IntPtr screenDc, memoryDc, dib, previous;
        private Bitmap pixels;
        private Graphics canvas;
        internal bool LastPushSucceeded { get; private set; }

        [StructLayout(LayoutKind.Sequential)] private struct NativePoint
        {
            internal int X, Y;
            internal NativePoint(int x, int y) { X = x; Y = y; }
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativeSize { internal int Width, Height; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct Blend { internal byte Operation, Flags, Alpha, Format; }
        [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
        {
            internal uint Size;
            internal int Width, Height;
            internal ushort Planes, Bits;
            internal uint Compression, ImageSize;
            internal int XPixels, YPixels;
            internal uint Colors, Important;
        }
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destDc, ref NativePoint dest, ref NativeSize size, IntPtr sourceDc, ref NativePoint source, int key, ref Blend blend, int flags);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);

        internal ReminderPerformanceSurface(Rectangle bounds)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Bounds = bounds;
            ShowInTaskbar = false;
            TopMost = true;
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00080000 | 0x00000020 | 0x00000080 | 0x08000000;
                return cp;
            }
        }

        internal bool Present(Action<Graphics> draw)
        {
            if (IsDisposed || ClientSize.Width < 1 || ClientSize.Height < 1) return false;
            EnsureBuffer();
            canvas.ResetClip();
            canvas.ResetTransform();
            canvas.Clear(Color.Transparent);
            draw(canvas);
            canvas.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);
            NativePoint dest = new NativePoint(Left, Top), source = new NativePoint(0, 0);
            NativeSize size = new NativeSize { Width = pixels.Width, Height = pixels.Height };
            Blend blend = new Blend { Alpha = 255, Format = 1 };
            LastPushSucceeded = UpdateLayeredWindow(Handle, screenDc, ref dest, ref size, memoryDc, ref source, 0, ref blend, 2);
            return LastPushSucceeded;
        }

        private void EnsureBuffer()
        {
            if (pixels != null && pixels.Size == ClientSize) return;
            ReleaseBuffer();
            screenDc = GetDC(IntPtr.Zero);
            memoryDc = CreateCompatibleDC(screenDc);
            BitmapInfo info = new BitmapInfo { Size = 40, Width = ClientSize.Width, Height = -ClientSize.Height, Planes = 1, Bits = 32 };
            IntPtr bits;
            dib = CreateDIBSection(memoryDc, ref info, 0, out bits, IntPtr.Zero, 0);
            if (memoryDc == IntPtr.Zero || dib == IntPtr.Zero || bits == IntPtr.Zero)
            {
                ReleaseBuffer();
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "无法创建提醒动画透明画布。");
            }
            previous = SelectObject(memoryDc, dib);
            pixels = new Bitmap(ClientSize.Width, ClientSize.Height, ClientSize.Width * 4, PixelFormat.Format32bppPArgb, bits);
            canvas = Graphics.FromImage(pixels);
        }

        private void ReleaseBuffer()
        {
            if (canvas != null) { canvas.Dispose(); canvas = null; }
            if (pixels != null) { pixels.Dispose(); pixels = null; }
            if (previous != IntPtr.Zero && memoryDc != IntPtr.Zero) SelectObject(memoryDc, previous);
            if (dib != IntPtr.Zero) DeleteObject(dib);
            if (memoryDc != IntPtr.Zero) DeleteDC(memoryDc);
            if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
            previous = dib = memoryDc = screenDc = IntPtr.Zero;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ReleaseBuffer();
            base.Dispose(disposing);
        }
    }
}
