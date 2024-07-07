using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.GraphSharp;
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
            var latest = DirectSavesLocation.AllLocal.SelectMany(loc => loc.ValidSaveGames).Where(s => Storage.LoadSaveFromCache(s, db) != null).MaxBy(game => game.LastModified);
            var saveGame = Storage.LoadSaveFromCache(latest, db);

            var solver = new Solver.BreedingSolver(
                gameSettings: new GameSettings(),
                db: db,
                pruningBuilder: PruningRulesBuilder.Default,
                ownedPals: saveGame.OwnedPals,
                maxBreedingSteps: 3,
                maxWildPals: 0,
                maxInputIrrelevantTraits: 2,
                maxBredIrrelevantTraits: 0,
                maxEffort: TimeSpan.FromHours(8),
                maxThreads: 0
            );

            var targetInstance = new PalSpecifier
            {
                Pal = "Galeclaw".ToPal(db),
                RequiredTraits = new List<Trait> {
                    "Swift".ToTrait(db),
                    "Runner".ToTrait(db),
                    "Nimble".ToTrait(db)
                },
            };

            DisplayedResult = solver.SolveFor(targetInstance, CancellationToken.None).MaxBy(r => r.NumTotalBreedingSteps);
        }

        private CachedSaveGame source;
        public BreedingResultViewModel(CachedSaveGame source, IPalReference displayedResult)
        {
            this.source = source;

            DisplayedResult = displayedResult;
        }

        public BreedingGraph Graph { get; private set; }

        public TimeSpan TimeEstimate => DisplayedResult?.BreedingEffort ?? TimeSpan.Zero;
        public string TimeEstimateLabel => TimeEstimate.TimeSpanSecondsStr();
        public string Label => $"{DisplayedResult?.ToString() ?? "Unknown"}, takes ~{TimeEstimate.TimeSpanMinutesStr()}";

        public int NumWildPals => DisplayedResult.NumWildPalParticipants();
        public int NumBreedingSteps => DisplayedResult.NumTotalBreedingSteps;

        public string FinalTraits => DisplayedResult.EffectiveTraitsString ?? string.Empty;

        private string inputLocations;
        public string InputLocations
        {
            get
            {
                if (inputLocations == null)
                {
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
                        .Select(g => $"{g.Count()} in {g.Key.Label()}")
                        .ToList();

                    var numWildPals = DisplayedResult.AllReferences().Count(r => r is WildPalReference);
                    if (numWildPals > 0)
                        descriptionParts.Add($"{numWildPals} Wild");

                    inputLocations = string.Join(", ", descriptionParts);
                }

                return inputLocations;
            }
        }

        public bool HasValue => Graph != null;

        public bool NeedsRefresh => Graph?.NeedsRefresh ?? false;

        private IPalReference displayedResult = null;
        public IPalReference DisplayedResult
        {
            get => displayedResult;
            private set
            {
                displayedResult = value;

                if (displayedResult == null) Graph = null;
                else Graph = BreedingGraph.FromPalReference(source, value);

                OnPropertyChanged(nameof(DisplayedResult));
                OnPropertyChanged(nameof(Graph));
                OnPropertyChanged(nameof(HasValue));
            }
        }
    }
}
