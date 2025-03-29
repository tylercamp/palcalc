
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.SaveReader.FArchive;
using PalCalc.SaveReader.FArchive.Custom;
using PalCalc.SaveReader.GVAS;
using PalCalc.SaveReader.SaveFile;
using PalCalc.SaveReader.SaveFile.Support.Level;
using System.Diagnostics;
using System.Net.Http.Headers;

Logging.InitCommonFull();

var db = PalDB.LoadEmbedded();

//var locs = XboxSavesLocation.FindAll();

//CompressedSAV.WithDecompressedSave(@"C:\Users\algor\AppData\Local\Packages\PocketpairInc.Palworld_ad4psfrxyesvt\SystemAppData\wgs\0009000009374154_0000000000000000000000006B210A9C\9E4FC968024C4FF8989588B179E6E82F\DF1AB54D4A0B442C99F0D3790F50811A", s =>
//{
//    using (var f = new FArchiveReader(s, PalWorldTypeHints.Hints))
//    {
//        var r = GvasFile.FromFArchive(f, []);
//    }

//});

//CompressedSAV.WithDecompressedSave(
//    [
//        @"C:\Users\algor\Downloads\palworld_2533274945012052_2025-01-25_16_45_56\F76A9C474B9CDEE155A6C6A38FCCBD39\Level\01.sav",
//        @"C:\Users\algor\Downloads\palworld_2533274945012052_2025-01-25_16_45_56\F76A9C474B9CDEE155A6C6A38FCCBD39\Level\02.sav"
//    ],
//    stream =>
//    {
//        using (var fa = new FArchiveReader(stream, PalWorldTypeHints.Hints, archivePreserve: true))
//        {
//            var gvas = GvasFile.FromFArchive(fa, []);
//        }
//    }
//);

//CompressedSAV.WithDecompressedSave(@"C:\Users\algor\Downloads\CRASHLOG(6)\save-0\Level.sav", stream =>
//{
//    using (var f = new FileStream("decompressed", FileMode.Create))
//        stream.CopyTo(f);
//});
//var save4 = new StandardSaveGame(@"C:\Users\algor\Downloads\CRASHLOG(6)\save-0");
//var level4 = save4.Level.ParseGvas(true);

//Environment.Exit(0);

var dsl = new DirectSavesLocation(@"C:\Users\algor\AppData\Local\Pal\Saved\SaveGames\76561198963790804");
var gps = dsl.GlobalPalStorage.ParseGvas(true);
var gps2 = dsl.GlobalPalStorage.ReadPals("GPS");

var save3 = new StandardSaveGame(@"C:\Users\algor\AppData\Local\Pal\Saved\SaveGames\76561198963790804\095144A9430A880B9D995A8C8777547F");

var v2 = new MapObjectVisitor(GvasMapObject.GlobalPalBoxObjectId, GvasMapObject.DimensionalPalStorageObjectId);
var level3 = save3.Level.ParseGvas(true, v2);
var dataLevel3 = save3.Level.ReadCharacterData(db, GameSettings.Defaults, save3.Players, dsl.GlobalPalStorage);
var rawDataLevel3 = save3.Level.ReadRawCharacterData();

foreach (var pl in save3.Players)
{
    if (pl.DimensionalPalStorageSaveFile != null)
    {
        var plgvas = pl.ParseGvas(true);
        var dps = pl.DimensionalPalStorageSaveFile.ParseGvas(true);
        var dpsChars = pl.DimensionalPalStorageSaveFile.ReadRawCharacters();
    }
}

var d = level3.Dynamic;
var t = d.worldSaveData.MapObjectSaveData[0].ConcreteModel;

var mapObjects = (IEnumerable<dynamic>)level3.Dynamic.worldSaveData.MapObjectSaveData.ToEnumerable();

foreach (var obj in mapObjects.Where(o => ((IEnumerable<dynamic>)o.ConcreteModel.ModuleMap.ToEnumerable()).Any(m => m.Key == "EPalMapObjectConcreteModelModuleType::CharacterContainer")))
{
    foreach (var mod in (IEnumerable<dynamic>)obj.ConcreteModel.ModuleMap.ToEnumerable())
    {
        var k = mod.Key;
        var v = mod.Value;
    }
    Debugger.Break();
}

var e = level3.Dynamic.worldSaveData.MapObjectSaveData[0].ConcreteModel.ModuleMap.ToEnumerable();



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

var chars = save2.Level.ReadCharacterData(PalDB.LoadEmbedded(), GameSettings.Defaults, save2.Players, null);
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
        var characters = save.Level.ReadCharacterData(db, GameSettings.Defaults, save.Players, gameFolder.GlobalPalStorage);
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