using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using PalCalc.UI.Model;
using PalCalc.UI.View;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        private static PalDB db = PalDB.LoadEmbedded();
        private Dictionary<SaveGame, PalTargetListViewModel> targetsBySaveFile;

        // main app model
        public MainWindowViewModel()
        {
            SaveSelection = new SaveSelectorViewModel(SavesLocation.AllLocal);
            SolverControls = new SolverControlsViewModel();
            PalTargetList = new PalTargetListViewModel();

            targetsBySaveFile = SaveSelection.AvailableSaves
                .Select(sgvm => sgvm.Value)
                .ToDictionary(
                    sg => sg,
                    sg =>
                    {
                        var saveLocation = Storage.SaveFileDataPath(sg);
                        var targetsFile = Path.Join(saveLocation, "pal-targets.json");
                        if (File.Exists(targetsFile))
                        {
                            var converter = new PalTargetListViewModelConverter(db, new GameSettings());
                            return JsonConvert.DeserializeObject<PalTargetListViewModel>(File.ReadAllText(targetsFile), converter);
                        }
                        else
                        {
                            return new PalTargetListViewModel();
                        }
                    }
                );

            SaveSelection.PropertyChanged += SaveSelection_PropertyChanged;

            UpdateTargetsList();
        }

        private void UpdateTargetsList()
        {
            if (PalTargetList != null) PalTargetList.PropertyChanged -= PalTargetList_PropertyChanged;

            if (SaveSelection.SelectedGame == null)
            {
                PalTargetList = null;
                PalTarget = null;
            }

            PalTargetList = targetsBySaveFile[SaveSelection.SelectedGame.Value];
            PalTargetList.PropertyChanged += PalTargetList_PropertyChanged;

            UpdatePalTarget();
        }

        private void UpdatePalTarget()
        {
            if (PalTargetList.SelectedTarget !=  null)
                PalTarget = new PalTargetViewModel(PalTargetList.SelectedTarget);
        }

        private void SaveSelection_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SaveSelection.SelectedGame))
            {
                UpdateTargetsList();
            }
        }

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
            }
            else
            {
                PalTargetList.Replace(PalTarget.InitialPalSpecifier, PalTarget.CurrentPalSpecifier);
                PalTargetList.SelectedTarget = PalTarget.CurrentPalSpecifier;
            }

            var outputFolder = Storage.SaveFileDataPath(SaveSelection.SelectedGame.Value);
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var outputFile = Path.Join(outputFolder, "pal-targets.json");
            var converter = new PalTargetListViewModelConverter(db, new GameSettings());
            File.WriteAllText(outputFile, JsonConvert.SerializeObject(PalTargetList, converter));
        }

        [ObservableProperty]
        private SaveSelectorViewModel saveSelection;
        [ObservableProperty]
        private SolverControlsViewModel solverControls;
        [ObservableProperty]
        private PalTargetListViewModel palTargetList;

        [ObservableProperty]
        private PalTargetViewModel palTarget;

        public PalDB DB => db;
    }
}
