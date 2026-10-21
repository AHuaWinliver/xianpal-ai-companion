// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace XianPal
{
    internal sealed class CharacterPackManifest
    {
        public int schemaVersion { get; set; }
        public string id { get; set; }
        public string version { get; set; }
        public string name { get; set; }
        public string persona { get; set; }
        public string[] quotes { get; set; }
        public string reminderStyle { get; set; }
        public int canvasWidth { get; set; }
        public int canvasHeight { get; set; }
        public float mouthX { get; set; }
        public float mouthY { get; set; }
        public Dictionary<string, string> files { get; set; }
    }

    internal sealed class CharacterAnimationSet : IDisposable
    {
        internal Dictionary<string, List<Image>> Frames = new Dictionary<string, List<Image>>(StringComparer.OrdinalIgnoreCase);
        public void Dispose()
        {
            foreach (List<Image> frames in Frames.Values) foreach (Image image in frames) image.Dispose();
            Frames.Clear();
        }
    }

    internal sealed class CharacterPackScan : IDisposable
    {
        internal PetCharacter[] Characters;
        internal string[] Errors;
        internal string PreparedId;
        internal CharacterAnimationSet PreparedAssets;
        public void Dispose() { if (PreparedAssets != null) { PreparedAssets.Dispose(); PreparedAssets = null; } }
    }

    // Data-only packages. No assemblies, scripts, URLs, author-supplied paths or code execution.
    internal sealed class CharacterPackCatalog
    {
        internal static readonly string[] States = { "idle", "waiting", "failed", "running", "running-left", "running-right", "waving", "review", "jumping", "punching", "eating", "wiping-mouth" };
        internal readonly string Root;
        private Dictionary<string, PetCharacter> previous = new Dictionary<string, PetCharacter>(StringComparer.OrdinalIgnoreCase);
        private const long MaxFrameBytes = 2 * 1024 * 1024, MaxPackBytes = 64 * 1024 * 1024;

        internal CharacterPackCatalog(string root)
        { Root = Path.GetFullPath(root); Directory.CreateDirectory(Root); CheckPath(Root); }

        internal static int FrameCount(string state)
        { return state == "idle" || state == "waiting" || state == "failed" ? 8 : 24; }

        internal static bool ReservedId(string id)
        {
            foreach (PetCharacter p in PetCharacter.All) if (String.Equals(id, p.Id, StringComparison.OrdinalIgnoreCase)) return true;
            return Array.IndexOf(new[] { "lumen", "nova", "ribbon", "coco", "dawn", "pebble", "slate" }, id) >= 0;
        }

        private static void CheckPath(string path)
        {
            // Check ancestors as well: an innocent-looking child may be beneath a junction.
            string cursor = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(cursor))
            {
                if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("资源路径不能包含符号链接或目录联接：" + path);
                cursor = Path.GetDirectoryName(cursor);
            }
        }

        private static byte[] ReadBytes(string path, long limit)
        {
            CheckPath(path);
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length <= 0 || stream.Length > limit) throw new InvalidDataException("资源文件为空或过大：" + Path.GetFileName(path));
                byte[] data = new byte[(int)stream.Length]; int offset = 0, read;
                while (offset < data.Length && (read = stream.Read(data, offset, data.Length - offset)) > 0) offset += read;
                if (offset != data.Length || stream.ReadByte() != -1) throw new IOException("资源正在写入，请完成复制后重试。");
                return data;
            }
        }

        private static uint BigEndian(byte[] data, int offset)
        { return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3]; }

        internal static PetCharacter ReadPack(string directory, bool loadImages, out CharacterAnimationSet assets)
        {
            assets = new CharacterAnimationSet();
            try
            {
                directory = Path.GetFullPath(directory); CheckPath(directory);
                string manifestPath = Path.Combine(directory, "character.json");
                byte[] metadata = ReadBytes(manifestPath, 64 * 1024);
                var serializer = new JavaScriptSerializer { MaxJsonLength = 64 * 1024, RecursionLimit = 16 };
                CharacterPackManifest m = serializer.Deserialize<CharacterPackManifest>(new UTF8Encoding(false, true).GetString(metadata).TrimStart('\uFEFF'));
                if (m == null || m.schemaVersion != 1) throw new InvalidDataException("不支持的角色资源格式版本。");
                if (m.id == null || !Regex.IsMatch(m.id, @"^[a-z][a-z0-9-]{2,47}$") || ReservedId(m.id) || !String.Equals(m.id, Path.GetFileName(directory), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("角色 id 必须与文件夹同名，且不能占用内置角色或历史兼容名称。");
                if (m.version == null || m.version.Length > 32 || !Regex.IsMatch(m.version, @"^\d+\.\d+\.\d+$")) throw new InvalidDataException("角色 version 应为三段版本号。");
                if (String.IsNullOrWhiteSpace(m.name) || m.name.Length > 20 || m.name.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0 || String.IsNullOrWhiteSpace(m.persona) || m.persona.Length > 4000)
                    throw new InvalidDataException("请提供有效角色名（最多20字符）和人设（最多4000字符）。");
                if (m.quotes == null || m.quotes.Length < 5 || m.quotes.Length > 50) throw new InvalidDataException("角色应有5到50条台词。");
                foreach (string quote in m.quotes) if (String.IsNullOrWhiteSpace(quote) || quote.Length > 200) throw new InvalidDataException("每条台词应为1到200字符。");
                if (m.canvasWidth != 384 || m.canvasHeight != 416 || Single.IsNaN(m.mouthX) || Single.IsInfinity(m.mouthX) || Single.IsNaN(m.mouthY) || Single.IsInfinity(m.mouthY) || m.mouthX <= 0 || m.mouthX >= 1 || m.mouthY <= 0 || m.mouthY >= 1)
                    throw new InvalidDataException("画布必须为384×416，嘴部位置必须在0与1之间。");
                if (Array.IndexOf(new[] { "command", "beam", "warning", "stone" }, m.reminderStyle) < 0) throw new InvalidDataException("请选择支持的角色演绎样式。");
                if (m.files == null || m.files.Count != 240) throw new InvalidDataException("请用导出工具生成完整的240帧SHA256清单。");
                foreach (string state in States) for (int f = 0; f < FrameCount(state); f++)
                {
                    string expected;
                    if (!m.files.TryGetValue(state + "/" + f.ToString("00") + ".png", out expected) || expected == null || !Regex.IsMatch(expected, @"^[a-fA-F0-9]{64}$"))
                        throw new InvalidDataException("动作SHA256清单缺失或格式不正确。");
                }
                foreach (string state in new[] { "running", "running-left", "running-right" })
                {
                    var phases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int f = 0; f < 24; f++) phases.Add(m.files[state + "/" + f.ToString("00") + ".png"]);
                    if (phases.Count != 24) throw new InvalidDataException(state + " 应有24个不同的步态帧，不能复制少量图片凑帧。");
                }

                foreach (string child in Directory.GetDirectories(directory))
                    if (Array.IndexOf(States, Path.GetFileName(child)) < 0) throw new InvalidDataException("不支持的资源子目录：" + Path.GetFileName(child));
                foreach (string file in Directory.GetFiles(directory))
                    if (!String.Equals(Path.GetFileName(file), "character.json", StringComparison.OrdinalIgnoreCase) && !String.Equals(Path.GetFileName(file), "README.md", StringComparison.OrdinalIgnoreCase) && !String.Equals(Path.GetFileName(file), "LICENSE.txt", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("资源包只允许角色配置、说明、许可与动作PNG。");

                long total = metadata.Length;
                using (SHA256 hash = SHA256.Create())
                {
                    hash.TransformBlock(metadata, 0, metadata.Length, metadata, 0);
                    foreach (string state in States)
                    {
                        string folder = Path.Combine(directory, state); CheckPath(folder);
                        if (Directory.GetDirectories(folder).Length != 0 || Directory.GetFiles(folder).Length != FrameCount(state)) throw new InvalidDataException(state + " 的帧数或目录结构不正确。");
                        List<Image> frames = new List<Image>(); if (loadImages) assets.Frames.Add(state, frames);
                        for (int f = 0; f < FrameCount(state); f++)
                        {
                            byte[] data = ReadBytes(Path.Combine(folder, f.ToString("00") + ".png"), MaxFrameBytes);
                            using (SHA256 frameHash = SHA256.Create())
                                if (!String.Equals(BitConverter.ToString(frameHash.ComputeHash(data)).Replace("-", ""), m.files[state + "/" + f.ToString("00") + ".png"], StringComparison.OrdinalIgnoreCase))
                                    throw new InvalidDataException(state + "/" + f.ToString("00") + ".png 校验失败，可能尚未完成复制。");
                            total += data.Length; if (total > MaxPackBytes) throw new InvalidDataException("单个角色包不能超过64MB。");
                            if (data.Length < 33 || BigEndian(data, 0) != 0x89504e47 || BigEndian(data, 4) != 0x0d0a1a0a || BigEndian(data, 8) != 13 || BigEndian(data, 12) != 0x49484452 || BigEndian(data, 16) != 384 || BigEndian(data, 20) != 416)
                                throw new InvalidDataException(state + "/" + f.ToString("00") + ".png 必须为384×416的真实PNG。");
                            hash.TransformBlock(data, 0, data.Length, data, 0);
                            using (MemoryStream stream = new MemoryStream(data, false))
                            using (Image image = Image.FromStream(stream, false, true))
                            {
                                if (image.RawFormat.Guid != ImageFormat.Png.Guid || image.Width != 384 || image.Height != 416 || !Image.IsAlphaPixelFormat(image.PixelFormat)) throw new InvalidDataException("动作PNG必须带透明通道。");
                                if (loadImages) frames.Add(new Bitmap(image));
                            }
                        }
                    }
                    hash.TransformFinalBlock(new byte[0], 0, 0);
                    byte[] check = ReadBytes(manifestPath, 64 * 1024);
                    if (!Equal(metadata, check)) throw new IOException("角色配置正在更新，请完成复制后重试。");
                    return new PetCharacter { Id = m.id, Name = m.name, Persona = m.persona, Quotes = m.quotes, MouthX = m.mouthX, MouthY = m.mouthY,
                        ReminderStyle = m.reminderStyle, ResourceRoot = directory, ResourceRevision = BitConverter.ToString(hash.Hash).Replace("-", "") };
                }
            }
            catch { assets.Dispose(); assets = null; throw; }
        }

        private static bool Equal(byte[] a, byte[] b)
        { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }

        internal CharacterPackScan Scan(string activeId, string activeRevision)
        {
            var next = new Dictionary<string, PetCharacter>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>(); var result = new CharacterPackScan();
            try
            {
                CheckPath(Root);
                string[] folders = Directory.GetDirectories(Root); Array.Sort(folders, StringComparer.OrdinalIgnoreCase);
                int count = 0;
                foreach (string folder in folders)
                {
                    string id = Path.GetFileName(folder);
                    if (id.StartsWith(".", StringComparison.Ordinal)) continue; // Authors stage a complete pack before renaming it.
                    if (++count > 16) { errors.Add("最多同时扫描16个外置角色，请移出多余目录。"); break; }
                    try
                    {
                        CharacterAnimationSet ignored; PetCharacter character = ReadPack(folder, false, out ignored); ignored.Dispose();
                        if (String.Equals(character.Id, activeId, StringComparison.OrdinalIgnoreCase) && character.ResourceRevision != activeRevision)
                        {
                            CharacterAnimationSet prepared; PetCharacter second = ReadPack(folder, true, out prepared);
                            if (second.ResourceRevision != character.ResourceRevision) { prepared.Dispose(); throw new IOException("角色正在更新，暂不切换。"); }
                            result.PreparedAssets = prepared; result.PreparedId = character.Id;
                        }
                        next.Add(character.Id, character);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(id + "：" + ex.Message);
                        PetCharacter old; if (previous.TryGetValue(id, out old)) next[id] = old;
                    }
                }
                previous = next;
            }
            catch (Exception ex)
            { next = new Dictionary<string, PetCharacter>(previous, StringComparer.OrdinalIgnoreCase); errors.Add("角色资源目录：" + ex.Message); }
            var sorted = new List<PetCharacter>(next.Values); sorted.Sort(delegate(PetCharacter a, PetCharacter b) { return StringComparer.OrdinalIgnoreCase.Compare(a.Id, b.Id); });
            result.Characters = sorted.ToArray(); result.Errors = errors.ToArray(); return result;
        }
    }
}
