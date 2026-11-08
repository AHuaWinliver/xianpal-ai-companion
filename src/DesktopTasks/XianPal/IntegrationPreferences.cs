using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if XIANPAL_LINUX
using JavaScriptSerializer = XianPal.Linux.CompatibleJson;
#else
using System.Web.Script.Serialization;
#endif

namespace XianPal
{
    internal sealed class IntegrationPreferences
    {
        public bool MonitorExternalSessions { get; set; }
        public string ClaudeSettingsPath { get; set; }
        public Dictionary<string, string> CharacterClaudeSettings { get; set; }
        public string ClaudeExecutablePath { get; set; }
        public Dictionary<string, string> CharacterClaudeExecutables { get; set; }

        internal string ExecutablePathFor(string characterId)
        {
            string selected = ExecutableOverrideFor(characterId);
            return String.IsNullOrWhiteSpace(selected) ? ClaudeExecutablePath ?? "" : selected;
        }

        internal string ExecutableOverrideFor(string characterId)
        {
            if (String.IsNullOrEmpty(characterId)) return ClaudeExecutablePath ?? "";
            string value;
            if (!String.IsNullOrEmpty(characterId) && CharacterClaudeExecutables != null &&
                CharacterClaudeExecutables.TryGetValue(characterId.ToLowerInvariant(), out value) && !String.IsNullOrWhiteSpace(value)) return value;
            return "";
        }

        internal void SetExecutablePath(string characterId, string path)
        {
            string normalized = ValidateExecutable(path);
            if (String.IsNullOrEmpty(characterId)) { ClaudeExecutablePath = normalized; return; }
            if (CharacterClaudeExecutables == null) CharacterClaudeExecutables = new Dictionary<string, string>();
            string key = characterId.ToLowerInvariant();
            if (normalized.Length == 0) CharacterClaudeExecutables.Remove(key);
            else CharacterClaudeExecutables[key] = normalized;
        }

        internal static string ValidateExecutable(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return "";
            string absolute = Path.GetFullPath(path.Trim());
            if (!File.Exists(absolute)) throw new FileNotFoundException("Claude executable does not exist.", absolute);
#if XIANPAL_LINUX
            if (OperatingSystem.IsLinux() && (File.GetUnixFileMode(absolute) &
                (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
                throw new ArgumentException("The selected Claude program is not executable.");
#else
            string extension = Path.GetExtension(absolute);
            if (!String.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose a Claude .exe, .cmd or .bat program.");
#endif
            return absolute;
        }

        internal string SettingsPathFor(string characterId)
        {
            if (String.IsNullOrEmpty(characterId)) return ClaudeSettingsPath ?? "";
            string path;
            return CharacterClaudeSettings != null && CharacterClaudeSettings.TryGetValue(characterId.ToLowerInvariant(), out path)
                ? path ?? "" : "";
        }

        internal void SetSettingsPath(string characterId, string path)
        {
            if (String.IsNullOrEmpty(characterId))
            {
                ClaudeSettingsPath = path;
                return;
            }
            if (CharacterClaudeSettings == null) CharacterClaudeSettings = new Dictionary<string, string>();
            string key = characterId.ToLowerInvariant();
            if (String.IsNullOrEmpty(path)) CharacterClaudeSettings.Remove(key);
            else CharacterClaudeSettings[key] = path;
        }
        private static string StoragePath { get { return Path.Combine(Program.StateDirectory, "claude-integration.json"); } }

        internal static IntegrationPreferences Load()
        {
            try
            {
                if (File.Exists(StoragePath))
                    return new JavaScriptSerializer().Deserialize<IntegrationPreferences>(File.ReadAllText(StoragePath, Encoding.UTF8)) ?? new IntegrationPreferences();
            }
            catch { }
            // External CLI sessions belong to the user, not to this pet.
            return new IntegrationPreferences();
        }

        internal void Save()
        {
            Directory.CreateDirectory(Program.StateDirectory);
            string temporary = StoragePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(this), new UTF8Encoding(false));
                if (File.Exists(StoragePath)) File.Replace(temporary, StoragePath, null);
                else File.Move(temporary, StoragePath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        internal static bool ShouldAcceptHook(string internalTaskId, string managedSession, bool monitorExternal)
        {
            // Scheduled runs have their own record; never import them twice.
            return !Program.ShouldSuppressHook(internalTaskId) &&
                (String.Equals(managedSession, "1", StringComparison.Ordinal) || monitorExternal);
        }
    }
}
