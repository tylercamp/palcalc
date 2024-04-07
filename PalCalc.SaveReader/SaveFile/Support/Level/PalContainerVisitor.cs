using PalCalc.SaveReader.FArchive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Support.Level
{
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
