using Material.Icons;
using PalCalc.Model;
using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Mapped
{
    public class PalGenderViewModel
    {
        private PalGenderViewModel(PalGender gender)
        {
            Label = gender.Label();
            Value = gender;
        }

        public ILocalizedText Label { get; }
        public PalGender Value { get; }

        public bool IsWildcard => Value == PalGender.WILDCARD || Value == PalGender.OPPOSITE_WILDCARD;
        public bool IsSpecific => !IsWildcard;

        public static PalGenderViewModel Wildcard { get; } = new PalGenderViewModel(PalGender.WILDCARD);
        public static PalGenderViewModel OppositeWildcard { get; } = new PalGenderViewModel(PalGender.OPPOSITE_WILDCARD);
        public static PalGenderViewModel Male { get; } = new PalGenderViewModel(PalGender.MALE);
        public static PalGenderViewModel Female { get; } = new PalGenderViewModel(PalGender.FEMALE);

        public static List<PalGenderViewModel> All { get; } = [Wildcard, OppositeWildcard, Male, Female];
        public static List<PalGenderViewModel> AllStandard { get; } = [Wildcard, Male, Female];
        public static List<PalGenderViewModel> AllSpecific { get; } = [Male, Female];

        public static PalGenderViewModel Make(PalGender value) =>
            value switch
            {
                PalGender.WILDCARD => Wildcard,
                PalGender.OPPOSITE_WILDCARD => OppositeWildcard,
                PalGender.MALE => Male,
                PalGender.FEMALE => Female,
                _ => throw new NotImplementedException()
            };
    }
}
