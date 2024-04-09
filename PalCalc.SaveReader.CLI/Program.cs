
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.SaveReader.FArchive;
using PalCalc.SaveReader.GVAS;
using System.Diagnostics;
using System.Net.Http.Headers;

var db = PalDB.LoadEmbedded();

foreach (var gameFolder in SavesLocation.AllLocal)
{
    Console.WriteLine("Checking game folder {0}", gameFolder.FolderName);

    foreach (var save in gameFolder.ValidSaveGames)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine("Checking save folder {0}", save.FolderName);

        var meta = save.LevelMeta.ReadGameOptions();
        var pals = save.Level.ReadPalInstances(db);

        Console.WriteLine(meta);
        Console.WriteLine("{0} owned pals", pals.Count);

        var palsByLocation = pals.GroupBy(p => p.Location.Type).ToDictionary(g => g.Key, g => g.ToList());

        Console.WriteLine("- {0} in party", palsByLocation.GetValueOrDefault(LocationType.PlayerParty)?.Count ?? 0);
        Console.WriteLine("- {0} in pal box", palsByLocation.GetValueOrDefault(LocationType.Palbox)?.Count ?? 0);
        Console.WriteLine("- {0} in bases", palsByLocation.GetValueOrDefault(LocationType.Base)?.Count ?? 0);
        Console.WriteLine("(took {0}ms)", sw.ElapsedMilliseconds);
    }
}

Console.ReadLine();

