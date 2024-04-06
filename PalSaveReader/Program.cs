
using PalSaveReader;
using PalSaveReader.FArchive;
using PalSaveReader.GVAS;
using System.Diagnostics;

var baseFolder = @"C:\Users\algor\AppData\Local\Pal\Saved\SaveGames\76561198963790804";

foreach (var folder in Directory.EnumerateDirectories(baseFolder))
{
    var sw = Stopwatch.StartNew();
    Console.Write(folder + " - ");
    var saveGame = new SaveGame(folder);

    if (!saveGame.IsValid)
    {
        Console.WriteLine("Invalid");
        continue;
    }

    var level = saveGame.Level.ParsedGvas;
    var levelMeta = saveGame.LevelMeta.ParsedGvas;
    var localData = saveGame.LocalData.ParsedGvas;
    var worldOption = saveGame.WorldOption.ParsedGvas;

    Console.WriteLine("Valid (in {0}ms)", sw.ElapsedMilliseconds);

    //if (Path.GetFileName(folder).StartsWith("F0")) Debugger.Break();
}

Console.ReadLine();

class SaveFile
{
    public SaveFile(string basePath, string fileName)
    {
        FullPath = $"{basePath}/{fileName}";
    }

    public bool Exists => File.Exists(FullPath);

    public string FullPath { get; }
    public GvasFile ParsedGvas
    {
        get
        {
            GvasFile result = null;
            CompressedSAV.WithDecompressedSave(FullPath, stream =>
            {
                using (var archiveReader = new FArchiveReader(stream, PalWorldTypeHints.Hints, ""))
                    result = GvasFile.FromFArchive(archiveReader);
            });
            return result;
        }
    }
}

class SaveGame
{
    public SaveGame(string basePath)
    {
        BasePath = basePath;
    }

    public string BasePath { get; }

    public bool IsValid => Level.Exists && LevelMeta.Exists && LocalData.Exists && WorldOption.Exists;

    public SaveFile Level => new SaveFile(BasePath, "Level.sav");
    public SaveFile LevelMeta => new SaveFile(BasePath, "LevelMeta.sav");
    public SaveFile LocalData => new SaveFile(BasePath, "LocalData.sav");
    public SaveFile WorldOption => new SaveFile(BasePath, "WorldOption.sav");

}