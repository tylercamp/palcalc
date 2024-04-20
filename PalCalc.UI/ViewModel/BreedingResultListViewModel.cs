using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel
{
    public partial class BreedingResultListViewModel : ObservableObject
    {
        private List<BreedingResultViewModel> results;
        public List<BreedingResultViewModel> Results
        {
            get => results;
            set
            {
                if (SetProperty(ref results, value))
                {
                    SelectedResult = results.FirstOrDefault();
                }
            }
        }

        [ObservableProperty]
        private BreedingResultViewModel selectedResult;

        public static BreedingResultListViewModel DesignerInstance { get; } = new BreedingResultListViewModel()
        {
            Results = new List<BreedingResultViewModel>()
            {
                new BreedingResultViewModel(new OwnedPalReference(new PalCalc.Model.PalInstance()
                {
                    Pal = new PalCalc.Model.Pal() {
                        Id = new PalCalc.Model.PalId() { PalDexNo = 100, IsVariant = false }
                    },
                    Gender = PalCalc.Model.PalGender.WILDCARD,
                    Location = new PalCalc.Model.PalLocation() { Index = 0, Type = PalCalc.Model.LocationType.Palbox },
                    Traits = new List<PalCalc.Model.Trait>()
                    {
                        new PalCalc.Model.Trait("Trait 1", "Internal 1", 0),
                    }
                }))
            }
        };
    }
}
