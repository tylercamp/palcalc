
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.SaveReader.FArchive;
using PalCalc.SaveReader.GVAS;
using System.Diagnostics;
using System.Net.Http.Headers;

Logging.InitCommonFull();

var db = PalDB.LoadEmbedded();

var saveFolders = new List<ISavesLocation>();
saveFolders.AddRange(DirectSavesLocation.AllLocal);
saveFolders.AddRange(XboxSavesLocation.FindAll());

foreach (var gameFolder in saveFolders)
{
    Console.WriteLine("Checking game folder {0}", gameFolder.FolderName);

    foreach (var save in gameFolder.ValidSaveGames.OrderByDescending(g => g.LastModified))
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine("Checking save folder {0}", save.BasePath);

        if (!save.IsValid)
        {
            Debugger.Break();
            var x = save.IsValid;
        }

        var metagvas = save.LevelMeta.ParseGvas(true);
        var localgvas = save.LocalData.ParseGvas(true);

        var meta = save.LevelMeta.ReadGameOptions();
        var characters = save.Level.ReadCharacterData(db, save.Players);
        var gvas = save.Level.ParseGvas(true);

        var visitor = new ReferenceCollectingVisitor();
        //save.Level.ParseGvas()

        Console.WriteLine(meta);
        Console.WriteLine("{0} owned pals", characters.Pals.Count);

        var palsByLocation = characters.Pals.GroupBy(p => p.Location.Type).ToDictionary(g => g.Key, g => g.ToList());

        Console.WriteLine("- {0} in party", palsByLocation.GetValueOrDefault(LocationType.PlayerParty)?.Count ?? 0);
        Console.WriteLine("- {0} in pal box", palsByLocation.GetValueOrDefault(LocationType.Palbox)?.Count ?? 0);
        Console.WriteLine("- {0} in bases", palsByLocation.GetValueOrDefault(LocationType.Base)?.Count ?? 0);
        Console.WriteLine("(took {0}ms)", sw.ElapsedMilliseconds);
    }
}

Console.ReadLine();

class ReferenceCollectingVisitor : IVisitor
{
    List<string> observedPaths = new List<string>();
    Guid[] ids;
    public ReferenceCollectingVisitor(params Guid[] ids) : base("")
    {
        this.ids = ids;
    }

    public override bool Matches(string path) => true;

    public override void VisitGuid(string path, Guid guid)
    {
        base.VisitGuid(path, guid);
        if (ids.Contains(guid))
            observedPaths.Add(path);
    }
}