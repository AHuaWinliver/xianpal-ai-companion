using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("仙伴 · AI桌面伴侣 安装程序")]
[assembly: AssemblyProduct(XianPal.StartupRegistration.DisplayName)]
[assembly: AssemblyVersion("0.6.12.0")]
[assembly: AssemblyFileVersion("0.6.12.0")]

namespace XianPal.Installer
{
    internal static class Program
    {
        internal const string AppName = XianPal.StartupRegistration.DisplayName;
        internal const string Version = "0.6.12";
        internal static string InstallDirectory = ResolveInstallDirectory();
        internal static string PetExe { get { return Path.Combine(InstallDirectory, "OrbitPet.exe"); } }

        private static string ResolveInstallDirectory()
        {
            string testRoot = Environment.GetEnvironmentVariable("FUFU_INSTALL_ROOT");
            if (!String.IsNullOrWhiteSpace(testRoot)) return Path.GetFullPath(testRoot);
            string own = AppDomain.CurrentDomain.BaseDirectory;
            if (Array.Exists(Environment.GetCommandLineArgs(), delegate(string value) { return value == "--uninstall"; }) && File.Exists(Path.Combine(own, ".orbitpet-install"))) return own;
            using (var installed = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\OrbitPet"))
            {
                string previous = installed == null ? null : installed.GetValue("InstallLocation") as string;
                if (!String.IsNullOrWhiteSpace(previous) && File.Exists(Path.Combine(previous, ".orbitpet-install"))) return Path.GetFullPath(previous);
            }
            string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrbitPet");
            if (File.Exists(Path.Combine(legacy, ".orbitpet-install"))) return legacy;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XianPal");
        }

        internal static void SelectInstallDirectory(string value)
        {
            string previous = InstallDirectory;
            InstallDirectory = Path.GetFullPath(value);
            try { ValidateInstallLocation(false); }
            catch { InstallDirectory = previous; throw; }
        }

        private static bool TestMode
        {
            get { return String.Equals(Environment.GetEnvironmentVariable("FUFU_TEST_MODE"), "1", StringComparison.Ordinal); }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                for (int i = 0; i < args.Length; i++) if (args[i] == "--install-dir")
                {
                    if (i + 1 == args.Length) throw new ArgumentException("--install-dir requires a path.");
                    SelectInstallDirectory(args[++i]);
                }
            }
            catch (Exception error)
            {
                if (Array.Exists(args, delegate(string value) { return value == "--silent"; })) Console.Error.WriteLine(error.Message);
                else MessageBox.Show(error.Message, "XianPal Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Environment.ExitCode = 1; return;
            }
            if (Array.Exists(args, delegate(string value) { return value == "--uninstall"; }))
            {
                Application.EnableVisualStyles();
                Uninstall();
                return;
            }
            if (Array.Exists(args, delegate(string value) { return value == "--silent"; }))
            {
                try { Install(); Environment.ExitCode = 0; }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(TestMode ? ex.ToString() : ex.GetBaseException().Message);
                    Environment.ExitCode = 1;
                }
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }

        internal static void Install()
        {
            ValidateInstallLocation(false);
            Directory.CreateDirectory(InstallDirectory);
            StopPet();
            ExtractPayload();
            File.WriteAllText(Path.Combine(InstallDirectory, ".orbitpet-install"), "OrbitPet:3", new System.Text.UTF8Encoding(false));
            RegisterStartMenuShortcut();
            RegisterHooks();
            if (!TestMode)
            {
                RegisterStartup();
                RegisterUninstaller();
            }

            if (!TestMode)
                Process.Start(new ProcessStartInfo(PetExe) { UseShellExecute = true });
        }

        internal static bool HasClaudeCli()
        {
            string[] candidates = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "claude.cmd")
            };
            foreach (string candidate in candidates) if (File.Exists(candidate)) return true;
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string entry in path.Split(Path.PathSeparator))
            {
                string clean = entry.Trim().Trim('"');
                if (File.Exists(Path.Combine(clean, "claude.exe")) || File.Exists(Path.Combine(clean, "claude.cmd"))) return true;
            }
            return false;
        }

        private static void ExtractPayload()
        {
            string staging = Path.Combine(InstallDirectory, ".payload-" + Guid.NewGuid().ToString("N"));
            string assets = Path.Combine(InstallDirectory, "assets");
            string scenes = Path.Combine(InstallDirectory,"scenes");
            string backup = Path.Combine(InstallDirectory, "asset-backups", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            string backupAssets = Path.Combine(backup, "assets");
            string backupScenes=Path.Combine(backup,"scenes");
            string backupExe = Path.Combine(backup, "OrbitPet.exe");
            bool savedExe = false, movedOldAssets = false, promotedAssets = false;
            bool movedOldScenes=false,promotedScenes=false;
            Directory.CreateDirectory(staging);
            try
            {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream resource = assembly.GetManifestResourceStream("XianPalPayload.zip"))
            {
                if (resource == null) throw new InvalidOperationException("安装包缺少内嵌资源。");
                using (ZipArchive zip = new ZipArchive(resource, ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        string destination = Path.GetFullPath(Path.Combine(staging, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                        string root = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
                        if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("安装包包含无效路径。");
                        if (String.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        using (Stream input = entry.Open())
                        using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                            input.CopyTo(output);
                    }
                }
            }

            // Validate the clean payload before changing the current install.
            // Overlay extraction leaves old 08..23.png files behind when an
            // animation changes from 24 to 8 frames, so replace the whole tree.
            ValidatePayload(staging);
            if (File.Exists(PetExe))
            {
                Directory.CreateDirectory(backup);
                File.Copy(PetExe, backupExe);
                savedExe = true;
            }
            string stagedAssets = Path.Combine(staging, "assets");
            if (!AssetTreesMatch(stagedAssets, assets))
            {
                if (Directory.Exists(assets))
                {
                    Directory.CreateDirectory(backup);
                    Directory.Move(assets, backupAssets);
                    movedOldAssets = true;
                }
                Directory.Move(stagedAssets, assets);
                promotedAssets = true;
            }
            // Scene artwork is a separate resource tree, not part of assets/. Promote
            // it transactionally too; top-level file copying otherwise silently drops it.
            string stagedScenes=Path.Combine(staging,"scenes");
            if(!AssetTreesMatch(stagedScenes,scenes))
            {
                if(Directory.Exists(scenes))
                {
                    if((File.GetAttributes(scenes)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("场景目录不能是目录链接。");
                    Directory.CreateDirectory(backup);Directory.Move(scenes,backupScenes);movedOldScenes=true;
                }
                Directory.Move(stagedScenes,scenes);promotedScenes=true;
            }
            foreach (string file in Directory.GetFiles(staging, "*", SearchOption.TopDirectoryOnly))
                File.Copy(file, Path.Combine(InstallDirectory, Path.GetFileName(file)), true);
            ValidatePayload(InstallDirectory);
            string source = Assembly.GetExecutingAssembly().Location;
            File.Copy(source, Path.Combine(InstallDirectory, "Uninstall.exe"), true);
            }
            catch
            {
                if(promotedScenes&&Directory.Exists(scenes))Directory.Move(scenes,Path.Combine(staging,"failed-scenes"));
                if(movedOldScenes&&Directory.Exists(backupScenes))Directory.Move(backupScenes,scenes);
                // Only remove the newly promoted tree from the active path;
                // never delete the old assets. Keep their backup on success.
                if (promotedAssets && Directory.Exists(assets)) Directory.Move(assets, Path.Combine(staging, "failed-assets"));
                if (movedOldAssets && Directory.Exists(backupAssets)) Directory.Move(backupAssets, assets);
                if (savedExe) File.Copy(backupExe, PetExe, true);
                throw;
            }
            finally
            {
                string stagingFull = Path.GetFullPath(staging);
                string installRoot = Path.GetFullPath(InstallDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!stagingFull.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(stagingFull).StartsWith(".payload-", StringComparison.Ordinal))
                    throw new InvalidOperationException("临时目录路径无效，未执行清理。");
                try { if (Directory.Exists(stagingFull)) Directory.Delete(stagingFull, true); } catch { }
            }
        }

        private static void ValidatePayload(string directory)
        {
            string scene=Path.Combine(directory,"scenes","sect-hall-v2.png");
            if(!File.Exists(scene))throw new InvalidOperationException("安装资源缺少门派大堂背景图。");
            using(Image image=Image.FromFile(scene))
                if(image.Width<320||image.Height<200||image.Width>8192||image.Height>8192)throw new InvalidOperationException("门派大堂背景图校验失败。");
            using (Process test = Process.Start(new ProcessStartInfo(Path.Combine(directory, "OrbitPet.exe"), "--self-test") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory }))
            {
                if (!test.WaitForExit(30000))
                {
                    try { test.Kill(); } catch { }
                    throw new InvalidOperationException("动画资源校验超时。校验目录：" + directory);
                }
                if (test.ExitCode != 0)
                    throw new InvalidOperationException("动画资源校验失败（退出码 " + test.ExitCode + "）。校验目录：" + directory + "。旧资源备份位于安装目录 asset-backups 中。");
            }
        }

        private static bool AssetTreesMatch(string expected, string installed)
        {
            if (!Directory.Exists(installed)) return false;
            // Do not follow user-created directory links outside the install.
            if ((File.GetAttributes(installed) & FileAttributes.ReparsePoint) != 0) return false;
            string[] oldFiles = RegularAssetFiles(installed);
            if (oldFiles == null) return false;
            string[] newFiles = Directory.GetFiles(expected, "*", SearchOption.AllDirectories);
            if (oldFiles.Length != newFiles.Length) return false;
            using (SHA256 hash = SHA256.Create())
                foreach (string file in newFiles)
                {
                    string old = Path.Combine(installed, file.Substring(expected.Length + 1));
                    if (!File.Exists(old) || new FileInfo(old).Length != new FileInfo(file).Length) return false;
                    byte[] oldHash, newHash;
                    using (Stream stream = File.OpenRead(old)) oldHash = hash.ComputeHash(stream);
                    using (Stream stream = File.OpenRead(file)) newHash = hash.ComputeHash(stream);
                    if (!String.Equals(Convert.ToBase64String(oldHash), Convert.ToBase64String(newHash), StringComparison.Ordinal)) return false;
                }
            return true;
        }

        private static string[] RegularAssetFiles(string root)
        {
            List<string> files = new List<string>();
            Stack<string> pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                foreach (string entry in Directory.GetFileSystemEntries(pending.Pop()))
                {
                    FileAttributes attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) return null;
                    if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                    else files.Add(entry);
                }
            }
            return files.ToArray();
        }

        private static string ClaudeDirectory
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable("FUFU_CLAUDE_CONFIG_DIR");
                if (String.IsNullOrWhiteSpace(configured))
                    configured = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
                return String.IsNullOrWhiteSpace(configured)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")
                    : Environment.ExpandEnvironmentVariables(configured);
            }
        }

        private static void RegisterHooks()
        {
            Directory.CreateDirectory(ClaudeDirectory);
            string settingsPath = Path.Combine(ClaudeDirectory, "settings.json");
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = Int32.MaxValue;
            Dictionary<string, object> root = new Dictionary<string, object>();
            if (File.Exists(settingsPath))
            {
                string json = File.ReadAllText(settingsPath);
                if (!String.IsNullOrWhiteSpace(json))
                    root = serializer.Deserialize<Dictionary<string, object>>(json);
                string backup = settingsPath + ".orbit-backup";
                File.Copy(settingsPath, backup, true);
            }

            Dictionary<string, object> hooks = GetOrCreateDictionary(root, "hooks");
            string[] events = { "SessionStart", "UserPromptSubmit", "PreToolUse", "PostToolUse", "PostToolUseFailure", "PermissionRequest", "Notification", "MessageDisplay", "TaskCompleted", "Stop", "StopFailure", "SessionEnd" };
            foreach (string eventName in events)
            {
                ArrayList groups = GetOrCreateList(hooks, eventName);
                RemoveManagedHookGroups(groups);
                Dictionary<string, object> handler = new Dictionary<string, object>();
                handler["type"] = "command";
                handler["command"] = BuildHookCommand(eventName);
                handler["timeout"] = 5;
                Dictionary<string, object> group = new Dictionary<string, object>();
                if (eventName == "Notification")
                    group["matcher"] = "permission_prompt|idle_prompt|agent_needs_input|elicitation_dialog";
                group["hooks"] = new object[] { handler };
                groups.Add(group);
            }

            WriteJsonAtomically(settingsPath, serializer.Serialize(root));
        }

        private static string BuildHookCommand(string eventName)
        {
            string forwardPath = PetExe.Replace('\\', '/').Replace("'", "''");
            // Decode the redirected hook pipe explicitly. Native invocation
            // alone can consume/reinterpret stdin or use the ANSI codepage.
            // Claude executes this command through Bash, so avoid $ variables
            // in the double-quoted bridge (Bash would expand them first).
            string script = "$ProgressPreference='SilentlyContinue'; $OutputEncoding=New-Object Text.UTF8Encoding($false); " +
                "$reader=New-Object IO.StreamReader([Console]::OpenStandardInput(),(New-Object Text.UTF8Encoding($false))); " +
                "$json=$reader.ReadToEnd(); $json | & '" + forwardPath + "' --hook " + eventName;
            string encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
            // A hook inherits the caller's console. -WindowStyle Hidden can
            // hide that existing CLI window; never change its window style.
            return "powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded;
        }

        private static Dictionary<string, object> GetOrCreateDictionary(Dictionary<string, object> parent, string name)
        {
            object value;
            Dictionary<string, object> result;
            if (parent.TryGetValue(name, out value) && (result = value as Dictionary<string, object>) != null) return result;
            result = new Dictionary<string, object>();
            parent[name] = result;
            return result;
        }

        private static ArrayList GetOrCreateList(Dictionary<string, object> parent, string name)
        {
            object value;
            ArrayList result;
            if (parent.TryGetValue(name, out value) && (result = value as ArrayList) != null) return result;
            object[] array = value as object[];
            result = array == null ? new ArrayList() : new ArrayList(array);
            parent[name] = result;
            return result;
        }

        private static void RemoveManagedHookGroups(ArrayList groups)
        {
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                IDictionary group = groups[i] as IDictionary;
                if (group == null || !group.Contains("hooks")) continue;
                IEnumerable handlers = group["hooks"] as IEnumerable;
                if (handlers == null || group["hooks"] is string) continue;
                ArrayList kept = new ArrayList();
                bool changed = false;
                foreach (object item in handlers)
                {
                    IDictionary handler = item as IDictionary;
                    if (handler != null && handler.Contains("command") && IsPetHookCommand(Convert.ToString(handler["command"]))) changed = true;
                    else kept.Add(item);
                }
                if (!changed) continue;
                if (kept.Count == 0) groups.RemoveAt(i);
                else group["hooks"] = kept.ToArray();
            }
        }

        private static bool ContainsManagedHookCommand(object value)
        {
            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (String.Equals(Convert.ToString(entry.Key), "command", StringComparison.OrdinalIgnoreCase) && IsPetHookCommand(Convert.ToString(entry.Value))) return true;
                    if (ContainsManagedHookCommand(entry.Value)) return true;
                }
            }
            IEnumerable sequence = value as IEnumerable;
            if (sequence != null && !(value is string))
                foreach (object item in sequence) if (ContainsManagedHookCommand(item)) return true;
            return false;
        }

        private static bool IsPetHookCommand(string command)
        {
            string text = command ?? "";
            System.Text.RegularExpressions.Match encoded = System.Text.RegularExpressions.Regex.Match(text, @"-EncodedCommand\s+([A-Za-z0-9+/=]+)\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (encoded.Success)
            {
                try { text = System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(encoded.Groups[1].Value)); }
                catch (FormatException) { return false; }
            }
            string ownPath = PetExe.Replace('\\', '/').Replace("'", "''");
            return text.Replace('\\', '/').IndexOf(ownPath, StringComparison.OrdinalIgnoreCase) >= 0
                && text.IndexOf("--hook ", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void WriteJsonAtomically(string path, string json)
        {
            string temporary = path + ".xianpal-tmp";
            File.WriteAllText(temporary, json, new System.Text.UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        private static void RegisterStartup()
        {
            XianPal.StartupRegistration.SetEnabled(true, PetExe);
        }

        private static string StartMenuFolder
        {
            get
            {
                // Never touch the real Start menu from an isolated installer test.
                string programs = TestMode ? Path.Combine(InstallDirectory, "test-start-menu") : Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                if (String.IsNullOrWhiteSpace(programs)) throw new InvalidOperationException("无法取得当前用户的开始菜单目录。");
                return Path.Combine(programs, "仙伴 AI桌面伴侣");
            }
        }

        private static string StartMenuShortcut { get { return Path.Combine(StartMenuFolder, "仙伴 AI桌面伴侣.lnk"); } }

        // Keep these declarations in Win32 vtable order, including unused methods.
        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int capacity, IntPtr findData, uint flags);
            void GetIDList(out IntPtr itemList);
            void SetIDList(IntPtr itemList);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int capacity);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int capacity);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int capacity);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
            void GetHotkey(out ushort hotkey);
            void SetHotkey(ushort hotkey);
            void GetShowCmd(out int showCommand);
            void SetShowCmd(int showCommand);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int capacity, out int iconIndex);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int iconIndex);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);
            void Resolve(IntPtr owner, uint flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        }

        private static IShellLinkW CreateShellLink()
        {
            return (IShellLinkW)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"), true));
        }

        private static bool ShortcutBelongsToPet(string path)
        {
            IShellLinkW shortcut = CreateShellLink();
            try
            {
                ((IPersistFile)shortcut).Load(path, 0);
                var targetPath = new StringBuilder(260);
                shortcut.GetPath(targetPath, targetPath.Capacity, IntPtr.Zero, 0);
                string target = targetPath.ToString();
                return !String.IsNullOrWhiteSpace(target) && String.Equals(Path.GetFullPath(target), Path.GetFullPath(PetExe), StringComparison.OrdinalIgnoreCase);
            }
            finally { Marshal.FinalReleaseComObject(shortcut); }
        }

        private static void RegisterStartMenuShortcut()
        {
            Directory.CreateDirectory(StartMenuFolder);
            IShellLinkW shortcut = null;
            string temporary = Path.Combine(StartMenuFolder, ".orbit-" + Guid.NewGuid().ToString("N") + ".lnk");
            try
            {
                if (File.Exists(StartMenuShortcut) && !ShortcutBelongsToPet(StartMenuShortcut))
                    throw new InvalidOperationException("开始菜单中存在指向其他程序的同名快捷方式，未覆盖：" + StartMenuShortcut);
                shortcut = CreateShellLink();
                shortcut.SetPath(PetExe);
                shortcut.SetWorkingDirectory(InstallDirectory);
                shortcut.SetDescription("仙伴 AI桌面伴侣 · 连接本机 Claude Code CLI");
                shortcut.SetShowCmd(1);
                // Versioned file avoids reusing Explorer's stale transparent icon.
                string icon = Path.Combine(InstallDirectory, "OrbitPet-" + Version + ".ico");
                shortcut.SetIconLocation(File.Exists(icon) ? icon : PetExe, 0);
                ((IPersistFile)shortcut).Save(temporary, true);
                Marshal.FinalReleaseComObject(shortcut); shortcut = null;
                if (File.Exists(StartMenuShortcut)) File.Replace(temporary, StartMenuShortcut, null);
                else File.Move(temporary, StartMenuShortcut);
            }
            finally
            {
                if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static void RemoveStartMenuShortcut()
        {
            if (!File.Exists(StartMenuShortcut)) return;
            if (ShortcutBelongsToPet(StartMenuShortcut)) File.Delete(StartMenuShortcut);
            if (Directory.Exists(StartMenuFolder) && Directory.GetFileSystemEntries(StartMenuFolder).Length == 0)
                Directory.Delete(StartMenuFolder, false);
        }

        private static void RegisterUninstaller()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\OrbitPet"))
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayVersion", Version);
                key.SetValue("Publisher", "Orbit");
                key.SetValue("InstallLocation", InstallDirectory);
                key.SetValue("DisplayIcon", PetExe);
                key.SetValue("UninstallString", "\"" + Path.Combine(InstallDirectory, "Uninstall.exe") + "\" --uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        private static void StopPet()
        {
            foreach (Process process in Process.GetProcessesByName("OrbitPet"))
            {
                try
                {
                    // Never stop another installation merely because its process name matches.
                    if (!String.Equals(process.MainModule.FileName, PetExe, StringComparison.OrdinalIgnoreCase)) continue;
                    process.CloseMainWindow(); if (!process.WaitForExit(1500)) process.Kill();
                }
                catch { }
            }
        }

        private static void Uninstall()
        {
            DialogResult answer = MessageBox.Show("确定卸载仙伴 AI桌面伴侣，并移除 Claude Code 联动吗？", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            try
            {
                ValidateInstallLocation(true);
                StopPet();
                RemoveHooks();
                RemoveStartMenuShortcut();
                XianPal.StartupRegistration.SetEnabled(false, PetExe);
                Registry.CurrentUser.DeleteSubKeyTree("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\OrbitPet", false);
                // Do not recursively delete a user-writable install tree. It may
                // contain user documents, old asset backups, or directory links.
                // Remove integration only and let the user review remaining files.
                MessageBox.Show("已移除开机启动、快捷方式及本安装的 Claude Code 联动。\n为保留可能存在的用户文件，程序目录未自动删除，可在关闭本窗口后自行清理：\n" + InstallDirectory, AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("卸载失败：\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ValidateInstallLocation(bool requireOwned)
        {
            string target = Path.GetFullPath(InstallDirectory).TrimEnd(Path.DirectorySeparatorChar);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] broad = { Path.GetPathRoot(target), profile,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) };
            foreach (string path in broad)
                if (!String.IsNullOrEmpty(path) && (String.Equals(target, Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                    || Path.GetFullPath(path).StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("安装目录过于宽泛，请选择独立应用目录。");
            for (DirectoryInfo cursor = new DirectoryInfo(target); cursor != null; cursor = cursor.Parent)
                if (cursor.Exists && (cursor.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("安装路径不能包含目录链接。");
            string marker = Path.Combine(target, ".orbitpet-install");
            bool owned = File.Exists(marker) && File.ReadAllText(marker) == "OrbitPet:3";
            if ((requireOwned || (Directory.Exists(target) && Directory.GetFileSystemEntries(target).Length > 0)) && !owned)
                throw new InvalidOperationException("此目录不属于本应用，请选择一个空目录。");
        }

        private static void RemoveHooks()
        {
            string path = Path.Combine(ClaudeDirectory, "settings.json");
            if (!File.Exists(path)) return;
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = Int32.MaxValue;
            Dictionary<string, object> root = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            object hooksObject;
            Dictionary<string, object> hooks;
            if (!root.TryGetValue("hooks", out hooksObject) || (hooks = hooksObject as Dictionary<string, object>) == null) return;
            foreach (string key in new List<string>(hooks.Keys))
            {
                ArrayList groups = GetOrCreateList(hooks, key);
                RemoveManagedHookGroups(groups);
                if (groups.Count == 0) hooks.Remove(key);
            }
            if (hooks.Count == 0) root.Remove("hooks");
            WriteJsonAtomically(path, serializer.Serialize(root));
        }
    }

}
