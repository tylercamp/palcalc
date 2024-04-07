
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.SaveReader.FArchive;
using PalCalc.SaveReader.GVAS;
using System.Diagnostics;
using System.Net.Http.Headers;

var baseFolder = @"C:\Users\algor\AppData\Local\Pal\Saved\SaveGames\76561198963790804";
var db = PalDB.LoadFromCommon();

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

    var level = saveGame.Level.ParseGvas();
    var levelMeta = saveGame.LevelMeta.ParseGvas();
    var localData = saveGame.LocalData.ParseGvas();
    var worldOption = saveGame.WorldOption.ParseGvas();

    var palInstances = saveGame.Level.ReadPalInstances(db);
    var worldMeta = saveGame.LevelMeta.ReadGameOptions();

    Console.WriteLine("Valid (in {0}ms)", sw.ElapsedMilliseconds);

    //if (Path.GetFileName(folder).StartsWith("F0")) Debugger.Break();
}

Console.ReadLine();

