using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using PalCalc.UI.Model;
using PalCalc.UI.View;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        private static PalDB db = PalDB.LoadEmbedded();

        // main app model
        public MainWindowViewModel()
        {
            SaveSelection = new SaveSelectorViewModel(SavesLocation.AllLocal);
            SolverControls = new SolverControlsViewModel();
            PalTargetList = new PalTargetListViewModel();

            PalTargetList.PropertyChanged += PalTargetList_PropertyChanged;
            UpdatePalTarget();
        }

        private void UpdatePalTarget() => PalTarget = new PalTargetViewModel(PalTargetList.SelectedTarget);

        private void PalTargetList_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PalTargetList.SelectedTarget))
            {
                UpdatePalTarget();
            }
        }

        public void RunSolver()
        {
            var currentSpec = PalTarget.CurrentPalSpecifier.ModelObject;
            if (currentSpec == null) return;

            var solver = SolverControls.ConfiguredSolver(SaveSelection.SelectedGame.CachedValue.OwnedPals);
            var results = solver.SolveFor(currentSpec);

            PalTarget.CurrentPalSpecifier.CurrentResults = new BreedingResultListViewModel() { Results = results.Select(r => new BreedingResultViewModel(r)).ToList() };
            if (PalTarget.InitialPalSpecifier == null)
            {
                PalTargetList.Add(PalTarget.CurrentPalSpecifier);
                PalTargetList.SelectedTarget = PalTarget.CurrentPalSpecifier;

                PalTarget.InitialPalSpecifier = PalTarget.CurrentPalSpecifier;
            }
            else
            {
                PalTarget.InitialPalSpecifier.CopyFrom(PalTarget.CurrentPalSpecifier);
            }
        }

        public SaveSelectorViewModel SaveSelection { get; private set; }
        public SolverControlsViewModel SolverControls { get; private set; }
        public PalTargetListViewModel PalTargetList { get; private set; }

        [ObservableProperty]
        private PalTargetViewModel palTarget;

        public PalDB DB => db;
    }
}
