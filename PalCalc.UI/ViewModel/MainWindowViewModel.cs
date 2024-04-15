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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PalCalc.UI.ViewModel
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        private static PalDB db = PalDB.LoadEmbedded();
        private Dictionary<SaveGame, PalTargetListViewModel> targetsBySaveFile;
        private LoadingSaveFileModal loadingSaveModal = null;
        private Dispatcher dispatcher;

        // https://stackoverflow.com/a/73181682
        private static void AllowUIToUpdate()
        {
            DispatcherFrame frame = new();
            // DispatcherPriority set to Input, the highest priority
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Input, new DispatcherOperationCallback(delegate (object parameter)
            {
                frame.Continue = false;
                Thread.Sleep(20); // Stop all processes to make sure the UI update is perform
                return null;
            }), null);
            Dispatcher.PushFrame(frame);
            // DispatcherPriority set to Input, the highest priority
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Input, new Action(delegate { }));
        }

        public MainWindowViewModel() : this(null) { }

        // main app model
        public MainWindowViewModel(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;

            CachedSaveGame.SaveFileLoadStart += CachedSaveGame_SaveFileLoadStart;
            CachedSaveGame.SaveFileLoadEnd += CachedSaveGame_SaveFileLoadEnd;

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

        private void CachedSaveGame_SaveFileLoadStart(SaveGame obj)
        {
            if (loadingSaveModal == null)
            {
                loadingSaveModal = new LoadingSaveFileModal();
                loadingSaveModal.Owner = Application.Current.MainWindow;
                loadingSaveModal.DataContext = "Save file was not yet cached or cache is outdated, reading content...";
                loadingSaveModal.Show();
                AllowUIToUpdate();
            }
        }

        private void CachedSaveGame_SaveFileLoadEnd(SaveGame obj)
        {
            if (loadingSaveModal != null)
            {
                loadingSaveModal.Close();
                loadingSaveModal = null;
                AllowUIToUpdate();
            }
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
            solver.SolverStateUpdated += Solver_SolverStateUpdated;

            Task.Factory.StartNew(() =>
            {
                dispatcher.Invoke(() => IsEditable = false);

                var results = solver.SolveFor(currentSpec);

                dispatcher.Invoke(() =>
                {
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

                    IsEditable = true;
                });
            });
        }

        private void Solver_SolverStateUpdated(SolverStatus obj)
        {
            dispatcher.BeginInvoke(() =>
            {
                var numTotalSteps = (double)(1 + obj.TargetSteps * 2);
                int overallStep = 0;
                switch (obj.CurrentPhase)
                {
                    case SolverPhase.Initializing:
                        SolverStatusMsg = "Initializing";
                        overallStep = 0;
                        break;

                    case SolverPhase.Breeding:
                        SolverStatusMsg = $"Breeding step {obj.CurrentStepIndex + 1}, calculating child pals and probabilities";
                        overallStep = 1 + obj.CurrentStepIndex * 2;
                        
                        break;

                    case SolverPhase.Simplifying:
                        SolverStatusMsg = $"Breeding step {obj.CurrentStepIndex + 1}, simplifying results";
                        overallStep = 1 + obj.CurrentStepIndex * 2 + 1;
                        break;

                    case SolverPhase.Finished:
                        SolverStatusMsg = "Finished";
                        overallStep = (int)numTotalSteps;
                        break;
                }

                SolverProgress = 100 * overallStep / numTotalSteps;
            });
        }

        [ObservableProperty]
        private SaveSelectorViewModel saveSelection;
        [ObservableProperty]
        private SolverControlsViewModel solverControls;
        [ObservableProperty]
        private PalTargetListViewModel palTargetList;

        [ObservableProperty]
        private PalTargetViewModel palTarget;

        [ObservableProperty]
        private double solverProgress;

        [ObservableProperty]
        private string solverStatusMsg;

        [NotifyPropertyChangedFor(nameof(ProgressBarVisibility))]
        [ObservableProperty]
        private bool isEditable = true;

        public Visibility ProgressBarVisibility => IsEditable ? Visibility.Collapsed : Visibility.Visible;

        public PalDB DB => db;
    }
}
