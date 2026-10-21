using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class IconShortcutTests
{
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static object Get(object item, string name) { return item.GetType().InvokeMember(name, BindingFlags.GetProperty, null, item, null); }
    private static void Set(object item, string name, object value) { item.GetType().InvokeMember(name, BindingFlags.SetProperty, null, item, new object[] { value }); }
    private static object Shortcut(object shell, string path) { return shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path }); }
    private static void Release(object item) { if (item != null && Marshal.IsComObject(item)) Marshal.FinalReleaseComObject(item); }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string app = Path.GetFullPath(args[0]), installer = Path.GetFullPath(args[1]);
            string directory = Path.GetDirectoryName(app);
            Require(Environment.GetEnvironmentVariable("FUFU_TEST_MODE") == "1" && directory.Contains("desktop-package-test"), "isolated installer state required");
            string version = AssemblyName.GetAssemblyName(app).Version.ToString(3);
            string iconPath = Path.Combine(directory, "OrbitPet-" + version + ".ico");
            ValidateIcon(iconPath);
            using (Icon embedded = Icon.ExtractAssociatedIcon(app))
            using (Bitmap bitmap = embedded.ToBitmap()) CheckBitmap(bitmap, "application executable embedded icon");
            using (Icon embedded = Icon.ExtractAssociatedIcon(installer))
            using (Bitmap bitmap = embedded.ToBitmap()) CheckBitmap(bitmap, "installer executable embedded icon");

            Assembly appAssembly = Assembly.LoadFrom(app);
            MethodInfo render = appAssembly.GetType("XianPal.AppIcon").GetMethod("CreateBitmap", BindingFlags.Static | BindingFlags.NonPublic);
            List<string> images = new List<string> { Path.Combine(directory, "assets", "idle", "00.png") };
            foreach (string character in Directory.GetDirectories(Path.Combine(directory, "assets", "characters"))) images.Add(Path.Combine(character, "idle", "00.png"));
            foreach (string path in images)
                using (Image source = Image.FromFile(path))
                using (Bitmap bitmap = (Bitmap)render.Invoke(null, new object[] { source, 32 })) CheckBitmap(bitmap, "character tray icon " + Path.GetDirectoryName(Path.GetDirectoryName(path)));
            Require(images.Count == 4, "all 4 cultivation companion tray icons checked");

            Environment.SetEnvironmentVariable("FUFU_INSTALL_ROOT", directory);
            Type program = Assembly.LoadFrom(installer).GetType("XianPal.Installer.Program");
            MethodInfo hookMatch = program.GetMethod("IsPetHookCommand", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo hookBuild = program.GetMethod("BuildHookCommand", BindingFlags.Static | BindingFlags.NonPublic);
            string ownHook = (string)hookBuild.Invoke(null, new object[] { "Stop" });
            Require((bool)hookMatch.Invoke(null, new object[] { ownHook }), "encoded owned hook recognized");
            Require(!(bool)hookMatch.Invoke(null, new object[] { "echo OrbitPet --hook Stop" }), "foreign similarly named hook retained");
            Require(!(bool)hookMatch.Invoke(null, new object[] { "echo '" + app + "'" }), "path mention without hook is not owned");
            var mixed = new System.Collections.Hashtable { { "matcher", "test" }, { "hooks", new object[] {
                new System.Collections.Hashtable { { "command", ownHook } },
                new System.Collections.Hashtable { { "command", "echo keep-user-hook" } } } } };
            var groups = new System.Collections.ArrayList { mixed };
            program.GetMethod("RemoveManagedHookGroups", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { groups });
            Require(groups.Count == 1 && ((object[])mixed["hooks"]).Length == 1 && (string)((System.Collections.IDictionary)((object[])mixed["hooks"])[0])["command"] == "echo keep-user-hook", "mixed hook group preserves unrelated handlers");
            program.GetMethod("ValidateInstallLocation", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { true });
            Require(File.ReadAllText(Path.Combine(directory, ".orbitpet-install")) == "OrbitPet:3", "installation ownership marker validated");
            string linkPath = (string)program.GetProperty("StartMenuShortcut", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null, null);
            Require(linkPath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "test shortcut cannot touch real Start menu");
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
            try
            {
                CheckShortcut(shell, linkPath, app, directory, iconPath);
                MethodInfo create = program.GetMethod("RegisterStartMenuShortcut", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo remove = program.GetMethod("RemoveStartMenuShortcut", BindingFlags.Static | BindingFlags.NonPublic);
                create.Invoke(null, null);
                CheckShortcut(shell, linkPath, app, directory, iconPath);
                Require(Directory.GetFiles(Path.GetDirectoryName(linkPath), "*.lnk").Length == 1, "reinstall updates same shortcut without duplicates or staging leftovers");

                string neighbor = Path.Combine(Path.GetDirectoryName(linkPath), "user-kept-file.txt");
                File.WriteAllText(neighbor, "preserve neighboring user files");
                remove.Invoke(null, null);
                Require(!File.Exists(linkPath) && File.Exists(neighbor), "uninstall removes only owned link and preserves neighboring files");
                create.Invoke(null, null);

                string backup = Path.Combine(directory, "owned-shortcut-test-backup.lnk");
                File.Copy(linkPath, backup, true);
                try
                {
                    object foreign = Shortcut(shell, linkPath);
                    try { Set(foreign, "TargetPath", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe")); foreign.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, foreign, null); }
                    finally { Release(foreign); }
                    byte[] original = File.ReadAllBytes(linkPath);
                    bool rejected = false;
                    try { create.Invoke(null, null); } catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidOperationException; }
                    Require(rejected && SameBytes(original, File.ReadAllBytes(linkPath)), "foreign same-name shortcut is never overwritten");
                    remove.Invoke(null, null);
                    Require(File.Exists(linkPath) && SameBytes(original, File.ReadAllBytes(linkPath)), "uninstall never removes foreign shortcut");
                }
                finally { File.Copy(backup, linkPath, true); File.Delete(backup); File.Delete(neighbor); }
                CheckShortcut(shell, linkPath, app, directory, iconPath);
            }
            finally { Release(shell); }
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }

    private static bool SameBytes(byte[] first, byte[] second)
    {
        if (first.Length != second.Length) return false;
        for (int i = 0; i < first.Length; i++) if (first[i] != second[i]) return false;
        return true;
    }

    private static void CheckShortcut(object shell, string path, string app, string directory, string icon)
    {
        Require(File.Exists(path), "Chinese Start menu shortcut created");
        object shortcut = Shortcut(shell, path);
        try
        {
            Require(String.Equals(Convert.ToString(Get(shortcut, "TargetPath")), app, StringComparison.OrdinalIgnoreCase), "shortcut launches pet, not installer");
            Require(String.Equals(Convert.ToString(Get(shortcut, "WorkingDirectory")), directory, StringComparison.OrdinalIgnoreCase), "shortcut working directory is installed app");
            Require(Convert.ToString(Get(shortcut, "IconLocation")).StartsWith(icon, StringComparison.OrdinalIgnoreCase), "shortcut uses versioned opaque icon");
            Require(Convert.ToString(Get(shortcut, "Description")).Contains("Claude Code"), "shortcut describes Claude Code connection");
        }
        finally { Release(shortcut); }
    }

    private static void CheckBitmap(Bitmap bitmap, string label)
    {
        int orange = 0, contrast = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (pixel.A != 255) throw new Exception(label + " has transparent pixel at " + x + "," + y);
                if (pixel.R > pixel.G * 1.5 && pixel.R > pixel.B * 1.5 && pixel.R > 120) orange++;
                // New art is blue/peach/grey, not the retired green robot.
                // Check readable subject/connector contrast against the opaque badge.
                if (Math.Abs(pixel.R-255)+Math.Abs(pixel.G-250)+Math.Abs(pixel.B-237)>145) contrast++;
            }
        Require(orange > 0 && contrast > bitmap.Width*bitmap.Height/20, label + " opaque with visible companion and terminal badge");
    }

    private static void ValidateIcon(string path)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
        {
            Require(reader.ReadUInt16() == 0 && reader.ReadUInt16() == 1 && reader.ReadUInt16() == sizes.Length, "ICO contains nine resolution entries");
            uint[] offsets = new uint[sizes.Length];
            for (int i = 0; i < sizes.Length; i++)
            {
                int width = reader.ReadByte(), height = reader.ReadByte();
                if (width == 0) width = 256; if (height == 0) height = 256;
                Require(width == sizes[i] && height == sizes[i], "ICO size " + sizes[i]);
                reader.ReadByte(); reader.ReadByte(); reader.ReadUInt16(); Require(reader.ReadUInt16() == 32, "32-bit ICO color"); reader.ReadUInt32(); offsets[i] = reader.ReadUInt32();
            }
            for (int i = 0; i < sizes.Length; i++)
            {
                reader.BaseStream.Position = offsets[i] + 40;
                for (int pixel = 0; pixel < sizes[i] * sizes[i]; pixel++)
                { reader.ReadByte(); reader.ReadByte(); reader.ReadByte(); RequireAlpha(reader.ReadByte()); }
                int maskLength = ((sizes[i] + 31) / 32) * 4 * sizes[i];
                foreach (byte mask in reader.ReadBytes(maskLength)) if (mask != 0) throw new Exception("Transparent ICO AND mask");
            }
        }
        foreach (int size in sizes)
            using (Icon icon = new Icon(path, new Size(size, size)))
            using (Bitmap bitmap = icon.ToBitmap()) CheckBitmap(bitmap, "decoded ICO " + size);
    }

    private static void RequireAlpha(byte alpha) { if (alpha != 255) throw new Exception("ICO alpha is not opaque"); }
}
