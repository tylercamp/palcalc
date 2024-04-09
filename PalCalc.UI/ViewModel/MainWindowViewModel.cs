using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
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
        [ObservableProperty]
        private BreedingGraph breedingGraph;

        [ObservableProperty]
        private string layoutAlgorithmType = "Tree";

        public MainWindowViewModel()
        {
            var dummyId = new PalId() { PalDexNo = 100, IsVariant = false };
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
    }
}
