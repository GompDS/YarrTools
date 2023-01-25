using System.Text.RegularExpressions;
using SoulsFormats;
using YarrToMap.Util;

namespace YarrToMap;

public class TextureManager
{
    public readonly HashSet<string> UsedTextures = new();
    private readonly HashSet<string> _usedMapPieces = new();
    private readonly HashSet<string> _usedObjects = new();
    private readonly HashSet<string> _objectsInMod = new();

    public TextureManager(Options op)
    {
        GetUsedMapPieces(op.ModMapDirectory, op);
        GetUsedMapPieceTextures(op.ModMapDirectory, op);
        GetUsedObjects(op.ModMapDirectory, op);
        GetModObjects(op);
        GetUsedObjectTextures(op.ModObjectDirectory);

        if (_usedMapPieces.Count == 0)
        {
            GetUsedMapPieces(op.GameMapDirectory, op);
        }

        GetUsedMapPieceTextures(op.GameMapDirectory, op);

        if (_usedObjects.Count == 0)
        {
            GetUsedObjects(op.GameMapDirectory, op);
        }
        else
        {
            _usedObjects.ExceptWith(_objectsInMod);
        }

        GetUsedObjectTextures(op.GameObjectDirectory);
    }

    public void GetUsedMapPieceTextures(string mapDirectory, Options op)
    {
        if (Directory.Exists(mapDirectory))
        {
            string? mapPartsDirectory = Directory.GetDirectories(mapDirectory,
                $"m{op.MapId}_{op.BlockId.ToString("D2")}_00_00").FirstOrDefault();
            if (mapPartsDirectory != null)
            {
                foreach (string dcx in Directory.EnumerateFiles(mapPartsDirectory, "*bnd.dcx")
                             .Where(x => _usedMapPieces.Contains(string.Concat("m", Path.GetFileName(x)
                                 .AsSpan(13, 6)))))
                {
                    UsedTextures.AddTexturesFromDcx(dcx);
                }
            }
        }
    }

    public void GetUsedObjectTextures(string objectDirectory)
    {
        if (Directory.Exists(objectDirectory))
        {
            foreach (string dcx in Directory.EnumerateFiles(objectDirectory, "*bnd.dcx")
                         .Where(x => _usedObjects.Contains(Path.GetFileName(x)[..7])))
            {
                UsedTextures.AddTexturesFromDcx(dcx);
            }
        }
    }

    public void GetUsedMapPieces(string mapDirectory, Options op)
    {
        if (Directory.Exists(mapDirectory))
        {
            string? mapStudioDirectory =
                Directory.GetDirectories(mapDirectory).FirstOrDefault(x => x.EndsWith("MapStudio", StringComparison.OrdinalIgnoreCase));
            if (mapStudioDirectory != null)
            {
                string? mapStudioDcx = Directory.GetFiles(mapStudioDirectory)
                    .FirstOrDefault(x =>
                        Path.GetFileName(x).Equals($"m{op.MapId}_{op.BlockId.ToString("D2")}_00_00.msb.dcx"));
                if (mapStudioDcx != null)
                {
                    MSB3 map = MSB3.Read(mapStudioDcx);
                    foreach (MSB3.Part.MapPiece mapPiece in map.Parts.MapPieces)
                    {
                        _usedMapPieces.Add(mapPiece.ModelName);
                    }
                }
            }
        }
    }

    public void GetUsedObjects(string mapDirectory, Options op)
    {
        if (Directory.Exists(mapDirectory))
        {
            string? mapStudioDirectory =
                Directory.GetDirectories(mapDirectory).FirstOrDefault(x => x.EndsWith("MapStudio", StringComparison.OrdinalIgnoreCase));
            if (mapStudioDirectory != null)
            {
                string? mapStudioDcx = Directory.GetFiles(mapStudioDirectory)
                    .FirstOrDefault(x =>
                        Path.GetFileName(x).Equals($"m{op.MapId}_{op.BlockId.ToString("D2")}_00_00.msb.dcx"));
                if (mapStudioDcx != null)
                {
                    MSB3 map = MSB3.Read(mapStudioDcx);
                    foreach (MSB3.Part.Object obj in map.Parts.Objects)
                    {
                        _usedObjects.Add(obj.ModelName);
                    }
                }
            }
        }
    }

    public void GetModObjects(Options op)
    {
        if (Directory.Exists(op.ModObjectDirectory))
        {
            foreach (string path in Directory.EnumerateFiles(op.ModObjectDirectory)
                         .Where(x => _usedObjects.Contains(Path.GetFileName(x)[..7])))
            {
                _objectsInMod.Add(Path.GetFileName(path)[..7]);
            }
        }
    }

    public List<BXF4> GetRegularTextureBinders(string mapDirectory, Options op)
    {
        List<BXF4> regularTextureBinders = new();
        string? mapTexturesDirectory =
            Directory.GetDirectories(mapDirectory).FirstOrDefault(x => x.EndsWith($"m{op.MapId}"));
        if (mapTexturesDirectory != null)
        {
            for (int i = 0; i < 4; i++)
            {
                string? bhdPath = Directory.EnumerateFiles(mapTexturesDirectory).FirstOrDefault(x =>
                    x.ToLower().EndsWith($"m{op.MapId}_000{i}.tpfbhd") ||
                    x.ToLower().EndsWith($"m{op.MapId}_000{i}.tpfbhd.patch"));
                string? bdtPath = Directory.EnumerateFiles(mapTexturesDirectory).FirstOrDefault(x =>
                    x.ToLower().EndsWith($"m{op.MapId}_000{i}.tpfbdt") ||
                    x.ToLower().EndsWith($"m{op.MapId}_000{i}.tpfbdt.patch"));
                if (bhdPath != null && bdtPath != null)
                {
                    if (File.ReadAllBytes(bdtPath).Length > 0)
                    {
                        BXF4 textureBinder = BXF4.Read(bhdPath, bdtPath);
                        if (op.CreateBackups)
                        {
                            textureBinder.Write(bhdPath + ".bak", bdtPath + ".bak");
                        }

                        regularTextureBinders.Add(textureBinder);
                        continue;
                    }

                    if (op.CreateBackups)
                    {
                        new BXF4().Write(bhdPath + ".bak", bdtPath + ".bak");
                    }
                }

                regularTextureBinders.Add(new BXF4());
            }
        }

        return regularTextureBinders;
    }

    public BXF4 GetLightmapBinder(string mapDirectory, Options op)
    {
        BXF4 lightmapBinder = new();
        string? mapTexturesDirectory =
            Directory.GetDirectories(mapDirectory).FirstOrDefault(x => x.EndsWith($"m{op.MapId}"));
        if (mapTexturesDirectory != null)
        {
            string? bhdPath = Directory.EnumerateFiles(mapTexturesDirectory).FirstOrDefault(x =>
                x.ToLower().EndsWith($"gi_env_m{op.MapId}.tpfbhd") ||
                x.ToLower().EndsWith($"gi_env_m{op.MapId}.tpfbhd.patch"));
            string? bdtPath = Directory.EnumerateFiles(mapTexturesDirectory).FirstOrDefault(x =>
                x.ToLower().EndsWith($"gi_env_m{op.MapId}.tpfbdt") ||
                x.ToLower().EndsWith($"gi_env_m{op.MapId}.tpfbdt.patch"));
            if (bhdPath != null && bdtPath != null)
            {
                if (File.ReadAllBytes(bdtPath).Length > 0)
                {
                    lightmapBinder = BXF4.Read(bhdPath, bdtPath);
                    if (op.CreateBackups)
                    {
                        lightmapBinder.Write(bhdPath + ".bak", bdtPath + ".bak");
                    }
                }
                else if (op.CreateBackups)
                {
                    new BXF4().Write(bhdPath + ".bak", bdtPath + ".bak");
                }
            }
        }

        return lightmapBinder;
    }

    public int TransferYarrTextures(IEnumerable<string> files, List<BXF4> modTextureBinders, Options op)
    {
        int transferCount = 0;
        foreach (string filePath in files.Where(x => UsedTextures.Contains(Path.GetFileNameWithoutExtension(x))))
        {
            BinderFile file = CreateTextureBinderFile(filePath);
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            if (fileName.EndsWith("_l"))
            {
                int foundIndex = -1;
                BXF4? targetTextureBinder =
                    modTextureBinders.FirstOrDefault(x => x.SearchForTexture(fileName[..^2], out foundIndex));
                if (targetTextureBinder != null)
                {
                    targetTextureBinder.CopyTextureTo(file, foundIndex + 1);
                }
                else
                {
                    targetTextureBinder =
                        modTextureBinders.First(x => x.Size() == modTextureBinders.Min(y => y.Size()));
                    targetTextureBinder.CopyTextureTo(file, targetTextureBinder.Files.Count);
                }
            }
            else
            {
                int foundIndex = -1;
                BXF4? targetTextureBinder =
                    modTextureBinders.FirstOrDefault(x => x.SearchForTexture(fileName + "_l", out foundIndex));
                if (targetTextureBinder != null)
                {
                    targetTextureBinder.CopyTextureTo(file, foundIndex - 1);
                }
                else
                {
                    targetTextureBinder =
                        modTextureBinders.First(x => x.Size() == modTextureBinders.Min(y => y.Size()));
                    targetTextureBinder.CopyTextureTo(file, targetTextureBinder.Files.Count);
                }
            }

            transferCount++;
            /*BXF4? targetTextureBinder = null;
            int targetFileId = -1;
            if (fileName.Contains("_gi_", StringComparison.OrdinalIgnoreCase))
            {
                targetTextureBinder = envTextureBinder;
                targetTextureBinder.SearchForTexture(Path.GetFileNameWithoutExtension(nonLodFile), out targetFileId);
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    mainTextureBinders[i].SearchForTexture(Path.GetFileNameWithoutExtension(nonLodFile), out targetFileId);
                    if (targetFileId < mainTextureBinders[i].Files.Count) continue;
                    targetTextureBinder = mainTextureBinders[i];
                    break;
                }

                if (targetTextureBinder == null)
                {
                    targetTextureBinder = mainTextureBinders.First(x => x.Size() == mainTextureBinders.Min(y => y.Size()));
                    targetFileId = targetTextureBinder.Files.Count;
                }
            }
            targetTextureBinder.CopyTextureTo(nonLodFile, targetFileId);
            transferCount++;

            string lodFile = nonLodFile.Insert(nonLodFile.IndexOf(".", StringComparison.Ordinal), "_l");
            if (!File.Exists(lodFile)) continue;
            targetTextureBinder.CopyTextureTo(lodFile, targetFileId + 1);
            transferCount++;
        }

        UpdateModTextures(op);*/
            
        }
        return transferCount;
    }

    public int TransferVanillaTextures(BXF4 vanillaTextureBinder, List<BXF4> modTextureBinders, Options op)
    {
        int transferCount = 0;
        foreach (BinderFile file in vanillaTextureBinder.Files.Where(x =>
                     modTextureBinders.All(y => !y.Files.Any(z => z.Name.Equals(x.Name))) &&
                     UsedTextures.Contains(
                         Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(x.Name)))))
        {
            string fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file.Name));
            if (fileName.EndsWith("_l"))
            {
                int foundIndex = -1;
                BXF4? targetTextureBinder =
                    modTextureBinders.FirstOrDefault(x => x.SearchForTexture(fileName[..^2], out foundIndex));
                if (targetTextureBinder != null)
                {
                    targetTextureBinder.CopyTextureTo(file, foundIndex + 1);
                }
                else
                {
                    targetTextureBinder =
                        modTextureBinders.First(x => x.Size() == modTextureBinders.Min(y => y.Size()));
                    targetTextureBinder.CopyTextureTo(file, targetTextureBinder.Files.Count);
                }
            }
            else
            {
                int foundIndex = -1;
                BXF4? targetTextureBinder =
                    modTextureBinders.FirstOrDefault(x => x.SearchForTexture(fileName + "_l", out foundIndex));
                if (targetTextureBinder != null)
                {
                    targetTextureBinder.CopyTextureTo(file, foundIndex - 1);
                }
                else
                {
                    targetTextureBinder =
                        modTextureBinders.First(x => x.Size() == modTextureBinders.Min(y => y.Size()));
                    targetTextureBinder.CopyTextureTo(file, targetTextureBinder.Files.Count);
                }
            }

            transferCount++;
        }

        return transferCount;
    }

    public static BinderFile CreateTextureBinderFile(string filePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        TPF newTpf = new();
        TPF.Texture newTexture = new(fileName, 0, 0, File.ReadAllBytes(filePath));
        if (fileName.EndsWith("_n") || fileName.EndsWith("_n_l"))
        {
            newTexture.Format = 0x6A;
        }
        else if (Regex.IsMatch(fileName, @"[0-9]{8}_m[0-9]{6}_gi_[0-9]{4}_[0-9]{2}_dol_[0-9]{2}"))
        {
            newTexture.Format = 0x66;
            newTpf.Flag2 = 0x03;
        }

        newTpf.Textures.Add(newTexture);

        BinderFile newFile = new(Binder.FileFlags.Flag1, $"{fileName}.tpf.dcx", 
            DCX.Compress(newTpf.Write(), DCX.Type.DCX_DFLT_10000_44_9));
        return newFile;
    }
}