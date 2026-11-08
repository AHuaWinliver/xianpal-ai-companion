// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace XianPal.Linux;

internal static class LinuxInstallerValidation
{
    internal static async Task Run(PetWindow pet, string evidence)
    {
        UiLanguage.Set("en");
        var window = new LinuxInstallerWindow();
        window.Show(pet);
        await Task.Delay(180);
        var destination = window.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "InstallDirectory");
        destination.Text = "/home/user/.local/opt/xianpal";
        DesktopValidation.Capture(window, "installer-en.png");
        window.Close();
        string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string fixture = Path.Combine(parent, "xianpal-install-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try
        {
            string target = Path.Combine(fixture, "安装 custom ' $literal");
            var result = await Task.Run(() => LinuxPackage.Install(AppContext.BaseDirectory, target));
            if (!LinuxPackage.Owns(target) || !File.Exists(Path.Combine(target, "installation.json")) || result.Warning.Length > 0)
                throw new Exception("Fresh Linux installation or user launcher registration failed: " + result.Warning);
            var info = new ProcessStartInfo("/bin/sh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            info.ArgumentList.Add(Path.Combine(target, "launch.sh")); info.ArgumentList.Add("--self-test");
            using (var check = Process.Start(info)!)
            {
                try { await check.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
                catch { if (!check.HasExited) check.Kill(entireProcessTree: true); throw; }
                if (check.ExitCode != 0) throw new Exception("Installed Linux application failed its self-test: " + await check.StandardError.ReadToEndAsync());
            }
            File.WriteAllText(Path.Combine(target, "user-sentinel.txt"), "retain in backup");
            var upgrade = await Task.Run(() => LinuxPackage.Install(AppContext.BaseDirectory, target));
            if (!LinuxPackage.Owns(target) || !LinuxPackage.Owns(upgrade.PreviousDirectory) || File.ReadAllText(Path.Combine(upgrade.PreviousDirectory, "user-sentinel.txt")) != "retain in backup")
                throw new Exception("Linux update did not retain the complete original installation.");
            string unrelated = Path.Combine(fixture, "unrelated"); Directory.CreateDirectory(unrelated);
            File.WriteAllText(Path.Combine(unrelated, "user.txt"), "keep");
            bool rejected = false;
            try { LinuxPackage.ValidateDestination(AppContext.BaseDirectory, unrelated); } catch (IOException) { rejected = true; }
            if (!rejected || File.ReadAllText(Path.Combine(unrelated, "user.txt")) != "keep") throw new Exception("An unrelated installation folder was modified.");
            File.WriteAllText(Path.Combine(evidence, "installer-results.txt"), "PASS native installer UI; custom Unicode/quoted destination; verified copied files; installed binary self-test; complete upgrade backup; unrelated folder protection; user-only desktop launcher.\n");
        }
        finally
        {
            string resolved = Path.GetFullPath(fixture);
            if (Path.GetDirectoryName(resolved) != parent || !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(resolved), "^xianpal-install-validation-[a-f0-9]{32}$") || new DirectoryInfo(resolved).LinkTarget != null)
                throw new IOException("Unsafe installation fixture cleanup path.");
            Directory.Delete(resolved, true);
            UiLanguage.Set("zh-CN");
        }
    }
}
