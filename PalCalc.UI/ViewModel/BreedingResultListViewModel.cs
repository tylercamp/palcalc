using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
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
            else if (Results.Select(selector).Distinct().Count() == 1 && !Translator.DEBUG_DISABLE_TRANSLATIONS) return WIDTH_HIDDEN;
            else return FIT_CONTENT;
        }

        public double EffortWidth => DEFAULT;
        public double NumStepsWidth => HiddenIfRedundant(vm => vm.NumBreedingSteps);
        public double LocationsWidth => HiddenIfRedundant(vm => vm.InputLocations);
        public double TraitsWidth => HiddenIfRedundant(vm => vm.EffectiveTraits.Description);

        public void RefreshWith(CachedSaveGame csg)
        {
            Results = Results.Select(r => new BreedingResultViewModel(csg, r.DisplayedResult)).ToList();
        }

        [JsonIgnore]
        private ILocalizedText resultsHeading;
        public ILocalizedText ResultsHeading => resultsHeading ??=
            Results == null
                ? Translator.Translations[LocalizationCodes.LC_RESULT_LIST_TITLE_EMPTY].Bind()
                : Translator.Translations[LocalizationCodes.LC_RESULT_LIST_TITLE_COUNT].Bind(new() { { "NumResults", Results.Count } });

        public static BreedingResultListViewModel DesignerInstance { get; } = new BreedingResultListViewModel()
        {
            Results = new List<BreedingResultViewModel>()
            {
                new BreedingResultViewModel(null, new OwnedPalReference(new PalInstance()
                {
                    Pal = "Beakon".ToPal(PalDB.LoadEmbedded()),
                    Gender = PalGender.WILDCARD,
                    Location = new PalLocation() { Index = 0, Type = LocationType.Palbox },
                    Traits = new List<Trait>()
                    {
                        "Runner".ToTrait(PalDB.LoadEmbedded()),
                    }
                }, new List<Trait>() { "Runner".ToTrait(PalDB.LoadEmbedded()) }))
            }
        };
    }
}
