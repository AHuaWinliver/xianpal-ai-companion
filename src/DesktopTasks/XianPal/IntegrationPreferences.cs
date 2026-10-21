using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace XianPal
{
    internal sealed class IntegrationPreferences
    {
        public bool MonitorExternalSessions { get; set; }
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
