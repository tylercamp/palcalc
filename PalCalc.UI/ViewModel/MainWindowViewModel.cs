using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        private static PalDB db = PalDB.LoadEmbedded();

        [ObservableProperty]
        private BreedingGraph breedingGraph;

        // design-time model
        public MainWindowViewModel(int x)
        {
            var dummyId = new PalId() { PalDexNo = 123 };
            BreedingGraph = BreedingGraph.FromPalReference(
                    new BredPalReference(
                        gameSettings: new GameSettings(),
                        pal: new Pal() { Name = "Final Child", Id = dummyId },
                        traits: new List<Trait>(),
                        traitsProbability: 0.1f,

                        parent1: new WildcardPalReference(new Pal() { Name = "Parent 1", Id = dummyId }, 1),

                        parent2: new BredPalReference(
                            gameSettings: new GameSettings(),
                            pal: new Pal() { Name = "Parent 2", Id = dummyId },
                            traits: new List<Trait>(),
                            traitsProbability: 0.25f,

                            parent1: new OwnedPalReference(new PalInstance()
                            {
                                Pal = new Pal() { Name = "Parent A of Parent 2" , Id = dummyId },
                                Gender = PalGender.MALE,
                                Location = new PalLocation()
                                {
                                    Index = 133,
                                    Type = LocationType.Palbox
                                }
                            }),

                            parent2: new OwnedPalReference(new PalInstance()
                            {
                                Pal = new Pal() { Name = "Parent B of Parent 2" , Id = dummyId },
                                Gender = PalGender.FEMALE,
                                Location = new PalLocation()
                                {
                                    Index = 1,
                                    Type = LocationType.PlayerParty
                                }
                            })
                        )
                    )
                );
        }

        // main app model
        public MainWindowViewModel()
        {
            var latest = SavesLocation.AllLocal.SelectMany(loc => loc.ValidSaveGames).MaxBy(game => game.LastModified);
            var solver = new Solver.Solver(
                gameSettings: new GameSettings(),
                db: db,
                ownedPals: latest.Level.ReadPalInstances(db),
                maxBreedingSteps: 3,
                maxWildPals: 0,
                maxIrrelevantTraits: 0,
                maxEffort: TimeSpan.FromHours(8)
            );

            var targetInstance = new PalInstance
            {
                Pal = "Galeclaw".ToPal(db),
                Gender = PalGender.WILDCARD,
                Traits = new List<Trait> {
                    "Swift".ToTrait(db),
                    "Runner".ToTrait(db),
                    "Nimble".ToTrait(db)
                },
                Location = null
            };

            var output = solver.SolveFor(targetInstance).MaxBy(r => r.NumBredPalParticipants());
            BreedingGraph = BreedingGraph.FromPalReference(output);
        }
    }
}
