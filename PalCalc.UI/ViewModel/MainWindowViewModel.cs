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

        // main app model
        public MainWindowViewModel()
        {
            SaveSelection = new SaveSelectorViewModel(SavesLocation.AllLocal);
            DisplayedResult = new BreedingResultViewModel();
        }

        public SaveSelectorViewModel SaveSelection { get; private set; }
        public BreedingResultViewModel DisplayedResult { get; private set; }

        public PalDB DB => db;
    }
}
