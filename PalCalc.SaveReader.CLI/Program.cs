
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

var save2 = new StandardSaveGame(@"C:\Users\algor\Desktop\Bad Loc");
var level2 = save2.Level.ParseGvas(true);

var charsGvas = level2.Collect(".worldSaveData.CharacterSaveParameterMap").Cast<MapProperty>().Single().Value;
foreach (var c in charsGvas)
{
    var props = ((c.Value as Dictionary<string, object>)["RawData"] as CharacterDataProperty).Data["SaveParameter"] as StructProperty;
    var slot = (props.Value as Dictionary<string, object>).GetValueOrDefault("SlotID") as StructProperty;

    var containerIdProp = (slot?.Value as Dictionary<string, object>)?.GetValueOrDefault("ContainerId") as StructProperty;

    var containerIdValueProp = (containerIdProp?.Value as Dictionary<string, object>)?.GetValueOrDefault("ID") as StructProperty;
    var containerId = (containerIdValueProp?.Value?.ToString());


}

var containersGvas = level2.Collect(".worldSaveData.CharacterContainerSaveData").Cast<MapProperty>().Single().Value;

foreach (var kvp in containersGvas)
{
    var keyProps = kvp.Key as Dictionary<string, object>;

    var id = (keyProps["ID"] as StructProperty).Value;
    var valueProps = kvp.Value as Dictionary<string, object>;

    var refSlot = (valueProps["bReferenceSlot"] as LiteralProperty).Value;
    var slots = (valueProps["Slots"] as ArrayProperty).Values<object>().Cast<Dictionary<string, object>>().ToList();

    foreach (var slot in slots)
    {
        var iid = (slot["IndividualId"] as StructProperty).Value as Dictionary<string, object>;
        var instanceId = (iid["InstanceId"] as StructProperty).Value;

        var rawData = (slot["RawData"] as CharacterContainerDataProperty).TypedMeta;
        var rawInstanceId = rawData.InstanceId;

        if (instanceId?.ToString() == "c060ecd3-6ced-415f-94a8-30e9e0ee5ef0" || rawInstanceId?.ToString() == "c060ecd3-6ced-415f-94a8-30e9e0ee5ef0")
            Debugger.Break();
    }
}

var chars = save2.Level.ReadCharacterData(PalDB.LoadEmbedded(), save2.Players);
var p = chars.Pals.Where(p => p.Pal.Name == "Jetragon" && p.PassiveSkills.Select(t => t.Name).Intersect(new string[] { "Legend", "Nimble", "Runner", "Swift" }).Count() == 4).Single();

return;

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