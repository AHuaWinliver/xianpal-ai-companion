// SPDX-License-Identifier: Apache-2.0
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using XianPal;

internal static class StartupRegistrationTests
{
    private static void Require(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    private static void CheckMetadata(string path, bool installer, string version)
    {
        FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
        Require(info.FileVersion == version + ".0" && info.ProductVersion == version + ".0", "Compiled version disagrees with VERSION");
        Require(info.ProductName == StartupRegistration.DisplayName, "Product name disagrees with startup display name");
        Require(installer ? info.FileDescription == StartupRegistration.DisplayName + " 安装程序" : info.FileDescription == StartupRegistration.DisplayName,
            "Task Manager file description is not the software name");
        Assembly assembly = Assembly.LoadFrom(path);
        Require(assembly.GetName().Version.ToString() == version + ".0", "Assembly version disagrees with VERSION");
        Type program = assembly.GetType(installer ? "XianPal.Installer.Program" : "XianPal.Program", true);
        string field = installer ? "AppName" : "ProductName";
        Require((string)program.GetField(field, BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue() == StartupRegistration.DisplayName,
            "Window and startup product names differ");
        Require(assembly.GetType("XianPal.StartupRegistration", false) != null, "Shared startup implementation absent");
        if (installer) Require((string)program.GetField("Version", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue() == version,
            "Installer display version disagrees with VERSION");
    }

    private static void TestRegistry(RegistryKey root)
    {
        string name = StartupRegistration.DisplayName, legacy = StartupRegistration.LegacyName;
        string command = "\"C:\\Applications\\仙伴 测试\\OrbitPet.exe\" --existing-argument";
        byte[] disabled = { 3, 0, 0, 0, 10, 20, 30, 40, 50, 60, 70, 80 };
        byte[] enabled = { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        using (RegistryKey run = root.CreateSubKey("Run"))
        using (RegistryKey approval = root.CreateSubKey("Approved"))
        {
            run.SetValue("UnrelatedApp", "keep command");
            approval.SetValue("UnrelatedApp", enabled, RegistryValueKind.Binary);
            StartupRegistration.MigrateLegacy(run, approval);
            Require(!StartupRegistration.HasEntry(run), "Migration enabled startup without an old registration");

            foreach (byte[] state in new[] { enabled, disabled })
            {
                run.SetValue(legacy, command, RegistryValueKind.String);
                approval.SetValue(legacy, state, RegistryValueKind.Binary);
                StartupRegistration.MigrateLegacy(run, approval);
                StartupRegistration.MigrateLegacy(run, approval);
                Require((string)run.GetValue(name) == command && run.GetValue(legacy) == null, "Legacy command/arguments lost or duplicate Run entry remains");
                Require(((byte[])approval.GetValue(name)).SequenceEqual(state), "Windows enabled/disabled state or timestamp changed");
                Require(approval.GetValue(legacy) == null && StartupRegistration.HasEntry(run), "Approval duplicate or startup detection failed");
                StartupRegistration.SetEnabled(run, approval, false, null);
            }

            string expandable = "\"%LOCALAPPDATA%\\OrbitPet\\OrbitPet.exe\"";
            run.SetValue(legacy, expandable, RegistryValueKind.ExpandString);
            StartupRegistration.MigrateLegacy(run, null);
            Require(run.GetValueKind(name) == RegistryValueKind.ExpandString &&
                (string)run.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) == expandable,
                "Migration expanded or changed environment-variable commands");

            run.SetValue(name, "new authoritative command");
            run.SetValue(legacy, command);
            approval.SetValue(name, disabled, RegistryValueKind.Binary);
            approval.SetValue(legacy, enabled, RegistryValueKind.Binary);
            StartupRegistration.MigrateLegacy(run, approval);
            Require((string)run.GetValue(name) == "new authoritative command" && ((byte[])approval.GetValue(name)).SequenceEqual(disabled),
                "Migration overwrote the existing new-name registration or approval");
            Require(run.GetValue(legacy) == null && approval.GetValue(legacy) == null, "Duplicate legacy values not removed");

            StartupRegistration.SetEnabled(run, approval, false, null);
            run.SetValue(legacy, command);
            approval.SetValue(legacy, disabled, RegistryValueKind.Binary);
            string executable = "C:\\Applications\\仙伴 测试\\OrbitPet.exe";
            StartupRegistration.SetEnabled(run, approval, true, executable);
            Require((string)run.GetValue(name) == "\"" + executable + "\"" && run.GetValue(legacy) == null,
                "Enabling failed to use the new name and quoted executable");
            Require(((byte[])approval.GetValue(name)).SequenceEqual(disabled), "Re-registration discarded Windows disabled state");
            run.SetValue(legacy, command);
            approval.SetValue(legacy, enabled, RegistryValueKind.Binary);
            StartupRegistration.SetEnabled(run, approval, false, null);
            Require(!StartupRegistration.HasEntry(run) && approval.GetValue(name) == null && approval.GetValue(legacy) == null,
                "Disable/uninstall failed to remove both names");
            Require((string)run.GetValue("UnrelatedApp") == "keep command" && ((byte[])approval.GetValue("UnrelatedApp")).SequenceEqual(enabled),
                "Unrelated startup registration modified");
        }
    }

    private static int Main(string[] args)
    {
        // Never test against the actual Windows Run / StartupApproved keys.
        string testKey = "Software\\XianPal\\Tests\\StartupRegistration\\" + Guid.NewGuid().ToString("N");
        try
        {
            Require(args.Length == 3, "Compiled app and installer paths plus public version required");
            CheckMetadata(args[0], false, args[2]);
            CheckMetadata(args[1], true, args[2]);
            using (RegistryKey root = Registry.CurrentUser.CreateSubKey(testKey)) TestRegistry(root);
            Console.WriteLine("PASS compiled app/installer versions match VERSION; product names and Task Manager descriptions match software name");
            Console.WriteLine("PASS isolated startup migration: legacy/new names, enabled/disabled bytes, arguments, value kinds, idempotency, disable/uninstall and unrelated entries");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Registry.CurrentUser.DeleteSubKeyTree(testKey, false); }
    }
}
