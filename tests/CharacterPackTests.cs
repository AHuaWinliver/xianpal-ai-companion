// SPDX-License-Identifier: Apache-2.0
// Synthetic rectangles only: these fixtures test loading, not finished character artwork.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using XianPal;

internal static class CharacterPackTests
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static object Field(object value, string name) { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }
    private static object Call(object value, string name, params object[] args) { return value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args); }
    private static bool Until(Func<bool> condition, int milliseconds)
    { Stopwatch watch = Stopwatch.StartNew(); while (!condition() && watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(20); } return condition(); }
    private static string Hash(byte[] bytes) { using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
    private static CharacterPackManifest Manifest(string folder) { return Json.Deserialize<CharacterPackManifest>(File.ReadAllText(Path.Combine(folder, "character.json"))); }
    private static void SaveManifest(string folder, CharacterPackManifest manifest) { File.WriteAllText(Path.Combine(folder, "character.json"), Json.Serialize(manifest), new UTF8Encoding(false)); }

    private static string WritePack(string parent, string id, string version, Color color)
    {
        string folder = Path.Combine(parent, id); Directory.CreateDirectory(folder);
        var m = new CharacterPackManifest { schemaVersion = 1, id = id, version = version, name = "测试伙伴 " + version, persona = "原创的测试伙伴，" + version,
            quotes = new[] { "一起开始吧。", "先做一小步。", "歇歇眼睛吧。", "慢慢理清思路。", "我们稳稳地来。" }, reminderStyle = "beam", canvasWidth = 384, canvasHeight = 416, mouthX = .5f, mouthY = .4f,
            files = new Dictionary<string, string>() };
        foreach (string state in CharacterPackCatalog.States)
        {
            Directory.CreateDirectory(Path.Combine(folder, state));
            for (int f = 0; f < CharacterPackCatalog.FrameCount(state); f++)
            {
                string relative = state + "/" + f.ToString("00") + ".png";
                using (Bitmap image = new Bitmap(384, 416, PixelFormat.Format32bppArgb))
                using (Graphics g = Graphics.FromImage(image))
                using (Brush fill = new SolidBrush(color))
                {
                    g.Clear(Color.Transparent); g.FillRectangle(fill, 160, 160, 60, 90); g.FillEllipse(fill, 90 + f, 80 + f, 18, 18);
                    image.Save(Path.Combine(folder, relative), ImageFormat.Png);
                }
                m.files.Add(relative, Hash(File.ReadAllBytes(Path.Combine(folder, relative))));
            }
        }
        SaveManifest(folder, m); return folder;
    }

    private static void MustReject(string folder, string reason)
    {
        CharacterAnimationSet assets = null; bool rejected = false;
        try { CharacterPackCatalog.ReadPack(folder, false, out assets); }
        catch { rejected = true; }
        finally { if (assets != null) assets.Dispose(); }
        Require(rejected, reason);
    }

    private static void TestValidation(string root)
    {
        var catalog = new CharacterPackCatalog(root);
        string folder = WritePack(root, "test-walker", "0.1.0", Color.ForestGreen);
        string frame = Path.Combine(folder, "idle", "00.png"); byte[] original = File.ReadAllBytes(frame);
        string revision;
        using (CharacterPackScan scan = catalog.Scan(null, null))
        {
            Require(scan.Errors.Length == 0 && scan.Characters.Length == 1, "new data-only pack discovered with 240 verified PNG frames");
            revision = scan.Characters[0].ResourceRevision;
            PetCharacter.SetExternalProfiles(scan.Characters);
            Require(PetCharacter.Available.Length == 5 && PetCharacter.All.Length == 4 && PetCharacter.Find("test-walker").IsExternal && PetCharacter.ValidateProfiles(), "dynamic profiles do not change built-in generation or legacy aliases");
        }
        CharacterAnimationSet loaded;
        CharacterPackCatalog.ReadPack(folder, true, out loaded);
        using (loaded) using (FileStream exclusive = File.Open(frame, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Require(loaded.Frames["running-left"].Count == 24, "decoded frames do not lock source files");

        File.WriteAllBytes(frame, new byte[] { 1, 2, 3 });
        using (CharacterPackScan scan = catalog.Scan("test-walker", revision))
            Require(scan.Errors.Length == 1 && scan.Characters[0].ResourceRevision == revision && scan.PreparedAssets == null, "partial/corrupt update keeps last known profile without replacing active frames");
        File.WriteAllBytes(frame, original);
        CharacterPackManifest m = Manifest(folder); m.id = "../outside"; SaveManifest(folder, m); MustReject(folder, "path traversal id rejected before image loading");
        m.id = "orbit"; SaveManifest(folder, m); MustReject(folder, "built-in ID collision rejected");
        m.id = "test-walker"; m.schemaVersion = 999; SaveManifest(folder, m); MustReject(folder, "unknown schema rejected");
        m.schemaVersion = 1; m.reminderStyle = "execute-code"; SaveManifest(folder, m); MustReject(folder, "executable/custom code styles rejected");
        m.reminderStyle = "beam"; SaveManifest(folder, m);
        string executable = Path.Combine(folder, "plugin.dll"); File.WriteAllBytes(executable, new byte[] { 1 }); MustReject(folder, "executable resource files rejected"); File.Delete(executable);
        File.Delete(frame); MustReject(folder, "missing animation frame rejected"); File.WriteAllBytes(frame, original);
        using (Bitmap wrong = new Bitmap(16, 16)) wrong.Save(frame, ImageFormat.Png);
        m.files["idle/00.png"] = Hash(File.ReadAllBytes(frame)); SaveManifest(folder, m); MustReject(folder, "wrong PNG dimensions rejected even with a matching hash");
        File.WriteAllBytes(frame, original); m.files["idle/00.png"] = Hash(original);
        string oldHash = m.files["running-right/01.png"]; m.files["running-right/01.png"] = m.files["running-right/00.png"];
        SaveManifest(folder, m); MustReject(folder, "duplicated walking phases cannot be used to pad 24 frames");
        m.files["running-right/01.png"] = oldHash; SaveManifest(folder, m);
        byte[] inflatedHeader = (byte[])original.Clone(); inflatedHeader[16] = 0x7f; File.WriteAllBytes(frame, inflatedHeader);
        m.files["idle/00.png"] = Hash(inflatedHeader); SaveManifest(folder, m); MustReject(folder, "oversized PNG header rejected before decompression");
        File.WriteAllBytes(frame, original); m.files["idle/00.png"] = Hash(original); SaveManifest(folder, m);
        byte[] json = File.ReadAllBytes(Path.Combine(folder, "character.json")); File.WriteAllText(Path.Combine(folder, "character.json"), "{");
        using (CharacterPackScan scan = catalog.Scan("test-walker", revision)) Require(scan.Errors.Length > 0 && scan.Characters[0].ResourceRevision == revision, "broken JSON does not remove the last known character");
        File.WriteAllBytes(Path.Combine(folder, "character.json"), json);
        WritePack(root, "test-walker", "0.2.0", Color.CornflowerBlue);
        using (CharacterPackScan scan = catalog.Scan("test-walker", revision))
            Require(scan.PreparedAssets != null && scan.Characters[0].ResourceRevision != revision, "selected update is decoded off-thread before UI delivery");
        Directory.Move(folder, Path.Combine(root, ".removed-test-walker"));
        using (CharacterPackScan scan = catalog.Scan(null, null)) Require(scan.Characters.Length == 0, "removed packs disappear and dot-prefixed staging folders are ignored");
        PetCharacter.SetExternalProfiles(null);
    }

    private static bool CurrentPixel(PetForm pet, Color color)
    {
        var frames = (Dictionary<string, List<Image>>)Field(pet, "animations");
        return ((Bitmap)frames["idle"][0]).GetPixel(180, 200).ToArgb() == color.ToArgb();
    }

    private static void TestLive(string stateRoot)
    {
        string packs = Path.Combine(stateRoot, "characters"); Directory.CreateDirectory(packs);
        string folder = WritePack(packs, "test-walker", "0.1.0", Color.ForestGreen);
        using (PetForm pet = new PetForm())
        {
            pet.Show(); ((System.Windows.Forms.Timer)Field(pet, "timer")).Stop();
            Require(PetCharacter.Find("test-walker").IsExternal, "installed app discovers pack on startup without recompilation");
            Call(pet, "SetCharacter", "test-walker", false);
            Require((string)Field(pet, "currentCharacterId") == "test-walker" && CurrentPixel(pet, Color.ForestGreen) && pet.LayeredRenderSucceeded, "external character switches through the existing renderer");
            Require(Field(pet, "walkingExpressionClock") == null, "external full-frame faces do not receive another character's head/neck overlays");
            Require(pet.ValidateLayeredAnimationAdvance(), "external 24-phase walking advances through lazy native frame cache");
            Require(PetBrain.BuildConversationPrompt(PetCharacter.Find("test-walker"), "你好").Contains("原创的测试伙伴"), "external persona drives the existing AI conversation prompt");
            Require(ReminderPerformance.Supports(PetCharacter.Find("test-walker").ReminderStyle), "external reminder style uses supported choreography with its own frames");
            using (FileStream exclusive = File.Open(Path.Combine(folder, "idle", "00.png"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            WritePack(packs, "second-pet", "0.1.0", Color.Purple);
            Require(Until(delegate { return PetCharacter.Find("second-pet").IsExternal; }, 20000), "filesystem events add a new character while app remains running");
            string revision = (string)Field(pet, "loadedCharacterRevision");
            pet.GetType().GetField("state", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pet, "waiting");
            pet.GetType().GetField("frameIndex", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pet, 3);
            WritePack(packs, "test-walker", "0.2.0", Color.CornflowerBlue);
            Require(Until(delegate { return (string)Field(pet, "loadedCharacterRevision") != revision && CurrentPixel(pet, Color.CornflowerBlue); }, 20000), "selected character updates in place without restart or reinstall");
            Require((string)Field(pet, "state") == "waiting" && (int)Field(pet, "frameIndex") == 3, "hot update preserves in-progress task reaction and animation phase");
            revision = (string)Field(pet, "loadedCharacterRevision");
            string frame = Path.Combine(folder, "idle", "00.png"); byte[] original = File.ReadAllBytes(frame);
            File.WriteAllBytes(frame, new byte[] { 1, 2, 3 });
            Require(Until(delegate { return ((string[])Field(pet, "characterPackErrors")).Length > 0; }, 20000), "background validation reports a damaged live update");
            Require((string)Field(pet, "loadedCharacterRevision") == revision && CurrentPixel(pet, Color.CornflowerBlue), "damaged update never disposes the visible last-good character");
            File.WriteAllBytes(frame, original);
            Require(Until(delegate { return ((string[])Field(pet, "characterPackErrors")).Length == 0; }, 20000), "fixed pack recovers automatically without restart");
            pet.ContextMenuStrip.Show(pet, new Point(0, 0));
            WritePack(packs, "test-walker", "0.3.0", Color.Orange);
            Require(Until(delegate { return Field(pet, "pendingCharacterScan") != null; }, 20000), "completed scan waits while a menu is open");
            Require((string)Field(pet, "loadedCharacterRevision") == revision, "resource update does not interrupt open menu interactions");
            pet.ContextMenuStrip.Close();
            Require(Until(delegate { return CurrentPixel(pet, Color.Orange); }, 20000), "deferred update applies after menu closes");
            Directory.Move(folder, Path.Combine(packs, ".removed-test-walker"));
            Require(Until(delegate { return (string)Field(pet, "currentCharacterId") == "orbit"; }, 20000), "deleting selected pack safely falls back to built-in companion");
            pet.Close();
            Require((bool)Field(pet, "characterPackWatchingDisposed"), "watcher, timer and queued resources are disposed on close");
        }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1 || !Path.GetFileName(args[0]).StartsWith("xianpal-character-pack-test-", StringComparison.Ordinal)) throw new Exception("isolated fixture directory required");
            string root = Path.GetFullPath(args[0]), state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
            Environment.SetEnvironmentVariable("FUFU_TEST_MODE", "1"); Environment.SetEnvironmentVariable("FUFU_STATE_DIR", state);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            TestValidation(Path.Combine(root, "validation")); TestLive(state);
            WritePack(Path.Combine(root, "export-frames"), "export-demo", "0.1.0", Color.ForestGreen);
            Console.WriteLine("PASS data-only packs and live refresh: no real Claude calls, reminders, startup settings or user files modified.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { PetCharacter.SetExternalProfiles(null); }
    }
}
