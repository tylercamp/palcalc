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

    public class MapObjectVisitor : IVisitor
    {
        private static ILogger logger = Log.ForContext<MapObjectVisitor>();

        public List<GvasMapObject> Result { get; } = new List<GvasMapObject>();

        GvasMapObject pendingEntry;
        string[] objectIds;

        private const string K_MAP_OBJECT_ID = ".MapObjectSaveData.MapObjectId";
        private const string K_WORLD_LOCATION = ".MapObjectSaveData.WorldLocation";
        private const string K_MAP_OBJECT_INSTANCE_ID = ".MapObjectSaveData.MapObjectInstanceId";
        private const string K_MAP_OBJECT_CONCRETE_INSTANCE_ID = ".MapObjectSaveData.MapObjectConcreteModelInstanceId";

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

            pendingEntry = new GvasMapObject();

            yield return new ValueCollectingVisitor(this, K_MAP_OBJECT_ID, K_WORLD_LOCATION, K_MAP_OBJECT_INSTANCE_ID, K_MAP_OBJECT_CONCRETE_INSTANCE_ID)
                .WithOnExit(values =>
                {
                    pendingEntry.ObjectId = values.GetValueOrDefault(K_MAP_OBJECT_ID) as string;
                    pendingEntry.WorldLocation = (VectorLiteral)values.GetValueOrElse(K_WORLD_LOCATION, new VectorLiteral());
                    pendingEntry.InstanceId = (Guid)values.GetValueOrElse(K_MAP_OBJECT_INSTANCE_ID, Guid.Empty);
                    pendingEntry.ConcreteModelInstanceId = (Guid)values.GetValueOrElse(K_MAP_OBJECT_CONCRETE_INSTANCE_ID, Guid.Empty);
                });

            yield return new PropertyEmittingVisitor<MapModelDataProperty>(this, ".MapObjectSaveData.Model.RawData")
                .WithOnValue((path, prop) =>
                {
                    pendingEntry.OwnerBaseId = prop.BaseCampIdBelongTo;
                    pendingEntry.OwnerGroupId = prop.GroupIdBelongTo;
                    pendingEntry.BuilderPlayerId = prop.BuildPlayerUid;
                    pendingEntry.CurrentHP = prop.CurrentHp;
                    pendingEntry.MaxHP = prop.MaxHp;
                });

            yield return new MapContainerCollectingVisitor()
                .WithOnExit(containerId => pendingEntry.PalContainerId = containerId);
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

            if (objectIds.Length == 0 || objectIds.Contains(pendingEntry.ObjectId))
                Result.Add(pendingEntry);

            pendingEntry = null;
        }
    }
}
