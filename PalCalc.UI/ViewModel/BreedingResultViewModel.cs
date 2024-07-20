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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel
{
    public class BreedingResultViewModel : ObservableObject
    {
        // For XAML designer view
        public BreedingResultViewModel()
        {
            var db = PalDB.LoadEmbedded();
            var saveGame = CachedSaveGame.SampleForDesignerView;

            var solver = new Solver.BreedingSolver(
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
                maxThreads: 0
            );

            var targetInstance = new PalSpecifier
            {
                Pal = "Galeclaw".ToPal(db),
                RequiredPassives = new List<PassiveSkill> {
                    "Swift".ToPassive(db),
                    "Runner".ToPassive(db),
                    "Nimble".ToPassive(db)
                },
            };

            DisplayedResult = solver.SolveFor(targetInstance, CancellationToken.None).MaxBy(r => r.NumTotalBreedingSteps);
        }

        private CachedSaveGame source;
        public BreedingResultViewModel(CachedSaveGame source, IPalReference displayedResult)
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
                Graph = BreedingGraph.FromPalReference(source, displayedResult);
                EffectivePassives = new PassiveSkillCollectionViewModel(DisplayedResult.EffectivePassives.Select(PassiveSkillViewModel.Make));
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
                        LocationType.Base => 1,
                        LocationType.PlayerParty => 2,
                        _ => throw new NotImplementedException()
                    })
                    .Select(g => LocalizationCodes.LC_PAL_LOC_COUNT.Bind(
                        new
                        {
                            Count = g.Count(),
                            LocType = g.Key.Label(),
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
        public int NumWildPals => DisplayedResult.NumWildPalParticipants();
        public int NumBreedingSteps => DisplayedResult.NumTotalBreedingSteps;
    }
}
