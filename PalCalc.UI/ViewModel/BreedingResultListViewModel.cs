using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

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
                    SelectedResult = results.OrderBy(r => r.TimeEstimate).FirstOrDefault();
                    OnPropertyChanged(nameof(EffortWidth));
                    OnPropertyChanged(nameof(NumStepsWidth));
                    OnPropertyChanged(nameof(LocationsWidth));
                    OnPropertyChanged(nameof(TraitsWidth));
                }
            }
        }

        [ObservableProperty]
        private BreedingResultViewModel selectedResult;

        private readonly double WIDTH_HIDDEN = 0;
        private readonly double FIT_CONTENT = double.NaN;
        private readonly double DEFAULT = double.NaN;

        private double HiddenIfRedundant<T>(Func<BreedingResultViewModel, T> selector)
        {
            if (Results == null || Results.Count < 2) return DEFAULT;
            else if (Results.Select(selector).Distinct().Count() == 1) return WIDTH_HIDDEN;
            else return FIT_CONTENT;
        }

        public double EffortWidth => DEFAULT;
        public double NumStepsWidth => HiddenIfRedundant(vm => vm.NumBreedingSteps);
        public double LocationsWidth => HiddenIfRedundant(vm => vm.InputLocations);
        public double TraitsWidth => HiddenIfRedundant(vm => vm.FinalTraits);

        public string ResultsHeading => Results == null ? "Results" : $"{Results.Count} Results";

        public static BreedingResultListViewModel DesignerInstance { get; } = new BreedingResultListViewModel()
        {
            Results = new List<BreedingResultViewModel>()
            {
                new BreedingResultViewModel(null, new OwnedPalReference(new PalCalc.Model.PalInstance()
                {
                    Pal = new PalCalc.Model.Pal() {
                        Name = "Test Pal",
                        Id = new PalCalc.Model.PalId() { PalDexNo = 100, IsVariant = false }
                    },
                    Gender = PalCalc.Model.PalGender.WILDCARD,
                    Location = new PalCalc.Model.PalLocation() { Index = 0, Type = PalCalc.Model.LocationType.Palbox },
                    Traits = new List<PalCalc.Model.Trait>()
                    {
                        new PalCalc.Model.Trait("Trait 1", "Internal 1", 0),
                    }
                }, new List<PalCalc.Model.Trait>() { new PalCalc.Model.Trait("Trait 1", "Internal 1", 0) }))
            }
        };
    }
}
