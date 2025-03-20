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
    // (just for debugging)
    [Flags]
    public enum BaseDataSource
    {
        Key,
        WorkerDirector,
        BaseData,

        All = Key | WorkerDirector | BaseData
    }

    public class GvasBaseInstance
    {
        // (value appears in Org BaseIds prop)
        public string Id { get; set; }

        // BaseCamp data
        public VectorLiteral Position { get; set; }
        public byte State { get; set; }
        public Guid OwnerGroupId { get; set; }
        public Guid OwnerMapObjectInstanceId { get; set; }

        // WorkerDirector data
        public byte CurrentOrderType { get; set; }
        public Guid ContainerId { get; set; }

        public BaseDataSource DataSources { get; set; }
    }

    internal class BaseVisitor : IVisitor
    {
        private static ILogger logger = Log.ForContext<BaseVisitor>();

        public BaseVisitor() : base(".worldSaveData.BaseCampSaveData")
        {
        }

        public List<GvasBaseInstance> Result { get; } = new List<GvasBaseInstance>();

        private GvasBaseInstance pendingInstance;

        public override IEnumerable<IVisitor> VisitMapEntryBegin(string path, int index, MapPropertyMeta meta)
        {
            if (pendingInstance != null)
            {
#if DEBUG
                Debugger.Break();
#endif

                logger.Warning("Started a new Base entry before the previous Base was finished");
            }

            pendingInstance = new GvasBaseInstance();

            yield return new ValueEmittingVisitor(this, isCaseSensitive: true, ".Key")
                .WithOnValue((path, value) =>
                {
                    pendingInstance.Id = value.ToString();
                    pendingInstance.DataSources |= BaseDataSource.Key;
                });

            yield return new PropertyEmittingVisitor<BaseCampDataProperty>(this, isCaseSensitive: true, ".Value.RawData")
                .WithOnValue((path, value) =>
                {
                    pendingInstance.Position = value.Transform.Translation;
                    pendingInstance.OwnerMapObjectInstanceId = value.OwnerMapObjectInstanceId;
                    pendingInstance.OwnerGroupId = value.GroupIdBelongTo;
                    pendingInstance.State = value.State;
                    pendingInstance.DataSources |= BaseDataSource.BaseData;
                });

            yield return new PropertyEmittingVisitor<WorkerDirectorDataProperty>(this, isCaseSensitive: true, ".Value.WorkerDirector.RawData")
                .WithOnValue((path, value) =>
                {
                    pendingInstance.ContainerId = value.ContainerId;
                    pendingInstance.CurrentOrderType = value.CurrentOrderType;
                    pendingInstance.DataSources |= BaseDataSource.WorkerDirector;
                });
        }

        public override void VisitMapEntryEnd(string path, int index, MapPropertyMeta meta)
        {
            if (pendingInstance == null)
            {
#if DEBUG
                Debugger.Break();
#endif
                logger.Warning("Reached end of Base entry but no data was being tracked");
                return;
            }

            Result.Add(pendingInstance);
            pendingInstance = null;
        }
    }
}
