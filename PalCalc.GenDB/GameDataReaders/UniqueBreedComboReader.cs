using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using PalCalc.Model;
using Serilog;
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
        private static ILogger logger = Log.ForContext<UniqueBreedComboReader>();

        public static List<UniqueBreedingCombo> ReadUniqueBreedCombos(IFileProvider provider)
        {
            logger.Information("Reading unique breeding combos");
            var rawCombos = provider.LoadPackageObject<UDataTable>(AssetPaths.PALS_UNIQUE_BREEDING_PATH);

            PalGender? StringToGender(string gender) => gender switch
            {
                "EPalGenderType::None" => null,
                "EPalGenderType::Male" => PalGender.MALE,
                "EPalGenderType::Female" => PalGender.FEMALE,
                _ => throw new ArgumentException($"Unknown gender type: {gender}")
            };

            List<UniqueBreedingCombo> result = [];

            foreach (var entry in rawCombos.RowMap.Values)
            {
                var combo = entry.ToObject<UCombiUnique>();
                result.Add(
                    new UniqueBreedingCombo(
                        (combo.ParentTribeA, StringToGender(combo.ParentGenderA)),
                        (combo.ParentTribeB, StringToGender(combo.ParentGenderB)),
                        combo.ChildCharacterID
                    )
                );
            }

            return result;
        }
    }
}
