using PalCalc.Model;
using PalCalc.SaveReader.FArchive;
using PalCalc.SaveReader.FArchive.Custom;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Support.Level
{
    public class GvasMapObject
    {
        // general MapObjectSaveData properties
        public VectorLiteral WorldLocation { get; set; }
        public string ObjectId { get; set; } // e.g. "PalBoxV2"

        public Guid InstanceId { get; set; }
        public Guid ConcreteModelInstanceId { get; set; }

        // Model properties
        public Guid OwnerBaseId { get; set; }
        public Guid OwnerGroupId { get;set; }
        public Guid BuilderPlayerId { get; set; }

        public int CurrentHP { get; set; }
        public int MaxHP { get; set; }

        public Guid? PalContainerId { get; set; }

        public static readonly string PalBoxObjectId = "PalBoxV2";
        public static readonly string ViewingCageObjectId = "DisplayCharacter";
        public static readonly string GlobalPalBoxObjectId = "GlobalPalStorage";
        public static readonly string DimensionalPalStorageObjectId = "DimensionPalStorage";
    }

    class MapContainerCollectingVisitor : IVisitor
    {
        private ILogger logger = Log.ForContext<MapContainerCollectingVisitor>();

        public MapContainerCollectingVisitor() : base(".worldSaveData.MapObjectSaveData.MapObjectSaveData")
        {

        }

        public override bool Matches(string path) => path.StartsWith(MatchedBasePath);

        bool collectingContainerId = false;
        byte[] pendingContainerId;

        public override void VisitString(string path, string value)
        {
            if (path != $"{MatchedBasePath}.ConcreteModel.ModuleMap.Key")
                return;

            if (value == "EPalMapObjectConcreteModelModuleType::CharacterContainer")
            {
                collectingContainerId = true;
            }
        }

        public override void VisitByteArray(string path, byte[] value)
        {
            if (collectingContainerId && value.Length > 0)
                pendingContainerId = value;
        }

        public override void VisitArrayPropertyEnd(string path, ArrayPropertyMeta meta)
        {
            collectingContainerId = false;
        }

        public event Action<Guid?> OnExit;
        public IVisitor WithOnExit(Action<Guid?> onExit)
        {
            OnExit += onExit;
            return this;
        }

        public override void Exit()
        {
            if (pendingContainerId == null)
            {
                OnExit?.Invoke(null);
            }
            else
            {
                try
                {
                    OnExit?.Invoke(FArchiveReader.ParseGuid(pendingContainerId));
                }
                catch (Exception e)
                {
#if DEBUG
                    Debugger.Break();
#endif
                    logger.Warning(e, "Error while trying to parse map object container ID");
                    OnExit?.Invoke(null);
                }
            }
        }
    }

    // Collect until the ID is known, then suppress irrelevant entry callbacks.
    // This also works when model/container fields precede MapObjectId.
    class MapObjectEntryVisitor : MapContainerCollectingVisitor
    {
        private const string EntryPath = ".worldSaveData.MapObjectSaveData.MapObjectSaveData";
        private const string ObjectIdPath = EntryPath + ".MapObjectId";
        private readonly string[] objectIds;
        private bool rejected;

        public GvasMapObject Result { get; } = new() { WorldLocation = new VectorLiteral() };

        public MapObjectEntryVisitor(string[] objectIds)
        {
            this.objectIds = objectIds;
            OnExit += id => Result.PalContainerId = id;
        }

        public override bool Matches(string path) =>
            (!rejected && path.StartsWith(EntryPath, StringComparison.OrdinalIgnoreCase))
            || string.Equals(path, ObjectIdPath, StringComparison.OrdinalIgnoreCase);

        public override void VisitString(string path, string value)
        {
            if (string.Equals(path, ObjectIdPath, StringComparison.OrdinalIgnoreCase))
            {
                Result.ObjectId = value;
                rejected = objectIds.Length > 0 && !objectIds.Contains(value);
            }
            else base.VisitString(path, value);
        }

        public override void VisitVector(string path, VectorLiteral value)
        {
            if (string.Equals(path, EntryPath + ".WorldLocation", StringComparison.OrdinalIgnoreCase))
                Result.WorldLocation = value;
        }

        public override void VisitGuid(string path, Guid value)
        {
            if (string.Equals(path, EntryPath + ".MapObjectInstanceId", StringComparison.OrdinalIgnoreCase))
                Result.InstanceId = value;
            else if (string.Equals(path, EntryPath + ".MapObjectConcreteModelInstanceId", StringComparison.OrdinalIgnoreCase))
                Result.ConcreteModelInstanceId = value;
        }

        public override void VisitMapModelProperty(string path, MapModelDataProperty prop)
        {
            if (path != EntryPath + ".Model.RawData") return;
            Result.OwnerBaseId = prop.BaseCampIdBelongTo;
            Result.OwnerGroupId = prop.GroupIdBelongTo;
            Result.BuilderPlayerId = prop.BuildPlayerUid;
            Result.CurrentHP = prop.CurrentHp;
            Result.MaxHP = prop.MaxHp;
        }
    }

    public class MapObjectVisitor : IVisitor
    {
        private static ILogger logger = Log.ForContext<MapObjectVisitor>();

        public List<GvasMapObject> Result { get; } = new List<GvasMapObject>();

        MapObjectEntryVisitor pendingEntry;
        string[] objectIds;

        public MapObjectVisitor(params string[] collectedObjectIds) : base(".worldSaveData.MapObjectSaveData")
        {
            objectIds = collectedObjectIds;
        }

        public override IEnumerable<IVisitor> VisitArrayEntryBegin(string path, int index, ArrayPropertyMeta meta)
        {
            if (pendingEntry != null)
            {
#if DEBUG
                Debugger.Break();
#endif
                logger.Warning("Starting new map object entry but the previous entry wasn't finished");
            }

            pendingEntry = new MapObjectEntryVisitor(objectIds);
            yield return pendingEntry;
        }

        public override void VisitArrayEntryEnd(string path, int index, ArrayPropertyMeta meta)
        {
            if (pendingEntry == null)
            {
#if DEBUG
                Debugger.Break();
#endif
                logger.Warning("Reached end of map object entry but no data was being tracked");
                return;
            }

            if (objectIds.Length == 0 || objectIds.Contains(pendingEntry.Result.ObjectId))
                Result.Add(pendingEntry.Result);

            pendingEntry = null;
        }
    }
}
