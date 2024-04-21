using PalCalc.SaveReader.FArchive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Support.Level
{
    struct PalContainerSlot
    {
        public Guid InstanceId;
        public Guid PlayerId;
    }

    class PalContainer
    {
        public string Id { get; set; }
        public int MaxEntries => Slots.Count;
        public int NumEntries => Slots.Count(s => s.InstanceId != Guid.Empty);

        public List<PalContainerSlot> Slots { get; set; }

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

            public Action<PalContainerSlot> OnSlotData;

            public override void VisitCharacterContainerPropertyEnd(string path, CharacterContainerDataPropertyMeta meta)
            {
                OnSlotData?.Invoke(new PalContainerSlot()
                {
                    InstanceId = meta.InstanceId.Value,
                    PlayerId = meta.PlayerId,
                });
            }
        }

        public override IEnumerable<IVisitor> VisitMapEntryBegin(string path, int index, MapPropertyMeta meta)
        {
            workingContainer = new PalContainer() { Slots = new List<PalContainerSlot>() };

            var keyIdCollector = new ValueCollectingVisitor(this, ".Key.ID");
            keyIdCollector.OnExit += v => workingContainer.Id = v[".Key.ID"].ToString();

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
            CollectedContainers.Add(workingContainer);
            workingContainer = null;
        }
    }
}
