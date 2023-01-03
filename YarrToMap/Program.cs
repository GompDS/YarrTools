using SoulsFormats;
using YarrToMap.Util;

namespace YarrToMap;

class Program
{
    public static void Main()
    {
        Options op = new();

        Console.WriteLine(": Searching the game files for textures used by this map... (this may take a few minutes)");
        
        HashSet<string> usedTextures = new();
        HashSet<string> usedMapPieces = new();
        HashSet<string> usedObjects = new();
        HashSet<string> objectsInMod = new();
        
        if (Directory.Exists(op.ModMapDirectory))
        {
            string? mapPartsDirectory = Directory.GetDirectories(op.ModMapDirectory, 
                $"m{op.MapId}_{op.BlockId.ToString("D2")}_00_00").FirstOrDefault();
            if (mapPartsDirectory != null)
            {
                usedMapPieces.UnionWith(HashSetUtils.GetSetOfUsedMapPieces(op.ModMapDirectory, op.MapId, op.BlockId));
                usedTextures.AddUsedMapPieceTexturesFromFolder(usedMapPieces, mapPartsDirectory);
            }
            
            if (Directory.Exists(op.ModObjectDirectory))
            {
                usedObjects.UnionWith(HashSetUtils.GetSetOfUsedObjects(op.ModMapDirectory, op.MapId, op.BlockId));
                usedTextures.AddUsedObjectTexturesFromFolder(usedObjects, op.ModObjectDirectory);
                IEnumerable<string> objPaths = Directory.EnumerateFiles(op.ModObjectDirectory);
                foreach (string path in objPaths.Where(x => usedObjects.Contains(Path.GetFileName(x)[..7])))
                {
                    objectsInMod.Add(Path.GetFileName(path)[..7]);
                }
            }
        }
        
        if (Directory.Exists(op.GameMapDirectory))
        {
            string? mapPartsDirectory = Directory.GetDirectories(op.GameMapDirectory, 
                $"m{op.MapId}_{op.BlockId.ToString("D2")}_00_00").FirstOrDefault();
            if (mapPartsDirectory != null)
            {
                if (usedMapPieces.Count == 0)
                {
                    usedMapPieces.UnionWith(HashSetUtils.GetSetOfUsedMapPieces(op.GameMapDirectory, op.MapId, op.BlockId));
                }
                usedTextures.AddUsedMapPieceTexturesFromFolder(usedMapPieces, mapPartsDirectory);
            }
            
            if (Directory.Exists(op.GameObjectDirectory))
            {
                if (usedObjects.Count == 0)
                {
                    usedObjects.UnionWith(HashSetUtils.GetSetOfUsedObjects(op.GameMapDirectory, op.MapId, op.BlockId));
                }
                else
                {
                    usedObjects.ExceptWith(objectsInMod);
                }
                usedTextures.AddUsedObjectTexturesFromFolder(usedObjects, op.GameObjectDirectory);
            }
        }
        
        Console.WriteLine($": Found {usedTextures.Count} unique textures (not counting LODs) used by this map.");

        if (!Directory.Exists(op.ModMapDirectory)) return;
        var textureBinders = new BXF4[4];
        BXF4 envTextureBinder = new();

        string? mapTexturesDirectory = Directory.GetDirectories(op.ModMapDirectory, 
            $"m{op.MapId}").FirstOrDefault();
        if (mapTexturesDirectory != null)
        {
            Console.WriteLine(": Removing unused textures from this map...");
                
            int unusedTextureBytes = 0;
            
            string[] mapTpfbhds = Directory.EnumerateFiles(mapTexturesDirectory).Where(x => 
                x.ToLower().EndsWith($"m{op.MapId}_*.tpfbhd") || x.ToLower().EndsWith($"m{op.MapId}_*.tpfbhd.patch"))
                .ToArray();
            string[] mapTpfbdts = Directory.EnumerateFiles(mapTexturesDirectory).Where(x => 
                    x.ToLower().EndsWith($"m{op.MapId}_*.tpfbdt") || x.ToLower().EndsWith($"m{op.MapId}_*.tpfbdt.patch"))
                .ToArray();
            for (int i = 0; i < 4; i++)
            {
                if (mapTpfbhds.Length > i && File.ReadAllBytes(mapTpfbdts[i]).Length > 0)
                {
                    textureBinders[i] = BXF4.Read(mapTpfbhds[i], mapTpfbdts[i]);
                }
                else
                {
                    textureBinders[i] = new BXF4();
                }

                unusedTextureBytes += textureBinders[i].RemoveUnusedTextures(usedTextures);
            }

            string? envTpfbhdPath = Directory.EnumerateFiles(mapTexturesDirectory).FirstOrDefault(x => 
                x.ToLower().EndsWith($"gi_env_m{op.MapId}.tpfbhd") || x.ToLower().EndsWith($"gi_env_m{op.MapId}.tpfbhd.patch"));
            string? envTpfbdtPath = Directory.EnumerateFiles(mapTexturesDirectory).FirstOrDefault(x => 
                x.ToLower().EndsWith($"gi_env_m{op.MapId}.tpfbdt") || x.ToLower().EndsWith($"gi_env_m{op.MapId}.tpfbdt.patch"));
            if (envTpfbhdPath != null && envTpfbdtPath != null && File.ReadAllBytes(envTpfbdtPath).Length > 0)
            {
                envTextureBinder = BXF4.Read(envTpfbhdPath, envTpfbdtPath);
                unusedTextureBytes += envTextureBinder.RemoveUnusedTextures(usedTextures);
            }
            else
            {
                envTextureBinder = new BXF4();
            }
                
            Console.WriteLine($": Removed approximately {unusedTextureBytes / 1000} KB of unused textures.");

            Console.WriteLine(": Making sure the existing map textures are distributed evenly...");

            int numberOfMoves = 0;
            while (true)
            {
                if (!textureBinders[0].IsEqual(textureBinders[1]))
                {
                    MakeSizeBasedAdjustment(textureBinders[0], textureBinders[1]);
                }
                else if (!textureBinders[1].IsEqual(textureBinders[2]))
                {
                    MakeSizeBasedAdjustment(textureBinders[1], textureBinders[2]);
                }
                else if (!textureBinders[2].IsEqual(textureBinders[3]))
                {
                    MakeSizeBasedAdjustment(textureBinders[2], textureBinders[3]);
                }
                else
                {
                    break;
                }

                numberOfMoves++;
            }
                
            Console.WriteLine($": Moved textures {numberOfMoves} times to ensure even distribution.");
        }
        else
        {
            Directory.CreateDirectory($"{op.ModMapDirectory}\\m{op.MapId}");
            for (int i = 0; i < 4; i++)
            {
                textureBinders[i] = new BXF4();
            }
        }

        Console.WriteLine(": Copying used textures from YARR to this map...");
            
        int transferCount = 0;
        IEnumerable<string> ddsFiles = Directory.EnumerateFiles(op.YarrTextureDirectory, "*.dds");
        foreach (string nonLodFile in ddsFiles.Where(x => !x.Contains("_l.") && usedTextures.Contains(Path.GetFileNameWithoutExtension(x))))
        {
            string lodFile;
            if (nonLodFile.Contains("_gi_", StringComparison.OrdinalIgnoreCase))
            {
                envTextureBinder.CopyTextureTo(nonLodFile);
                transferCount++;
                
                lodFile = nonLodFile.Insert(nonLodFile.IndexOf(".", StringComparison.Ordinal), "_l");
                if (!File.Exists(lodFile)) continue;
                envTextureBinder.CopyTextureTo(lodFile);
                transferCount++;
            }
            else
            {
                long smallestBinderSize = textureBinders.Min(x => x.Size());
                BXF4 smallestTextureBinder = textureBinders.First(x => x.Size() == smallestBinderSize);
                smallestTextureBinder.CopyTextureTo(nonLodFile, textureBinders);
                transferCount++;
                
                lodFile = nonLodFile.Insert(nonLodFile.IndexOf(".", StringComparison.Ordinal), "_l");
                if (!File.Exists(lodFile)) continue;
                smallestTextureBinder.CopyTextureTo(lodFile, textureBinders);
                transferCount++;
            }
        }
            
        Console.WriteLine($": Copied {transferCount} textures from YARR to this map.");
        
        if (!op.GetVanillaTextures || !op.GetVanillaTexturesEnv)
        {
            mapTexturesDirectory = Directory.GetDirectories(op.GameMapDirectory, 
                $"m{op.MapId}").FirstOrDefault();
            if (mapTexturesDirectory != null)
            {
                Console.WriteLine(": Copying used vanilla textures from game files to this map...");
                transferCount = 0;
                int unusedTextureBytes = 0;

                if (!op.GetVanillaTextures)
                {
                    string[] mapTpfbhds = Directory.EnumerateFiles(mapTexturesDirectory).Where(x => 
                            x.ToLower().EndsWith($"m{op.MapId}_*.tpfbhd")).ToArray();
                    string[] mapTpfbdts = Directory.EnumerateFiles(mapTexturesDirectory).Where(x => 
                            x.ToLower().EndsWith($"m{op.MapId}_*.tpfbdt")).ToArray();
                    for (int i = 0; i < 4; i++)
                    {
                        BXF4 gameTextureBinder = BXF4.Read(mapTpfbhds[i], mapTpfbdts[i]);
                        foreach (BinderFile nonLodFile in gameTextureBinder.Files.Where(x => !x.Name.Contains("_l.") &&
                                     usedTextures.Contains(
                                         Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(x.Name)))))
                        {
                            long smallestBinderSize = textureBinders.Min(x => x.Size());
                            BXF4 smallestTextureBinder = textureBinders.First(x => x.Size() == smallestBinderSize);

                            if (smallestTextureBinder.CopyTextureTo(nonLodFile, textureBinders)) transferCount++;

                            string lodName = Path.GetFileName(nonLodFile.Name).Insert(
                                Path.GetFileName(nonLodFile.Name).IndexOf(".", StringComparison.Ordinal), "_l");
                            BinderFile? lodFile = gameTextureBinder.Files.FirstOrDefault(x => x.Name.Contains(lodName));

                            int j = 0;
                            while (lodFile == null && j < 4)
                            {
                                BXF4 otherGameTextureBinder = BXF4.Read(mapTpfbhds[j], mapTpfbdts[j]);
                                lodFile = otherGameTextureBinder.Files.FirstOrDefault(x => x.Name.Contains(lodName));
                                j++;
                            }

                            if (lodFile == null) continue;
                            if (smallestTextureBinder.CopyTextureTo(lodFile, textureBinders)) transferCount++;
                        }

                        unusedTextureBytes += gameTextureBinder.GetUnusedTextureByteCount(usedTextures, mapTpfbhds, mapTpfbdts);
                    }
                }

                if (!op.GetVanillaTexturesEnv)
                {
                    string? envTpfbhdPath = Directory.EnumerateFiles(mapTexturesDirectory).FirstOrDefault(x => 
                        x.ToLower().EndsWith($"gi_env_m{op.MapId}.tpfbhd"));
                    string? envTpfbdtPath = Directory.EnumerateFiles(mapTexturesDirectory).FirstOrDefault(x => 
                        x.ToLower().EndsWith($"gi_env_m{op.MapId}.tpfbdt"));
                    if (envTpfbhdPath != null && envTpfbdtPath != null)
                    {
                        BXF4 gameEnvTextureBinder = BXF4.Read(envTpfbhdPath, envTpfbdtPath);
                        foreach (BinderFile nonLodFile in gameEnvTextureBinder.Files.Where(x =>
                                     !x.Name.Contains("_l.") &&
                                     usedTextures.Contains(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(x.Name)))))
                        {
                            if (envTextureBinder.CopyTextureTo(nonLodFile)) transferCount++;

                            string lodName = Path.GetFileName(nonLodFile.Name).Insert(
                                Path.GetFileName(nonLodFile.Name).IndexOf(".", StringComparison.Ordinal), "_l");
                            BinderFile? lodFile =
                                gameEnvTextureBinder.Files.FirstOrDefault(x => x.Name.Contains(lodName));
                            if (lodFile == null) continue;
                            if (envTextureBinder.CopyTextureTo(lodFile)) transferCount++;
                        }

                        unusedTextureBytes += gameEnvTextureBinder.GetUnusedTextureByteCount(usedTextures);
                    }
                }
                
                Console.WriteLine($": Copied {transferCount} vanilla textures from game files to this map.");
                Console.WriteLine($": Approximately {unusedTextureBytes / 1000} KB of vanilla textures were unused.");
            }
        }

        for (int i = 0; i < 4; i++)
        {
            if (textureBinders[i].Files.Count <= 0) continue;
            string pathBase = $"{op.ModMapDirectory}\\m{op.MapId}\\m{op.MapId}_000{i}";
            if (op.UsePatch)
            {
                textureBinders[i].Write($"{pathBase}.tpfbhd.patch", $"{pathBase}.tpfbdt.patch");
            }
            else
            {
                textureBinders[i].Write($"{pathBase}.tpfbhd", $"{pathBase}.tpfbdt");
            }
        }

        if (envTextureBinder.Files.Count > 0)
        {
            string pathBase = $"{op.ModMapDirectory}\\m{op.MapId}\\gi_env_m{op.MapId}";
            if (op.UsePatchEnv)
            {
                envTextureBinder.Write($"{pathBase}.tpfbhd.patch", $"{pathBase}.tpfbdt.patch");
            }
            else
            {
                envTextureBinder.Write($"{pathBase}.tpfbhd", $"{pathBase}.tpfbdt");
            }
        }

        Console.WriteLine(": Texture transfer complete!");
        Console.WriteLine("\nPress any key to exit...");
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

    private static void CreateBackup(BXF4 binder, string bhdPath, string bdtPath, Options op)
    {
        if (!File.Exists(bhdPath + ".bak") && !File.Exists(bdtPath + ".bak") && op.CreateBackups)
        {
            binder.Write(bhdPath + ".bak", bdtPath + ".bak");
        }
        File.Delete(bhdPath);
        File.Delete(bdtPath);
    }
}