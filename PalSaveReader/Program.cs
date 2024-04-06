
using PalSaveReader;
using PalSaveReader.FArchive;
using PalSaveReader.GVAS;
using System.Diagnostics;
using System.Net.Http.Headers;

var baseFolder = @"C:\Users\algor\AppData\Local\Pal\Saved\SaveGames\76561198963790804";

foreach (var folder in Directory.EnumerateDirectories(baseFolder))
{
    var saveVisitors = new List<IVisitor>()
    {
        new PalContainerVisitor(),
    };

    var metaVisitor = new List<IVisitor>()
    {
        new ValueCollectingVisitor(".SaveData", ".WorldName", ".HostPlayerName", ".HostPlayerLevel"),
    };

    var sw = Stopwatch.StartNew();
    Console.Write(folder + " - ");
    var saveGame = new SaveGame(folder);

    if (!saveGame.IsValid)
    {
        Console.WriteLine("Invalid");
        continue;
    }

    var level = saveGame.Level.ParsedGvas(saveVisitors);
    var levelMeta = saveGame.LevelMeta.ParsedGvas(metaVisitor);
    var localData = saveGame.LocalData.ParsedGvas(new List<IVisitor>());
    var worldOption = saveGame.WorldOption.ParsedGvas(new List<IVisitor>());

    Console.WriteLine("Valid (in {0}ms)", sw.ElapsedMilliseconds);

    if (Path.GetFileName(folder).StartsWith("F0")) Debugger.Break();
}

Console.ReadLine();

class PalContainer
{
    public string Id { get; set; }
    public int MaxEntries { get; set; }
    public int NumEntries { get; set; }

    public override string ToString() => $"{Id} ({NumEntries}/{MaxEntries})";
}

class PalContainerVisitor : IVisitor
{
    public List<PalContainer> CollectedContainers { get; set; } = new List<PalContainer>();

    PalContainer workingContainer = null;

    public PalContainerVisitor() : base(".worldSaveData.CharacterContainerSaveData")
    {
    }

    class SlotInstanceIdEmittingVisitor : IVisitor
    {
        public SlotInstanceIdEmittingVisitor(string path) : base(path, "Value.Slots.Slots.RawData") { }

        public Action<Guid> OnInstanceId;

        public override void VisitCharacterContainerPropertyEnd(string path, CharacterContainerDataPropertyMeta meta)
        {
            OnInstanceId?.Invoke(meta.InstanceId.Value);
        }
    }

    public override IEnumerable<IVisitor> VisitMapEntryBegin(string path, int index, MapPropertyMeta meta)
    {
        workingContainer = new PalContainer();

        var keyIdCollector = new ValueCollectingVisitor(this, ".Key.ID");
        keyIdCollector.OnExit += v => workingContainer.Id = v[".Key.ID"].ToString();

        var slotContentIdEmitter = new SlotInstanceIdEmittingVisitor(path);
        slotContentIdEmitter.OnInstanceId += id =>
        {
            workingContainer.MaxEntries++;
            if (id != Guid.Empty) workingContainer.NumEntries++;
        };

        // ".Value.Slots.Slots.IndividualId"

        //var slotValueIdCollector = new VisitorCollectingVisitor()

        yield return keyIdCollector;
        yield return slotContentIdEmitter;
        //yield return 

    }

    public override void VisitMapEntryEnd(string path, int index, MapPropertyMeta meta)
    {
        CollectedContainers.Add(workingContainer);
        workingContainer = null;
    }
}

class SaveFile
{
    public SaveFile(string basePath, string fileName)
    {
        FullPath = $"{basePath}/{fileName}";
    }

    public bool Exists => File.Exists(FullPath);

    public string FullPath { get; }
    public GvasFile ParsedGvas(IEnumerable<IVisitor> visitors)
    {
        GvasFile result = null;
        CompressedSAV.WithDecompressedSave(FullPath, stream =>
        {
            using (var archiveReader = new FArchiveReader(stream, PalWorldTypeHints.Hints))
                result = GvasFile.FromFArchive(archiveReader, visitors);
        });
        return result;
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