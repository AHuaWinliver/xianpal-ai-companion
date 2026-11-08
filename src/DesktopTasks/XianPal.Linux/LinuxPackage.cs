// SPDX-License-Identifier: Apache-2.0
using System.Security.Cryptography;
using System.Text;

namespace XianPal.Linux;

internal sealed class LinuxInstallationResult
{
    internal string PreviousDirectory = "", Warning = "";
}

internal static class LinuxPackage
{
    internal const string Marker = ".xianpal-install";
    private const string Identity = "XianPal:Linux:1";
    internal static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "opt", "xianpal");

    internal static bool Owns(string directory) => File.Exists(Path.Combine(directory, Marker)) &&
        new FileInfo(Path.Combine(directory, Marker)).LinkTarget == null && File.ReadAllText(Path.Combine(directory, Marker)).Trim() == Identity;

    internal static string ValidateDestination(string source, string destination)
    {
        string target = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar);
        source = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar);
        if (string.IsNullOrEmpty(target) || target == home || new[] { "/usr", "/etc", "/var", "/home", "/tmp", "/opt", "/usr/local", "/usr/local/bin" }.Contains(target))
            throw new ArgumentException(UiLanguage.Text("Choose a dedicated application folder, not a system or user root."));
        if (target == source || target.StartsWith(source + "/", StringComparison.Ordinal) || source.StartsWith(target + "/", StringComparison.Ordinal))
            throw new ArgumentException("The destination must be separate from the package you are running.");
        for (DirectoryInfo parent = new(target); parent != null; parent = parent.Parent)
            if (parent.LinkTarget != null) throw new IOException(UiLanguage.Text("The installation path cannot contain directory links."));
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any() && !Owns(target))
            throw new IOException(UiLanguage.Text("This folder contains unrelated files. Choose an empty application folder."));
        return target;
    }

    private static FileStream InstallationLease()
    {
        Directory.CreateDirectory(Program.StateDirectory);
        var lease = File.Open(Path.Combine(Program.StateDirectory, "desktop.lock"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
        try { lease.Lock(0, 1); return lease; }
        catch { lease.Dispose(); throw new IOException("Close XianPal before installing or updating. Running Claude jobs are not stopped automatically."); }
    }

    internal static void ValidatePackage(string source)
    {
        foreach (string file in new[] { "xianpal", "xianpal.dll", "xianpal.deps.json", "xianpal.runtimeconfig.json", "launch.sh", "LICENSE", "NOTICE", "ASSET-LICENSE.md" })
            if (!File.Exists(Path.Combine(source, file))) throw new InvalidDataException("Incomplete Linux package: " + file);
        using (var executable = File.OpenRead(Path.Combine(source, "xianpal")))
            if (executable.ReadByte() != 0x7f || executable.ReadByte() != 'E' || executable.ReadByte() != 'L' || executable.ReadByte() != 'F')
                throw new InvalidDataException("The package does not contain a native Linux executable.");
        foreach (PetCharacter character in PetCharacter.All)
        {
            string root = character.Id == "orbit" ? Path.Combine(source, "assets") : Path.Combine(source, "assets", "characters", character.Id);
            foreach (string state in CharacterPackCatalog.States)
                for (int frame = 0; frame < CharacterPackCatalog.FrameCount(state); frame++)
                    if (!File.Exists(Path.Combine(root, state, frame.ToString("00") + ".png")))
                        throw new InvalidDataException("Missing character animation: " + character.Id + "/" + state + "/" + frame);
        }
    }

    internal static LinuxInstallationResult Install(string source, string destination, IProgress<string> progress = null)
    {
        source = Path.GetFullPath(source);
        ValidatePackage(source);
        string target = ValidateDestination(source, destination);
        using var lease = InstallationLease();
        string staging = target + ".install-" + Guid.NewGuid().ToString("N");
        string backup = target + ".previous-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        Directory.CreateDirectory(staging);
        bool movedPrevious = false, committed = false;
        try
        {
            // Never follow package links. The published payload is a self-contained
            // directory; character packs and user state live outside this directory.
            var files = new List<string>();
            void Collect(string directory)
            {
                foreach (string item in Directory.EnumerateFileSystemEntries(directory))
                {
                    var info = new FileInfo(item);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.LinkTarget != null) throw new IOException("Package links are not supported: " + item);
                    if ((info.Attributes & FileAttributes.Directory) != 0) Collect(item);
                    else if (Path.GetFileName(item) != Marker && Path.GetFileName(item) != "installation.json") files.Add(item);
                }
            }
            Collect(source);
            var hashes = new Dictionary<string, string>();
            for (int index = 0; index < files.Count; index++)
            {
                string relative = Path.GetRelativePath(source, files[index]);
                string output = Path.Combine(staging, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.Copy(files[index], output);
                if (OperatingSystem.IsLinux()) File.SetUnixFileMode(output, File.GetUnixFileMode(files[index]));
                string sourceHash = Hash(files[index]);
                if (sourceHash != Hash(output)) throw new IOException("Copied file failed verification: " + relative);
                hashes.Add(relative, sourceHash);
                if (index % 20 == 0) progress?.Report((index + 1) + " / " + files.Count);
            }
            if (OperatingSystem.IsLinux())
                foreach (string executable in new[] { "xianpal", "launch.sh", "diagnose-linux.sh", "install.sh" })
                {
                    string file = Path.Combine(staging, executable);
                    if (File.Exists(file)) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
            ValidatePackage(staging);
            File.WriteAllText(Path.Combine(staging, Marker), Identity, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(staging, "installation.json"), new CompatibleJson().Serialize(new { version = typeof(Program).Assembly.GetName().Version?.ToString(3), installedAt = DateTime.UtcNow.ToString("o"), files = hashes }), new UTF8Encoding(false));
            // Retain the complete previous installation. No recursive deletion of
            // an existing installation or personal data is performed.
            if (Directory.Exists(target)) { Directory.Move(target, backup); movedPrevious = true; }
            try { Directory.Move(staging, target); committed = true; }
            catch { if (movedPrevious && !Directory.Exists(target)) Directory.Move(backup, target); throw; }
            var result = new LinuxInstallationResult { PreviousDirectory = movedPrevious ? backup : "" };
            try { LinuxStartup.RegisterInstallation(target); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            { result.Warning = "Application files were installed, but the desktop launcher could not be registered: " + error.Message; }
            return result;
        }
        finally
        {
            // Staging contains only files copied by this invocation, and its
            // absolute path was constructed beside the validated target.
            if (!committed && Directory.Exists(staging) && Path.GetDirectoryName(staging) == Path.GetDirectoryName(target) &&
                Path.GetFileName(staging).StartsWith(Path.GetFileName(target) + ".install-", StringComparison.Ordinal) && new DirectoryInfo(staging).LinkTarget == null)
                Directory.Delete(staging, true);
        }
    }

    private static string Hash(string path)
    { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
}
