// SPDX-License-Identifier: Apache-2.0
namespace XianPal.Linux;

internal static class LinuxProcessIdentity
{
    private static string StartJiffies(int id)
    {
        string stat = File.ReadAllText("/proc/" + id + "/stat");
        int end = stat.LastIndexOf(')');
        if (end < 0) throw new InvalidDataException("Invalid process stat.");
        string[] fields = stat[(end + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 20 || !ulong.TryParse(fields[19], out _)) throw new InvalidDataException("Invalid process start time.");
        return fields[19]; // /proc field 22; stable clock ticks since this boot.
    }

    internal static string CurrentMarker() => Environment.ProcessId + "|" + StartJiffies(Environment.ProcessId) + "|" + File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim();

    internal static bool? IsAlive(string[] marker)
    {
        if (marker.Length != 3 || !int.TryParse(marker[0], out int id) || !ulong.TryParse(marker[1], out _) || !Guid.TryParse(marker[2], out _)) return null;
        try
        {
            return marker[1] == StartJiffies(id) && marker[2] == File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim();
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
