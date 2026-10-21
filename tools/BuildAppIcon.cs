using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using XianPal;

internal static class BuildAppIcon
{
    private static int Main(string[] args)
    {
        if (args.Length != 3) return 2;
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        List<byte[]> images = new List<byte[]>();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1])));
        Directory.CreateDirectory(args[2]);
        using (Image source = Image.FromFile(args[0]))
            foreach (int size in sizes)
                using (Bitmap bitmap = AppIcon.CreateBitmap(source, size))
                {
                    bitmap.Save(Path.Combine(args[2], "icon-" + size + ".png"), ImageFormat.Png);
                    using (MemoryStream stream = new MemoryStream())
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        int maskStride = ((size + 31) / 32) * 4;
                        writer.Write(40); writer.Write(size); writer.Write(size * 2);
                        writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(0);
                        writer.Write(size * size * 4); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
                        for (int y = size - 1; y >= 0; y--)
                            for (int x = 0; x < size; x++)
                            {
                                Color pixel = bitmap.GetPixel(x, y);
                                if (pixel.A != 255) throw new InvalidOperationException("Icon must be fully opaque.");
                                writer.Write(pixel.B); writer.Write(pixel.G); writer.Write(pixel.R); writer.Write((byte)255);
                            }
                        writer.Write(new byte[maskStride * size]); // no transparent mask pixels
                        images.Add(stream.ToArray());
                    }
                }
        using (BinaryWriter writer = new BinaryWriter(File.Create(args[1])))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
            }
            foreach (byte[] image in images) writer.Write(image);
        }
        Console.WriteLine("Built opaque app icon: 9 sizes (16–256 px).");
        return 0;
    }
}
