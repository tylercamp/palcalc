using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB.GameDataReaders
{
    class UCombiUnique
    {
        [FStructProperty]
        public string ParentTribeA { get; set; }
        [FStructProperty]
        public string ParentGenderA { get; set; }

        [FStructProperty]
        public string ParentTribeB { get; set; }
        [FStructProperty]
        public string ParentGenderB { get; set; }

        [FStructProperty]
        public string ChildCharacterID { get; set; }
    }

    internal class UniqueBreedComboReader
    {
        public static List<((string, PalGender?), (string, PalGender?), string)> ReadUniqueBreedCombos(IFileProvider provider)
        {
            Console.WriteLine("Reading unique breeding combos");
            var rawCombos = provider.LoadObject<UDataTable>(AssetPaths.PALS_UNIQUE_BREEDING_PATH);

            string TribeToPal(string tribe) => tribe.Split("::").Last();
            PalGender? StringToGender(string gender) => gender switch
            {
                "EPalGenderType::None" => null,
                "EPalGenderType::Male" => PalGender.MALE,
                "EPalGenderType::Female" => PalGender.FEMALE
            };

            List<((string, PalGender?), (string, PalGender?), string)> result = [];

            foreach (var entry in rawCombos.RowMap.Values)
            {
                // data model specifies breeding by "tribe", which seems to represent groups of pal types, but in practice
                // every pal gets its own tribe atm
                var combo = entry.ToObject<UCombiUnique>();
                result.Add(
                    (
                        (TribeToPal(combo.ParentTribeA), StringToGender(combo.ParentGenderA)),
                        (TribeToPal(combo.ParentTribeB), StringToGender(combo.ParentGenderB)),
                        combo.ChildCharacterID
                    )
                );
            }

            return result;
        }
    }
}
