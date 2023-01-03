using System.Text.RegularExpressions;

namespace YarrToMap;

public class Options
{ 
    public int MapId { get; }
    public int BlockId { get; }
    public string ModMapDirectory { get; }
    public string ModObjectDirectory { get; }
    public string GameMapDirectory { get; }
    public string GameObjectDirectory { get; }
    public string YarrTextureDirectory { get; }
    public bool UsePatch { get; }
    public bool GetVanillaTextures { get; }
    public bool UsePatchEnv { get; }
    public bool GetVanillaTexturesEnv { get; }
    public bool CreateBackups { get; }
    public Options()
    {
        DirectoryInfo? parentDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory[..^1]);
        while (parentDirectory != null)
        {
            if (parentDirectory.Name.Equals("YARR"))
            {
                break;
            }
            parentDirectory = Directory.GetParent(parentDirectory.FullName);
        }

        Console.WriteLine("Enter the map to transfer textures to (ex. 30_0):");
        string? input = Console.ReadLine();
        if (input == null)
        {
            throw new IOException("No input detected.");
        }
        
        Match m = Regex.Match(input, "\\d\\d_\\d");
        if (m.Success)
        {
            MapId = int.Parse(m.Value.Substring(0, 2));
            BlockId = int.Parse(m.Value.Substring(3));
        }
        else
        {
            throw new IOException("Map and block Id was entered incorrectly.");
        }

        UsePatch = YesNoQuestion(
            $"Do you want to use the .patch extension for m{MapId}_000Xs ?",
            $"The .patch extension will be used for m{MapId}_000Xs.",
            $"The .patch extension will not be used for m{MapId}_000Xs.");
        if (!UsePatch)
        {
            GetVanillaTextures = YesNoQuestion(
                $"Do you want used vanilla textures to be included in m{MapId}_000Xs ?",
                $"Used vanilla textures will be included in m{MapId}_000Xs.",
                $"Used vanilla textures will not be included in m{MapId}_000Xs.");
        }
            
        UsePatchEnv = YesNoQuestion(
            $"Do you want to use the .patch extension for gi_env_m{MapId} ?",
            $"The .patch extension will be used for gi_env_m{MapId}.",
            $"The .patch extension will not be used for gi_env_m{MapId}.");
        if (!UsePatchEnv)
        {
            GetVanillaTexturesEnv = YesNoQuestion(
                $"Do you want used vanilla textures to be included in gi_env_m{MapId} ?",
                $"Used vanilla textures will be included in gi_env_m{MapId}.",
                $"Used vanilla textures will not be included in gi_env_m{MapId}.");
        }

        CreateBackups = YesNoQuestion(
            "Should backups be created?",
            "Backups will be created.",
            "Backups will not be created.");
        

        DirectoryInfo? modDirectory = Directory.GetParent(parentDirectory.FullName);
        if (modDirectory == null)
        {
            throw new DirectoryNotFoundException("No mod directory.");
        }
        ModMapDirectory = $"{modDirectory.FullName}\\map";
        ModObjectDirectory = $"{modDirectory.FullName}\\obj";
        
        DirectoryInfo? gameDirectory = Directory.GetParent(modDirectory.FullName);
        if (gameDirectory == null)
        {
            throw new DirectoryNotFoundException("No game directory.");
        }
        GameMapDirectory = $"{gameDirectory.FullName}\\map";
        GameObjectDirectory = $"{gameDirectory.FullName}\\obj";

        YarrTextureDirectory = $"{parentDirectory}\\dds";
    }

    public static bool YesNoQuestion(string question, string resultYes, string resultNo)
    {
        Console.WriteLine(question);
        Console.Write("(y/n): ");
        ConsoleKeyInfo keyInfo = Console.ReadKey();
        while (keyInfo.KeyChar != 'y' && keyInfo.KeyChar != 'n')
        {
            Console.WriteLine("\nInvalid key entered. Try again.");
            Console.Write("(y/n): ");
            keyInfo = Console.ReadKey();
        }
        bool result = keyInfo.KeyChar == 'y';
        Console.WriteLine();
        //Console.WriteLine(result ? resultYes : resultNo);
        return result;
    }
}