// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class InstallerConfigurationTests
{
    private static void Check(bool value, string text)
    { if (!value) throw new Exception(text); Console.WriteLine("PASS " + text); }
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            if (Environment.GetEnvironmentVariable("FUFU_TEST_MODE") != "1") throw new Exception("Test isolation required.");
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var program = assembly.GetType("XianPal.Installer.Program", true);
            var select = program.GetMethod("SelectInstallDirectory", BindingFlags.Static | BindingFlags.NonPublic);
            var location = program.GetField("InstallDirectory", BindingFlags.Static | BindingFlags.NonPublic);
            string root = Environment.GetEnvironmentVariable("FUFU_STATE_DIR");
            string custom = Path.Combine(root, "仙伴 custom folder");
            select.Invoke(null, new object[] { custom });
            Check((string)location.GetValue(null) == custom, "custom Unicode installation directory is selected");
            string unrelated = Path.Combine(root, "unrelated"); Directory.CreateDirectory(unrelated);
            string userFile = Path.Combine(unrelated, "user.txt"); File.WriteAllText(userFile, "keep");
            foreach (string invalid in new[] { unrelated, Path.GetPathRoot(custom), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) })
            {
                bool rejected = false;
                try { select.Invoke(null, new object[] { invalid }); }
                catch (TargetInvocationException error) { rejected = error.InnerException is InvalidOperationException; }
                Check(rejected && (string)location.GetValue(null) == custom && File.ReadAllText(userFile) == "keep", "invalid installation folder is rejected without changing the previous selection or user files");
            }
            var locale = assembly.GetType("XianPal.UiLanguage", true).GetMethod("Set", BindingFlags.Static | BindingFlags.NonPublic);
            locale.Invoke(null, new object[] { "en" });
            using (var window = (Form)Activator.CreateInstance(assembly.GetType("XianPal.Installer.InstallerForm", true), true))
            {
                var destination = window.Controls.Find("InstallDirectory", true)[0] as TextBox;
                Check(destination != null && destination.Text == custom, "installer shows an editable custom destination");
                Check(window.Controls.Find("Install", true)[0].Text == "Install", "installer opens in the selected English language");
                object preview = FindPreview(window);
                var clips = (IList)preview.GetType().GetField("clips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview);
                Check(clips.Count == 4, "installer contains four feature preview clips");
                foreach (Array clip in clips) Check(clip.Length == 24, "preview uses the complete 24-frame original artwork sequence");
                window.Location = new Point(SystemInformation.VirtualScreen.Right + 100, 0);
                // Use a public illustrative path in screenshot evidence.
                destination.Text = @"C:\Apps\XianPal";
                window.Show(); Application.DoEvents();
                using (var capture = new Bitmap(window.Width, window.Height))
                { window.DrawToBitmap(capture, new Rectangle(Point.Empty, capture.Size)); capture.Save(Path.Combine(root, "installer-en.png")); }
                destination.Text = custom;
                window.Close();
            }
            Console.WriteLine("PASS installer configuration and embedded previews; installation was not started.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static Control FindPreview(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child.GetType().Name == "InstallerPreview") return child;
            var found = FindPreview(child); if (found != null) return found;
        }
        return null;
    }
}
