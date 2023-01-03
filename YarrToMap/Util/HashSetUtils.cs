using SoulsFormats;
using System;

namespace YarrToMap.Util;

public static class HashSetUtils
{
    public static void AddUsedTexturesFromFolder(this HashSet<string> currentTextureSet, string folder)
    {
        IEnumerable<string> mapPieceDcxFiles = Directory.EnumerateFiles(folder, "*bnd.dcx");
        foreach (string dcx in mapPieceDcxFiles)
        {
            currentTextureSet.AddTexturesFromDcx(dcx);
        }
    }

    public static void AddUsedObjectTexturesFromFolder(this HashSet<string> currentTextureSet, HashSet<string> usedObjects, string folder)
    {
        IEnumerable<string> dcxFiles = Directory.EnumerateFiles(folder, "*bnd.dcx");
        foreach (string dcx in dcxFiles.Where(x => usedObjects.Contains(Path.GetFileName(x).Substring(0, 7))))
        {
            currentTextureSet.AddTexturesFromDcx(dcx);
        }
    }
    
    public static void AddUsedMapPieceTexturesFromFolder(this HashSet<string> currentTextureSet, HashSet<string> usedMapPieces, string folder)
    {
        IEnumerable<string> dcxFiles = Directory.EnumerateFiles(folder, "*bnd.dcx");
        foreach (string dcx in dcxFiles.Where(x => usedMapPieces.Contains(string.Concat("m", Path.GetFileName(x).AsSpan(13, 6)))))
        {
            currentTextureSet.AddTexturesFromDcx(dcx);
        }
    }

    public static void AddTexturesFromDcx(this HashSet<string> currentTextureSet, string dcx)
    {
        BND4 bnd = BND4.Read(dcx);

        HashSet<string> localTextures = new();
        BinderFile? tpfFile = bnd.Files.FirstOrDefault(x => TPF.Is(x.Bytes));
        if (tpfFile != null)
        {
            TPF tpf = TPF.Read(tpfFile.Bytes);
            foreach (TPF.Texture tex in tpf.Textures)
            {
                localTextures.Add(Path.GetFileNameWithoutExtension(tex.Name));
            }
        }

        BinderFile? flverFile = bnd.Files.FirstOrDefault(x => x.Name.EndsWith(".flver"));
        if (flverFile != null)
        {
            FLVER2 model = FLVER2.Read(flverFile.Bytes);
            foreach (FLVER2.Material mat in model.Materials)
            {
                foreach (FLVER2.Texture tex in mat.Textures.Where(x => x.Path.Length > 0 && !localTextures.Contains(Path.GetFileNameWithoutExtension(x.Path))))
                {
                    currentTextureSet.Add(Path.GetFileNameWithoutExtension(tex.Path));
                    currentTextureSet.Add(Path.GetFileNameWithoutExtension(tex.Path) + "_l");
                }
            }
        }
    }

    public static HashSet<string> GetSetOfUsedObjects(string mapFolder, int mapId, int blockId)
    {
        HashSet<string> usedObjects = new();
        if (Directory.Exists(mapFolder))
        {
            string? mapStudioDirectory = Directory.GetDirectories(mapFolder, 
                $"MapStudio").FirstOrDefault();
            if (mapStudioDirectory != null)
            {
                string tester = $"m{mapId}_{blockId.ToString("D2")}_00_00.msb.dcx";
                IEnumerable<string> files = Directory.GetFiles(mapStudioDirectory);
                string? mapStudioDcx = files.FirstOrDefault(x => Path.GetFileName(x).Equals(tester));
                if (mapStudioDcx != null)
                {
                    MSB3 map = MSB3.Read(mapStudioDcx);
                    foreach (MSB3.Part.Object obj in map.Parts.Objects)
                    {
                        usedObjects.Add(obj.ModelName);
                    }
                }
            }
        }
        return usedObjects;
    }
    
    public static HashSet<string> GetSetOfUsedMapPieces(string mapFolder, int mapId, int blockId)
    {
        HashSet<string> usedMapPieces = new();
        if (!Directory.Exists(mapFolder)) return usedMapPieces;
        string? mapStudioDirectory = Directory.GetDirectories(mapFolder, "MapStudio").FirstOrDefault();
        if (mapStudioDirectory != null)
        {
            string tester = $"m{mapId}_{blockId.ToString("D2")}_00_00.msb.dcx";
            IEnumerable<string> files = Directory.GetFiles(mapStudioDirectory);
            string? mapStudioDcx = files.FirstOrDefault(x => Path.GetFileName(x).Equals(tester));
            if (mapStudioDcx != null)
            {
                MSB3 map = MSB3.Read(mapStudioDcx);
                foreach (MSB3.Part.MapPiece mapPiece in map.Parts.MapPieces)
                {
                    usedMapPieces.Add(mapPiece.ModelName);
                }
            }
        }
        return usedMapPieces;
    }
}