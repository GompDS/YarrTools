using SoulsFormats;

namespace YarrToMap.Util;

public static class Bxf4Utils
{
    public static long Size(this BXF4 binder)
    {
        long numberOfBytes = 0;
        foreach (BinderFile file in binder.Files)
        {
            numberOfBytes += file.Bytes.Length;
        }

        return numberOfBytes;
    }

    public static bool IsEqual(this BXF4 binderA, BXF4 binderB)
    {
        long difference = Math.Abs(binderA.Size() - binderB.Size());
        int allowedByteDifference = 500000;
        if (difference > allowedByteDifference)
        {
            IEnumerable<BinderFile> okToMove = binderA.Files.Where(x =>
                x.Bytes.Length < binderA.Size() - binderB.Size() && !x.Name.Contains("_l."));
            if (okToMove.Any())
            {
                return false;
            }
        }

        return true;
    }

    public static void TransferLargestTextureLessThanDifference(this BXF4 binderA, BXF4 binderB)
    {
        long maxBytes = binderA.Files
            .Where(x => x.Bytes.Length < binderA.Size() - binderB.Size() && !x.Name.Contains("_l."))
            .Max(x => x.Bytes.Length);
        BinderFile nonLodTex = binderA.Files.First(x => !x.Name.Contains("_l.") && x.Bytes.Length == maxBytes);
        BinderFile? lodTex = binderA.Files.FirstOrDefault(x =>
            x.Name.Equals(nonLodTex.Name.Insert(nonLodTex.Name.IndexOf(".", StringComparison.Ordinal), "_l")));
        binderA.TransferFile(binderB, nonLodTex);
        if (lodTex != null)
        {
            binderA.TransferFile(binderB, lodTex);
        }
    }

    public static void TransferFile(this BXF4 binderA, BXF4 binderB, BinderFile file)
    {
        binderB.Files.Add(new BinderFile(file.Flags, binderB.Files.Count, file.Name, (byte[])file.Bytes.Clone()));
        for (int i = binderA.Files.IndexOf(file) + 1; i < binderA.Files.Count; i++)
        {
            binderA.Files[i].ID--;
        }

        binderA.Files.Remove(file);
    }

    public static Tuple<BXF4, int>? SearchForTexture(this BXF4[] binders, string textureName)
    {
        foreach (BXF4 binder in binders)
        {
            foreach (BinderFile file in binder.Files)
            {
                if (file.Name.Equals($"{textureName}.tpf.dcx"))
                {
                    return new Tuple<BXF4, int>(binder, file.ID);
                }
            }
        }

        return null;
    }
    
    public static int SearchForTexture(this BXF4 binder, string textureName)
    {
        foreach (BinderFile file in binder.Files)
        {
            if (file.Name.Equals($"{textureName}.tpf.dcx"))
            {
                return file.ID;
            }
        }

        return -1;
    }

    public static void CopyTextureTo(this BXF4 smallestTextureBinder, string file, BXF4[] textureBinders)
    {
        byte format = 0;
        if (file.Contains("_n.") || file.Contains("_n_l."))
        {
            format = 0x6A;
        }

        TPF.Texture newTexture = new(Path.GetFileNameWithoutExtension(file), format, 0, File.ReadAllBytes(file));
        TPF newTpf = new();
        newTpf.Textures.Add(newTexture);

        Tuple<BXF4, int>? textureLocation =
            textureBinders.SearchForTexture(Path.GetFileNameWithoutExtension(file));
        if (textureLocation != null)
        {
            BinderFile newFile = new BinderFile(Binder.FileFlags.Flag1, textureLocation.Item2,
                $"{Path.GetFileNameWithoutExtension(file)}.tpf.dcx",
                DCX.Compress(newTpf.Write(), DCX.Type.DCX_DFLT_10000_44_9));
            textureLocation.Item1.Files[textureLocation.Item2] = newFile;
        }
        else
        {
            BinderFile newFile = new BinderFile(Binder.FileFlags.Flag1, smallestTextureBinder.Files.Count,
                $"{Path.GetFileNameWithoutExtension(file)}.tpf.dcx",
                DCX.Compress(newTpf.Write(), DCX.Type.DCX_DFLT_10000_44_9));
            smallestTextureBinder.Files.Add(newFile);
        }
    }
    
    /// <summary>
    /// Used for copying _gi_ textures to the envTextureBinder from YARR.
    /// </summary>
    public static void CopyTextureTo(this BXF4 targetTextureBinder, string file)
    {
        TPF.Texture newTexture = new(Path.GetFileNameWithoutExtension(file), 0x66, 0, File.ReadAllBytes(file));
        newTexture.Type = TPF.TexType.Cubemap;
        TPF newTpf = new()
        {
            Flag2 = 0x03
        };
        newTpf.Textures.Add(newTexture);

        int textureIndex = targetTextureBinder.SearchForTexture(Path.GetFileNameWithoutExtension(file));
        if (textureIndex >= 0)
        {
            BinderFile newFile = new (Binder.FileFlags.Flag1, textureIndex,
                $"{Path.GetFileNameWithoutExtension(file)}.tpf.dcx",
                DCX.Compress(newTpf.Write(), DCX.Type.DCX_DFLT_10000_44_9));
            targetTextureBinder.Files[textureIndex] = newFile;
        }
        else
        {
            BinderFile newFile = new (Binder.FileFlags.Flag1, targetTextureBinder.Files.Count,
                $"{Path.GetFileNameWithoutExtension(file)}.tpf.dcx",
                DCX.Compress(newTpf.Write(), DCX.Type.DCX_DFLT_10000_44_9));
            targetTextureBinder.Files.Add(newFile);
        }
    }
    
    public static bool CopyTextureTo(this BXF4 smallestTextureBinder, BinderFile file, BXF4[] textureBinders)
    {
        string simpleName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file.Name));

        Tuple<BXF4, int>? textureLocation =
            textureBinders.SearchForTexture(simpleName);
        if (textureLocation == null)
        {
            file.ID = smallestTextureBinder.Files.Count;
            smallestTextureBinder.Files.Add(file);
            return true;
        }

        return false;
    }
    
    public static bool CopyTextureTo(this BXF4 targetTextureBinder, BinderFile file)
    {
        string simpleName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file.Name));

        if (targetTextureBinder.SearchForTexture(simpleName) >= 0) return false;
        file.ID = targetTextureBinder.Files.Count;
        targetTextureBinder.Files.Add(file);
        return true;
    }

    public static int RemoveUnusedTextures(this BXF4 textureBinder, HashSet<string> usedTextures)
    {
        int unusedTextureBytes = 0;
        for (int j = 0; j < textureBinder.Files.Count; j++)
        {
            textureBinder.Files[j].ID = j;
                        
            if (textureBinder.Files[j].Name.Contains("_l."))
            {
                if (!usedTextures.Contains(textureBinder.Files[j].Name.Split("_l.")[0]))
                {
                    unusedTextureBytes += textureBinder.Files[j].Bytes.Length;
                    textureBinder.Files.Remove(textureBinder.Files[j]);
                    j--;
                }
            }
            else
            {
                if (!usedTextures.Contains(textureBinder.Files[j].Name.Split(".")[0]))
                {
                    unusedTextureBytes += textureBinder.Files[j].Bytes.Length;
                    textureBinder.Files.Remove(textureBinder.Files[j]);
                    j--;
                }
            }
        }

        return unusedTextureBytes;
    }

    public static int GetUnusedTextureByteCount(this BXF4 textureBinder, HashSet<string> usedTextures, string[] tpfbhds, string[] tpfbdts)
    {
        int unusedTextureBytes = 0;
        foreach (BinderFile nonLodFile in textureBinder
                     .Files.Where(x => !x.Name.Contains("_l.") && 
                                       !usedTextures.Contains(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(x.Name)))))
        {
            unusedTextureBytes += nonLodFile.Bytes.Length;
            string lodName = Path.GetFileName(nonLodFile.Name).Insert(Path.GetFileName(nonLodFile.Name).IndexOf(".", StringComparison.Ordinal), "_l");
            BinderFile? lodFile = textureBinder.Files.FirstOrDefault(x => x.Name.Contains(lodName));
                        
            int j = 0;
            while (lodFile == null && j < 4)
            {
                BXF4 otherGameTextureBinder = BXF4.Read(tpfbhds[j], tpfbdts[j]);
                lodFile = otherGameTextureBinder.Files.FirstOrDefault(x => x.Name.Contains(lodName));
                j++;
            }

            if (lodFile == null) continue;
            unusedTextureBytes += lodFile.Bytes.Length;
        }

        return unusedTextureBytes;
    }
    
    public static int GetUnusedTextureByteCount(this BXF4 textureBinder, HashSet<string> usedTextures)
    {
        int unusedTextureBytes = 0;
        foreach (BinderFile nonLodFile in textureBinder
                     .Files.Where(x => !x.Name.Contains("_l.") && 
                                       !usedTextures.Contains(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(x.Name)))))
        {
            unusedTextureBytes += nonLodFile.Bytes.Length;
            string lodName = Path.GetFileName(nonLodFile.Name).Insert(Path.GetFileName(nonLodFile.Name).IndexOf(".", StringComparison.Ordinal), "_l");
            BinderFile? lodFile = textureBinder.Files.FirstOrDefault(x => x.Name.Contains(lodName));
            if (lodFile == null) continue;
            unusedTextureBytes += lodFile.Bytes.Length;
        }

        return unusedTextureBytes;
    }
}