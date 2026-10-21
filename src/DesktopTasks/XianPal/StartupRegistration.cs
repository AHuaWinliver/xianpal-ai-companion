// SPDX-License-Identifier: Apache-2.0
using Microsoft.Win32;
using System;
using System.IO;
using System.Security;

namespace XianPal
{
    // Linked into both the app and installer: one user-facing startup identity.
    internal static class StartupRegistration
    {
        internal const string DisplayName = "仙伴 · AI桌面伴侣";
        internal const string LegacyName = "OrbitPet";
        private const string RunPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string ApprovalPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run";

        internal static bool IsEnabled()
        {
            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunPath))
                return HasEntry(run);
        }

        internal static bool HasEntry(RegistryKey run)
        {
            return run != null && (run.GetValue(DisplayName) != null || run.GetValue(LegacyName) != null);
        }

        internal static void SetEnabled(bool enabled, string executable)
        {
            using (RegistryKey run = Registry.CurrentUser.CreateSubKey(RunPath))
            using (RegistryKey approval = Registry.CurrentUser.OpenSubKey(ApprovalPath, true))
                SetEnabled(run, approval, enabled, executable);
        }

        internal static void SetEnabled(RegistryKey run, RegistryKey approval, bool enabled, string executable)
        {
            if (enabled)
            {
                if (String.IsNullOrWhiteSpace(executable)) throw new ArgumentException("Executable path is required.");
                MigrateLegacy(run, approval);
                run.SetValue(DisplayName, "\"" + executable + "\"", RegistryValueKind.String);
            }
            else
            {
                run.DeleteValue(DisplayName, false);
                run.DeleteValue(LegacyName, false);
                if (approval != null)
                {
                    approval.DeleteValue(DisplayName, false);
                    approval.DeleteValue(LegacyName, false);
                }
            }
        }

        internal static void TryMigrateLegacy()
        {
            // Only normal application startup calls this. Do not opt a user in
            // when no Run entry exists, or let a restricted registry break UI startup.
            try
            {
                using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunPath, true))
                {
                    if (run == null || run.GetValue(LegacyName) == null) return;
                    using (RegistryKey approval = Registry.CurrentUser.OpenSubKey(ApprovalPath, true))
                        MigrateLegacy(run, approval);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (SecurityException) { }
            catch (IOException) { }
        }

        internal static void MigrateLegacy(RegistryKey run, RegistryKey approval)
        {
            if (run == null) return;
            object command = run.GetValue(LegacyName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (!(command is string)) return;
            if (run.GetValue(DisplayName) == null)
            {
                // Copy approval BEFORE exposing the renamed Run entry, preserving
                // Windows Task Manager's disabled state and opaque timestamp bytes.
                if (approval != null && approval.GetValue(DisplayName) == null)
                {
                    object state = approval.GetValue(LegacyName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (state != null) approval.SetValue(DisplayName, state, approval.GetValueKind(LegacyName));
                }
                run.SetValue(DisplayName, command, run.GetValueKind(LegacyName));
            }
            // An existing new-name entry wins. Migration is safe to repeat.
            run.DeleteValue(LegacyName, false);
            if (approval != null) approval.DeleteValue(LegacyName, false);
        }
    }
}
