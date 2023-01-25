using SoulsFormats;

namespace YarrToMap.Util;

public static class HashSetUtils
{
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
}