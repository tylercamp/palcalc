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

    public class PalDetailsViewModel(PalInstance pal, GvasCharacterInstance rawData)
    {
        public List<PalDetailsProperty> PalProperties { get; } = pal == null ? [] :
            new Dictionary<string, object>()
            {
                { "Pal", pal.Pal.Name },
                { "Paldex #", pal.Pal.Id.PalDexNo },
                { "Paldex Is Variant", pal.Pal.Id.IsVariant },
                { "Gender", pal.Gender },
                { "Detected Owner ID", pal.OwnerPlayerId },
            }
            .Select(kvp => new PalDetailsProperty() { Key = kvp.Key, Value = kvp.Value?.ToString() ?? "null" })
            .Concat(pal.Traits.Zip(Enumerable.Range(1, pal.Traits.Count)).Select(t => new PalDetailsProperty() { Key = $"Trait {t.Second}", Value = t.First.Name }))
            .ToList();

        public List<PalDetailsProperty> RawProperties { get; } = rawData == null ? [] :
            new Dictionary<string, object>()
            {
                { "CharacterId", rawData.CharacterId },
                { "NickName", rawData.NickName },
                { "Level", rawData.Level },
                { "RawGender", rawData.Gender },

                { "IsPlayer", rawData.IsPlayer },

                { "InstanceId", rawData.InstanceId },
                { "OwnerPlayerId", rawData.OwnerPlayerId },
                { "OldOwnerPlayerIds", string.Join(", ", rawData.OldOwnerPlayerIds) },

                { "SlotIndex", rawData.SlotIndex },

                { "TalentHp", rawData.TalentHp },
                { "TalentShot", rawData.TalentShot },
                { "TalentMelee", rawData.TalentMelee },
                { "TalentDefense", rawData.TalentDefense },
            }
            .Select(kvp => new PalDetailsProperty() { Key = kvp.Key, Value = kvp.Value?.ToString() ?? "null" })
            .Concat(rawData.Traits.Zip(Enumerable.Range(1, rawData.Traits.Count)).Select(t => new PalDetailsProperty() { Key = $"Trait {t.Second}", Value = t.First }))
            .ToList();
    }
}
