using PalCalc.SaveReader.FArchive;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Support.Level
{
    public class PalContainerSlot
    {
        public Guid InstanceId;
        public Guid PlayerId; // note: not actually useful?
        public int SlotIndex;
    }

    /* Note:
     * 
     * In 0.3.3, containers were updated to no longer store empty slots. Each slot now has a `SlotIndex`
     * and each container now has a `SlotNum` property. There's also a `CustomVersionData` byte array,
     * not sure what that's for.
     */

    public class PalContainer
    {
        public string Id { get; set; }
        public int MaxEntries { get; set; }
        public int NumEntries => Slots.Count(s => s.InstanceId != Guid.Empty);

        public List<PalContainerSlot> Slots { get; set; }

        public override string ToString() => $"{Id} ({NumEntries}/{MaxEntries})";
    }

    class PalContainerVisitor : IVisitor
    {
        private static ILogger logger = Log.ForContext<PalContainerVisitor>();

        public List<PalContainer> CollectedContainers { get; set; } = new List<PalContainer>();

        PalContainer workingContainer = null;

        public PalContainerVisitor() : base(".worldSaveData.CharacterContainerSaveData")
        {
        }

        class SlotInstanceIdEmittingVisitor : IVisitor
        {
            public SlotInstanceIdEmittingVisitor(string path) : base(path, "Value.Slots")
            {
                pendingSlot = null;
                numEmitted = 0;
            }

            public Action<PalContainerSlot> OnSlotData;

            PalContainerSlot pendingSlot;
            int numEmitted;

            public override bool Matches(string path) => path.StartsWith(MatchedPath);

            public override IEnumerable<IVisitor> VisitArrayEntryBegin(string path, int index, ArrayPropertyMeta meta)
            {
                if (path != MatchedPath)
                    yield break;
                else
                {
#if DEBUG
                    if (pendingSlot != null)
                        Debugger.Break();
#endif
                    pendingSlot = new PalContainerSlot();
                    pendingSlot.SlotIndex = -1;

                    var vev = new ValueEmittingVisitor(this, ".Slots.SlotIndex");
                    vev.OnValue += (_, v) => pendingSlot.SlotIndex = Convert.ToInt32(v);
                    yield return vev;
                }
            }

            public override void VisitArrayEntryEnd(string path, int index, ArrayPropertyMeta meta)
            {
                if (path == MatchedPath)
                {
#if DEBUG
                    if (pendingSlot == null) Debugger.Break();
#endif

                    if (pendingSlot.SlotIndex < 0) pendingSlot.SlotIndex = numEmitted++;
                    OnSlotData?.Invoke(pendingSlot);
                    pendingSlot = null;
                }
            }

            public override void VisitCharacterContainerPropertyEnd(string path, CharacterContainerDataPropertyMeta meta)
            {
#if DEBUG
                if (pendingSlot == null)
                    Debugger.Break();
#endif

                pendingSlot.InstanceId = meta.InstanceId.Value;
                pendingSlot.PlayerId = meta.PlayerId;
            }
        }

        public override IEnumerable<IVisitor> VisitMapEntryBegin(string path, int index, MapPropertyMeta meta)
        {
            logger.Verbose("map entry begin");

            workingContainer = new PalContainer() { Slots = new List<PalContainerSlot>() };

            var keyIdCollector = new ValueCollectingVisitor(this, ".Key.ID", ".Value.SlotNum");
            keyIdCollector.OnExit += v =>
            {
                workingContainer.Id = v[".Key.ID"].ToString();
                if (v.ContainsKey(".Value.SlotNum")) workingContainer.MaxEntries = Convert.ToInt32(v[".Value.SlotNum"]);
                else workingContainer.MaxEntries = workingContainer.Slots.Count;
            };

            var slotContentIdEmitter = new SlotInstanceIdEmittingVisitor(path);
            slotContentIdEmitter.OnSlotData += slot =>
            {
                workingContainer.Slots.Add(slot);
            };

            yield return keyIdCollector;
            yield return slotContentIdEmitter;
        }

        public override void VisitMapEntryEnd(string path, int index, MapPropertyMeta meta)
        {
            logger.Verbose("map entry end");

            CollectedContainers.Add(workingContainer);
            workingContainer = null;
        }
    }
}
