using PalCalc.Model;
using PalCalc.SaveReader.SaveFile.Support.Level;
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
        public string Key { get; set; }
        public string Value { get; set; }
    }

    // TODO - Localize (eventually)

    public class PalDetailsViewModel(PalInstance pal, GvasCharacterInstance rawData)
    {
        public List<PalDetailsProperty> PalProperties { get; } = pal == null ? [] :
            ((ReadOnlySpan<(string, object)>)[
                ( "Pal", pal.Pal.Name ),
                ( "Paldex #", pal.Pal.Id.PalDexNo ),
                ( "Paldex Is Variant", pal.Pal.Id.IsVariant ),
                ( "Gender", pal.Gender ),
                ( "Detected Owner ID", pal.OwnerPlayerId ),
                .. pal.PassiveSkills.ZipWithIndex().Select(p => ($"Passive Skill {p.Item2+1}", p.Item1.Name))
            ])
            .ToArray()
            .Select(p => new PalDetailsProperty() { Key = p.Item1, Value = p.Item2?.ToString() ?? "null" })
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

                .. rawData.PassiveSkills.ZipWithIndex().Select(p => ($"Passive Skill {p.Item2+1}", p.Item1))
            ])
            .ToArray()
            .Select(kvp => new PalDetailsProperty() { Key = kvp.Item1, Value = kvp.Item2?.ToString() ?? "null" })
            .ToList();
    }
}
