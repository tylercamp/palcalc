using PalCalc.SaveReader.FArchive;
using PalCalc.SaveReader.FArchive.Custom;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Foundation.Metadata;

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
    }

    public class MapObjectVisitor : IVisitor
    {
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
                Debugger.Break();
            }

            pendingEntry = new GvasMapObject();

            yield return new ValueCollectingVisitor(this, K_MAP_OBJECT_ID, K_WORLD_LOCATION, K_MAP_OBJECT_INSTANCE_ID, K_MAP_OBJECT_CONCRETE_INSTANCE_ID)
                .WithOnExit(values =>
                {
                    pendingEntry.ObjectId = (string)values[K_MAP_OBJECT_ID];
                    pendingEntry.WorldLocation = (VectorLiteral)values[K_WORLD_LOCATION];
                    pendingEntry.InstanceId = (Guid)values[K_MAP_OBJECT_INSTANCE_ID];
                    pendingEntry.ConcreteModelInstanceId = (Guid)values[K_MAP_OBJECT_CONCRETE_INSTANCE_ID];
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
        }

        public override void VisitArrayEntryEnd(string path, int index, ArrayPropertyMeta meta)
        {
            if (pendingEntry == null)
            {
                Debugger.Break();
            }

            if (objectIds.Length == 0 || objectIds.Contains(pendingEntry.ObjectId))
                Result.Add(pendingEntry);

            pendingEntry = null;

            base.VisitArrayEntryEnd(path, index, meta);
        }
    }
}
