using PalCalc.SaveReader;
using PalCalc.UI2.ViewModels.Mapped;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.UI2.ViewModel
{
    internal partial class MainVM : BaseVM
    {
        public MainVM()
        {
            var availableSavesLocations = new List<ISavesLocation>();
            availableSavesLocations.AddRange(DirectSavesLocation.AllLocal);

            var xboxLocations = XboxSavesLocation.FindAll();
            if (xboxLocations.Count > 0) availableSavesLocations.AddRange(xboxLocations);
            else
            {
                // add a placeholder so the user can (optionally) see the explanation why no saves are available (game isn't installed/synced via xbox app)
                availableSavesLocations.Add(new XboxSavesLocation());
            }

            SaveSelector = new SaveSelectorVM(
                addManualSaveCommand: null,
                savesLocations: availableSavesLocations.Select(
                    l => new StandardSavesLocationVM(l, l.ValidSaveGames.Select(g => new SaveGameVM(g)))
                ),
                manualSaves: Enumerable.Empty<SaveGameVM>() // TODO
            );
        }

        public SaveSelectorVM SaveSelector { get; }
    }
}