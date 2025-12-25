using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.GraphSharp;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.PalDerived;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Solver
{
    public class BreedingResultViewModel : ObservableObject
    {
        // For XAML designer view
        public BreedingResultViewModel()
        {
            var db = PalDB.LoadEmbedded();
            var saveGame = CachedSaveGame.SampleForDesignerView;

            var solver = new BreedingSolver(
                new BreedingSolverSettings(
                    gameSettings: new GameSettings(),
                    db: db,
                    pruningBuilder: PruningRulesBuilder.Default,
                    ownedPals: saveGame.OwnedPals,
                    maxBreedingSteps: 3,
                    maxSolverIterations: 5,
                    maxWildPals: 0,
                    allowedWildPals: PalDB.LoadEmbedded().Pals.ToList(),
                    bannedBredPals: new List<Pal>(),
                    maxInputIrrelevantPassives: 2,
                    maxBredIrrelevantPassives: 0,
                    maxEffort: TimeSpan.FromHours(8),
                    maxThreads: 0,
                    maxSurgeryCost: 0,
                    allowedSurgeryPassives: [],
                    useGenderReversers: false
                )
            );

            var targetInstance = new PalSpecifier
            {
                Pal = "Galeclaw".ToPal(db),
                RequiredPassives = new List<PassiveSkill> {
                    "Swift".ToStandardPassive(db),
                    "Runner".ToStandardPassive(db),
                    "Nimble".ToStandardPassive(db)
                },
            };

            DisplayedResult = solver.SolveFor(targetInstance, new SolverStateController() { CancellationToken = CancellationToken.None }).MaxBy(r => r.NumTotalBreedingSteps);

            IVs = new IVSetViewModel(
                HP: new IVDirectValueViewModel(true, 80),
                Attack: new IVDirectValueViewModel(true, 70),
                Defense: new IVDirectValueViewModel(true, 60)
            );
            IV_Average = new IVDirectValueViewModel(true, 70);
        }

        private CachedSaveGame source;
        public BreedingResultViewModel(CachedSaveGame source, GameSettings settings, IPalReference displayedResult)
        {
            this.source = source;

            if (displayedResult == null)
            {
                Graph = null;
                DisplayedResult = null;
                Label = LocalizationCodes.LC_COMMON_UNKNOWN.Bind();
            }
            else
            {
                DisplayedResult = displayedResult;
                Graph = BreedingGraph.FromPalReference(source, settings, displayedResult);
                EffectivePassives = new PassiveSkillCollectionViewModel(DisplayedResult.EffectivePassives.Select(PassiveSkillViewModel.Make));

                IVs = IVSetViewModel.FromIVs(displayedResult.IVs);

                var validIVs = new[]
                {
                    displayedResult.IVs.HP,
                    displayedResult.IVs.Attack,
                    displayedResult.IVs.Defense
                }.Where(iv => iv != IV_Value.Random).ToArray();

                if (validIVs.Length > 0)
                {
                    var min = (int)Math.Round(validIVs.Average(iv => iv.Min));
                    var max = (int)Math.Round(validIVs.Average(iv => iv.Max));

                    IV_Average = min != max
                        ? new IVRangeValueViewModel(true, min, max)
                        : new IVDirectValueViewModel(true, min);
                }
                else
                {
                    IV_Average = IVAnyValueViewModel.Instance;
                }

                Label = LocalizationCodes.LC_RESULT_LABEL.Bind(
                    new
                    {
                        PalName = PalViewModel.Make(DisplayedResult.Pal).Label,
                        TraitsList = EffectivePassives.Description,
                        TimeEstimate = TimeEstimate.TimeSpanMinutesStr(),
                    }
                );

                var descriptionParts = DisplayedResult
                    .AllReferences()
                    .Where(r => r is OwnedPalReference || r is CompositeOwnedPalReference)
                    .SelectMany(r =>
                    {
                        switch (r)
                        {
                            case OwnedPalReference opr: return new List<PalLocation>() { opr.UnderlyingInstance.Location };

                            case CompositeOwnedPalReference corl:
                                return new List<PalLocation>()
                                {
                                    corl.Male.UnderlyingInstance.Location,
                                    corl.Female.UnderlyingInstance.Location
                                };

                            default:
                                throw new NotImplementedException(); // shouldn't happen
                        }
                    })
                    .GroupBy(l => l.Type)
                    .OrderBy(g => g.Key switch
                    {
                        LocationType.Palbox => 0,
                        LocationType.DimensionalPalStorage => 1,
                        LocationType.Base => 2,
                        LocationType.ViewingCage => 3,
                        LocationType.PlayerParty => 4,
                        LocationType.GlobalPalStorage => 5,
                        LocationType.Custom => 6,
                        _ => throw new NotImplementedException()
                    })
                    .Select(g => LocalizationCodes.LC_PAL_LOC_COUNT.Bind(
                        new
                        {
                            Count = g.Count(),
                            LocType = g.Key.ShortLabel(),
                        }
                    ))
                    .ToList();

                var numWildPals = DisplayedResult.AllReferences().Count(r => r is WildPalReference);
                if (numWildPals > 0)
                    descriptionParts.Add(LocalizationCodes.LC_PAL_WILD_COUNT.Bind(numWildPals));

                InputLocations = Translator.Join.Bind(descriptionParts);
            }
        }

        public ILocalizedText Label { get; }
        public ILocalizedText InputLocations { get; }

        public BreedingGraph Graph { get; }

        public IPalReference DisplayedResult { get; }

        public PassiveSkillCollectionViewModel EffectivePassives { get; }

        public TimeSpan TimeEstimate => DisplayedResult?.BreedingEffort ?? TimeSpan.Zero;
        public string TimeEstimateLabel => TimeEstimate.TimeSpanSecondsStr();

        public bool HasValue => Graph != null;
        public bool NeedsRefresh => Graph?.NeedsRefresh ?? false;
        public int NumWildPals => DisplayedResult.NumTotalWildPals;
        public int NumBreedingSteps => DisplayedResult.NumTotalBreedingSteps;
        public int NumEggs => DisplayedResult.NumTotalEggs;

        public IVSetViewModel IVs { get; }
        // (needed for BreedingResultListView which uses `util:GridViewSort.PropertyName`)
        public IVValueViewModel IV_HP => IVs.HP;
        public IVValueViewModel IV_Attack => IVs.Attack;
        public IVValueViewModel IV_Defense => IVs.Defense;

        public IVValueViewModel IV_Average { get; }
    }
}
