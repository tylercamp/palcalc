using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PalCalc.UI.ViewModel.Solver
{
    // A snapshot of the full solver settings used when the breeding results were produced. Used
    // during deserialization.
    public class BreedingResultListViewModelSettingsSnapshot
    {
        public GameSettings GameSettings { get; set; }
        public SerializableSolverSettings SolverSettings { get; set; }
    }

    public partial class BreedingResultListViewModel : ObservableObject
    {
        public BreedingResultListViewModelSettingsSnapshot SettingsSnapshot { get; set; }

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
                    OnPropertyChanged(nameof(PassiveSkillsWidth));
                    OnPropertyChanged(nameof(NumEggsWidth));
                    OnPropertyChanged(nameof(IVsWidth));
                }
            }
        }

        [ObservableProperty]
        private BreedingResultViewModel selectedResult;

        private readonly double WIDTH_HIDDEN = 0;
        private readonly double FIT_CONTENT = double.NaN;
        private readonly double DEFAULT = double.NaN;

        // hide columns if they're all the same value
        private double HiddenIfRedundant<T>(Func<BreedingResultViewModel, T> selector)
        {
            if (Results == null || Results.Count < 2) return DEFAULT;
            // (but don't hide columns if we've disabled translations and are looking for anything that needs updates)
            else if (Results.Select(selector).Distinct().Count() == 1 && !Translator.DEBUG_DISABLE_TRANSLATIONS) return WIDTH_HIDDEN;
            else return FIT_CONTENT;
        }

        public double EffortWidth => DEFAULT;
        public double NumStepsWidth => HiddenIfRedundant(vm => vm.NumBreedingSteps);
        public double LocationsWidth => HiddenIfRedundant(vm => vm.InputLocations);
        public double PassiveSkillsWidth => HiddenIfRedundant(vm => vm.EffectivePassives.Description);
        public double NumEggsWidth => HiddenIfRedundant(vm => vm.NumEggs);
        public double IVsWidth
        {
            get
            {
                if (Results == null || Results.Count < 2) return DEFAULT;

                if (Results.All(r =>
                    r.IVs.HP is IVAnyValueViewModel &&
                    r.IVs.Attack is IVAnyValueViewModel &&
                    r.IVs.Defense is IVAnyValueViewModel)
                ) return WIDTH_HIDDEN;

                return FIT_CONTENT;
            }
        }

        // breeding result data aren't serialized with all required info, namely player + guild names.
        //
        // they require a `CachedSaveGame` but this isn't always available when deserializing during
        // app start-up, so they will accept a `null` CSG temporarily (in ctor) but must be updated
        // before display
        public void UpdateCachedData(CachedSaveGame csg, GameSettings settings)
        {
            Results = Results.Select(r => new BreedingResultViewModel(csg, settings, r.DisplayedResult)).ToList();
        }

        [JsonIgnore]
        private ILocalizedText resultsHeading;
        public ILocalizedText ResultsHeading => resultsHeading ??=
            Results == null
                ? LocalizationCodes.LC_RESULT_LIST_TITLE_EMPTY.Bind()
                : LocalizationCodes.LC_RESULT_LIST_TITLE_COUNT.Bind(Results.Count);

        public static BreedingResultListViewModel DesignerInstance { get; } = new BreedingResultListViewModel()
        {
            Results = new List<BreedingResultViewModel>()
            {
                new BreedingResultViewModel(null, GameSettings.Defaults, new OwnedPalReference(
                    new PalInstance()
                    {
                        Pal = "Beakon".ToPal(PalDB.LoadEmbedded()),
                        Gender = PalGender.WILDCARD,
                        Location = new PalLocation() { Index = 0, Type = LocationType.Palbox },
                        PassiveSkills = new List<PassiveSkill>()
                        {
                            "Runner".ToStandardPassive(PalDB.LoadEmbedded()),
                        }
                    },
                    new List<PassiveSkill>() { "Runner".ToStandardPassive(PalDB.LoadEmbedded()) },
                    new IV_Set()
                    {
                        HP = new IV_Value(true, 80, 90),
                        Attack = IV_Value.Random,
                        Defense = IV_Value.Random
                    }
                ))
            }
        };
    }
}
