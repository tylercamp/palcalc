using PalCalc.SaveReader.FArchive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Support.Level
{
    class GvasPalInstance
    {
        public string CharacterId { get; set; }
        public Guid ContainerId { get; set; }
        public int SlotIndex { get; set; }
        public string Gender { get; set; }

        public List<string> Traits { get; set; }
    }

    class PalInstanceVisitor : IVisitor
    {
        public PalInstanceVisitor() : base(".worldSaveData.CharacterSaveParameterMap.Value.RawData.SaveParameter") { }

        public List<GvasPalInstance> Result = new List<GvasPalInstance>();

        GvasPalInstance pendingInstance = null;

        public override IEnumerable<IVisitor> VisitStructPropertyBegin(string path, StructPropertyMeta meta)
        {
            if (meta.StructType != "PalIndividualCharacterSaveParameter") yield break;

            pendingInstance = new GvasPalInstance();

            var collectingVisitor = new ValueCollectingVisitor(this,
                ".CharacterID",
                ".IsPlayer",
                ".Gender",
                ".SlotID.ContainerId.ID",
                ".SlotID.SlotIndex"
            );

            collectingVisitor.OnExit += (vals) =>
            {
                if (vals.ContainsKey(".IsPlayer") && (bool)vals[".IsPlayer"] == true)
                {
                    pendingInstance = null;
                    return;
                }

                var characterId = vals[".CharacterID"] as string;

                if (characterId.StartsWith("Hunter_") || characterId.StartsWith("SalesPerson_"))
                {
                    pendingInstance = null;
                    return;
                }

                pendingInstance.CharacterId = characterId;

                pendingInstance.Gender = vals[".Gender"].ToString();
                pendingInstance.ContainerId = (Guid)vals[".SlotID.ContainerId.ID"];
                pendingInstance.SlotIndex = (int)vals[".SlotID.SlotIndex"];
            };

            yield return collectingVisitor;

            List<string> traits = new List<string>();
            var traitsVisitor = new ValueEmittingVisitor(this, ".PassiveSkillList");
            traitsVisitor.OnValue += (_, v) =>
            {
                traits.Add(v.ToString());
            };

            traitsVisitor.OnExit += () =>
            {
                if (pendingInstance != null) pendingInstance.Traits = traits;
            };

            yield return traitsVisitor;
        }

        public override void VisitStructPropertyEnd(string path, StructPropertyMeta meta)
        {
            if (meta.StructType != "PalIndividualCharacterSaveParameter") return;

            if (pendingInstance != null) Result.Add(pendingInstance);

            pendingInstance = null;
        }
    }
}
