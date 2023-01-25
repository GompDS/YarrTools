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

    public static bool SearchForTexture(this BXF4 binder, string searchFileName, out int foundIndex)
    {
        foreach (BinderFile file in binder.Files)
        {
            string fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file.Name));
            string shortSearchFileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(searchFileName));
            if (fileName.Equals(shortSearchFileName))
            {
                foundIndex = file.ID;
                return true;
            }
        }

        foundIndex = binder.Files.Count;
        return false;
    }
    
    public static void CopyTextureTo(this BXF4 targetTextureBinder, BinderFile file, int fileId)
    {
        file.ID = fileId;
        if (fileId < targetTextureBinder.Files.Count - 1)
        {
            targetTextureBinder.Files.RemoveAt(fileId);
            targetTextureBinder.Files.Insert(fileId, file);
            targetTextureBinder.FixFileIds();
        }
        else
        {
            targetTextureBinder.Files.Add(file);
        }
    }

    public static void FixFileIds(this BXF4 textureBinder)
    {
        for (int i = 0; i < textureBinder.Files.Count; i++)
        {
            textureBinder.Files[i].ID = i;
        }
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

    public static int GetUnusedTextureByteCount(this BXF4 textureBinder, HashSet<string> usedTextures)
    {
        int unusedTextureBytes = 0;
        foreach (BinderFile file in textureBinder.Files.Where(x =>
                     !usedTextures.Contains(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(x.Name)).Replace("_l", ""))))
        {
            unusedTextureBytes += file.Bytes.Length;
        }

        return unusedTextureBytes;
    }
}