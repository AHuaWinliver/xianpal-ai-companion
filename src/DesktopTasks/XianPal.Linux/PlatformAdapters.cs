// SPDX-License-Identifier: Apache-2.0
using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Threading;

namespace XianPal
{
    internal static class UiText
    {
        private static readonly Regex AnsiEscape = new(@"\x1B\[[0-?]*[ -/]*[@-~]");

        internal static string Normalize(string value)
        {
            string clean = AnsiEscape.Replace((value ?? "").Replace("\uFEFF", "").Replace("\0", ""), "");
            try { return clean.Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { return clean; }
        }

        internal static string TailCodeUnits(string value, int maximumLength)
        {
            string clean = Normalize(value);
            if (clean.Length <= maximumLength) return clean;
            int start = clean.Length - Math.Max(1, maximumLength);
            if (start > 0 && char.IsLowSurrogate(clean[start]) && char.IsHighSurrogate(clean[start - 1])) start--;
            while (start < clean.Length && CharUnicodeInfo.GetUnicodeCategory(clean, start) == UnicodeCategory.NonSpacingMark) start++;
            return clean[start..];
        }
    }
}

namespace XianPal.Linux
{
    internal sealed class UiTimer : IDisposable
    {
        private readonly DispatcherTimer timer = new();
        internal event EventHandler Tick;
        internal int Interval { set { timer.Interval = TimeSpan.FromMilliseconds(value); } }
        internal UiTimer() { timer.Tick += OnTick; }
        private void OnTick(object sender, EventArgs args) { Tick?.Invoke(this, args); }
        internal void Start() => timer.Start();
        internal void Stop() => timer.Stop();
        public void Dispose()
        {
            timer.Stop();
            timer.Tick -= OnTick;
        }
    }

    internal static class NativePaths
    {
        [DllImport("libc")]
        private static extern IntPtr realpath([MarshalAs(UnmanagedType.LPUTF8Str)] string path, IntPtr buffer);
        [DllImport("libc")]
        private static extern void free(IntPtr pointer);

        internal static string ResolveDirectory(string path)
        {
            string absolute = Path.GetFullPath(path);
            if (!OperatingSystem.IsLinux()) return absolute;
            IntPtr pointer = realpath(absolute, IntPtr.Zero);
            if (pointer == IntPtr.Zero) return absolute;
            try { return Marshal.PtrToStringUTF8(pointer) ?? absolute; }
            finally { free(pointer); }
        }

        internal static string UserDirectory(string variable, params string[] defaultSegments)
        {
            string path = Environment.GetEnvironmentVariable(variable);
            if (Path.IsPathFullyQualified(path ?? "")) return Path.Combine(path, "xianpal");
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(Path.Combine(home, Path.Combine(defaultSegments)), "xianpal");
        }
    }
}
