// SPDX-License-Identifier: Apache-2.0
using System;
using XianPal;

internal static class CharacterPackTool
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1) throw new ArgumentException("Supply the character pack directory.");
            CharacterAnimationSet assets;
            PetCharacter character = CharacterPackCatalog.ReadPack(args[0], false, out assets);
            assets.Dispose();
            Console.WriteLine("PASS character pack: " + character.Id + " / " + character.Name + " / 240 PNG frames / SHA256 " + character.ResourceRevision);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
