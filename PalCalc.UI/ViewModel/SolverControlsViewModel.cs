using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel
{
    public partial class SolverControlsViewModel : ObservableObject
    {
        private int maxBreedingSteps;
        public int MaxBreedingSteps
        {
            get => maxBreedingSteps;
            set => SetProperty(ref maxBreedingSteps, Math.Max(1, value));
        }

        private int maxWildPals;
        public int MaxWildPals
        {
            get => maxWildPals;
            set => SetProperty(ref maxWildPals, Math.Max(0, value));
        }

        private int maxIrrelevantTraits;
        public int MaxIrrelevantTraits
        {
            get => maxIrrelevantTraits;
            set => SetProperty(ref maxIrrelevantTraits, Math.Clamp(value, 0, GameConstants.MaxTotalTraits));
        }

        public BreedingSolver ConfiguredSolver(List<PalInstance> pals) => new BreedingSolver(
            new GameSettings(),
            PalDB.LoadEmbedded(),
            pals,
            MaxBreedingSteps,
            MaxWildPals,
            MaxIrrelevantTraits,
            TimeSpan.MaxValue
        );
    }
}
