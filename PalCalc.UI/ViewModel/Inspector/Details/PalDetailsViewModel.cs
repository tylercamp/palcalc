using PalCalc.Model;
using PalCalc.SaveReader.SaveFile.Support.Level;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel.Inspector.Details
{
    public class PalDetailsProperty
    {
        public ILocalizedText Key { get; set; }
        public ILocalizedText Value { get; set; }
    }

    public class PalDetailsViewModel(PalInstance pal, GvasCharacterInstance rawData)
    {
        private static ILocalizedText RawText(object value) => new HardCodedText(value?.ToString() ?? "null");
        private static ILocalizedText BoolText(bool value) => (value ? LocalizationCodes.LC_COMMON_YES : LocalizationCodes.LC_COMMON_NO).Bind();

        public List<PalDetailsProperty> PalProperties { get; } = pal == null ? [] :
            ((IEnumerable<(ILocalizedText, ILocalizedText)>)[
                (LocalizationCodes.LC_SAVEINSPECT_PROPERTY_PAL.Bind(), PalViewModel.Make(pal.Pal).Name),
                (LocalizationCodes.LC_SAVEINSPECT_PROPERTY_PALDEX_NUM.Bind(), RawText(pal.Pal.Id.PalDexNo)),
                (LocalizationCodes.LC_SAVEINSPECT_PROPERTY_PALDEX_IS_VARIANT.Bind(), BoolText(pal.Pal.Id.IsVariant)),
                (LocalizationCodes.LC_SAVEINSPECT_PROPERTY_GENDER.Bind(), pal.Gender.Label()),
                (LocalizationCodes.LC_SAVEINSPECT_PROPERTY_DETECTED_OWNER_ID.Bind(), RawText(pal.OwnerPlayerId)),
                (LocalizationCodes.LC_SAVEINSPECT_ON_EXPEDITION.Bind(), BoolText(pal.IsOnExpedition)),
                .. pal.PassiveSkills.ZipWithIndex().Select(p => (LocalizationCodes.LC_SAVEINSPECT_PROPERTY_PASSIVE_SKILL.Bind(p.Item2 + 1), PassiveSkillViewModel.Make(p.Item1).Name)),
                .. pal.EquippedActiveSkills.ZipWithIndex().Select(s => (LocalizationCodes.LC_SAVEINSPECT_PROPERTY_EQUIPPED_ACTIVE_SKILL.Bind(s.Item2 + 1), ActiveSkillViewModel.Make(s.Item1).Name)),
                .. pal.ActiveSkills.ZipWithIndex().Select(s => (LocalizationCodes.LC_SAVEINSPECT_PROPERTY_ACTIVE_SKILL.Bind(s.Item2 + 1), ActiveSkillViewModel.Make(s.Item1).Name)),
            ])
            .ToArray()
            .Select(p => new PalDetailsProperty() { Key = p.Item1, Value = p.Item2 })
            .ToList();

        public List<PalDetailsProperty> RawProperties { get; } = rawData == null ? [] :
            ((ReadOnlySpan<(string, object)>)[
                ( "CharacterId", rawData.CharacterId ),
                ( "NickName", rawData.NickName ),
                ( "Level", rawData.Level ),
                ( "RawGender", rawData.Gender ),

                ( "IsPlayer", rawData.IsPlayer ),

                ( "InstanceId", rawData.InstanceId ),
                ( "OwnerPlayerId", rawData.OwnerPlayerId ),
                ( "OldOwnerPlayerIds", string.Join(", ", rawData.OldOwnerPlayerIds) ),

                ( "SlotIndex", rawData.SlotIndex ),

                ( "TalentHp", rawData.TalentHp ),
                ( "TalentShot", rawData.TalentShot ),
                ( "TalentMelee", rawData.TalentMelee ),
                ( "TalentDefense", rawData.TalentDefense ),

                ( "ExpeditionMapObjectId", rawData.ExpeditionMapObjectId ),

                .. rawData.PassiveSkills.ZipWithIndex().Select(p => ($"Passive Skill {p.Item2+1}", p.Item1)),
                .. rawData.EquippedActiveSkills.ZipWithIndex().Select(s => ($"Equipped Active Skill {s.Item2+1}", s.Item1)),
                .. rawData.ActiveSkills.ZipWithIndex().Select(s => ($"Active Skill {s.Item2+1}", s.Item1)),
            ])
            .ToArray()
            .Select(kvp => new PalDetailsProperty() { Key = RawText(kvp.Item1), Value = RawText(kvp.Item2) })
            .ToList();
    }
}
