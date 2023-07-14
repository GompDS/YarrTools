using System.Text.RegularExpressions;
using SoulsFormats;
using YarrToMap.Util;

namespace YarrToMap;

static class Program
{
    public static void Main()
    {
        Options op = new();

        Console.WriteLine(": Searching the game files for textures used by this map... (this may take a few minutes)");

        TextureManager textureManager = new(op);

        Console.WriteLine($": Found {textureManager.UsedTextures.Count} unique textures (not counting LODs) used by this map.\n");

        List<BXF4> regularTextureBinders = textureManager.GetRegularTextureBinders(op.ModMapDirectory, op);
        //BXF4 lightmapBinder = textureManager.GetLightmapBinder(op.ModMapDirectory, op);

        Console.WriteLine(": Removing unused textures from this map...");

        int unusedTextureBytes = regularTextureBinders.Sum(textureBinder => textureBinder.RemoveUnusedTextures(textureManager.UsedTextures));
        //unusedTextureBytes += lightmapBinder.RemoveUnusedTextures(textureManager.UsedTextures);
        
        Console.WriteLine($": Removed approximately {unusedTextureBytes / 1000} KB of unused textures.\n");
        
        Console.WriteLine(": Making sure the existing map textures are distributed evenly...");
        
        int numberOfMoves = 0;
        while (true)
        {
            if (!regularTextureBinders[0].IsEqual(regularTextureBinders[1]))
            {
                MakeSizeBasedAdjustment(regularTextureBinders[0], regularTextureBinders[1]);
            }
            else if (!regularTextureBinders[1].IsEqual(regularTextureBinders[2]))
            {
                MakeSizeBasedAdjustment(regularTextureBinders[1], regularTextureBinders[2]);
            }
            else if (!regularTextureBinders[2].IsEqual(regularTextureBinders[3]))
            {
                MakeSizeBasedAdjustment(regularTextureBinders[2], regularTextureBinders[3]);
            }
            else
            {
                break;
            }

            numberOfMoves++;
        }
                
        Console.WriteLine($": Moved textures {numberOfMoves} times to ensure even distribution.\n");

        Console.WriteLine(": Copying used textures from YARR to this map...");
        
        int transferCount = 0;
        Regex pattern = new (@"[0-9]{8}_m[0-9]{6}_gi_[0-9]{4}_[0-9]{2}_dol_[0-9]{2}");
        IEnumerable<string> files = Directory.EnumerateFiles(op.YarrTextureDirectory, "*.dds")
            .Where(x => !pattern.IsMatch(x));
        transferCount += textureManager.TransferYarrTextures(files, regularTextureBinders, op);
        /*files = Directory.EnumerateFiles(op.YarrTextureDirectory, "*.dds")
            .Where(x => pattern.IsMatch(x));
        transferCount += textureManager.TransferYarrTextures(files, new List<BXF4> { lightmapBinder }, op);*/

        Console.WriteLine($": Copied {transferCount} textures from YARR to this map.\n");

        if (op.IncludeRegularTextures /*|| op.IncludeLightmaps*/)
        {
            Console.WriteLine(": Copying used vanilla textures from game files to this map...");
            transferCount = 0;
            unusedTextureBytes = 0;
            if (op.IncludeRegularTextures)
            {
                List<BXF4> vanillaTextureBinders = textureManager.GetRegularTextureBinders(op.GameMapDirectory, op);
                foreach (BXF4 vanillaTextureBinder in vanillaTextureBinders)
                {
                    transferCount += textureManager.TransferVanillaTextures(vanillaTextureBinder, regularTextureBinders, op);
                    unusedTextureBytes += vanillaTextureBinder.GetUnusedTextureByteCount(textureManager.UsedTextures);
                }
            }
            /*
            if (op.IncludeLightmaps)
            {
                BXF4 vanillaEnvTextureBinder = textureManager.GetLightmapBinder(op.GameMapDirectory, op);
                transferCount += textureManager.TransferVanillaTextures(vanillaEnvTextureBinder, new List<BXF4> {lightmapBinder}, op);
                unusedTextureBytes += vanillaEnvTextureBinder.GetUnusedTextureByteCount(textureManager.UsedTextures);
            }*/

            Console.WriteLine($": Copied {transferCount} vanilla textures from game files to this map.");
            Console.WriteLine($": Approximately {unusedTextureBytes / 1000} KB of vanilla textures were unused.\n");
        }

        for (int i = 0; i < 4; i++)
        {
            if (regularTextureBinders[i].Files.Count <= 0) continue;
            string pathBase = $"{op.ModMapDirectory}\\m{op.MapId}\\m{op.MapId}_000{i}";
            if (op.PatchRegular)
            {
                regularTextureBinders[i].Write($"{pathBase}.tpfbhd.patch", $"{pathBase}.tpfbdt.patch");
            }
            else
            {
                regularTextureBinders[i].Write($"{pathBase}.tpfbhd", $"{pathBase}.tpfbdt");
            }
        }

        /*if (lightmapBinder.Files.Count > 0)
        {
            string pathBase = $"{op.ModMapDirectory}\\m{op.MapId}\\gi_env_m{op.MapId}";
            if (op.PatchLightmaps)
            {
                lightmapBinder.Write($"{pathBase}.tpfbhd.patch", $"{pathBase}.tpfbdt.patch");
            }
            else
            {
                lightmapBinder.Write($"{pathBase}.tpfbhd", $"{pathBase}.tpfbdt");
            }
        }*/

        Console.WriteLine(": Texture transfer complete!");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    private static void MakeSizeBasedAdjustment(BXF4 binderA, BXF4 binderB)
    {
        if (binderA.Size() > binderB.Size())
        {
            binderA.TransferLargestTextureLessThanDifference(binderB);
        }
        else
        {
            binderB.TransferLargestTextureLessThanDifference(binderA);
        }
    }
}