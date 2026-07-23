using PalCalc.SaveReader;
using PalCalc.UI.Model;
using PalCalc.UI.Model.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Mapped.Saves
{
    internal static class SavesDetection
    {
        public static IEnumerable<SavesCollectionViewModel> FindAll(AppSettings settings, ISavesService savesService)
        {
            // (only Steam saves are currently supported by `AllLocal`)
            var steamCollections = SteamSaves.CollectAll(DirectSavesLocation.AllLocal);
            if (steamCollections.Count == 0)
                steamCollections.Add(SteamSaves.MakePlaceholderCollection());

            var xboxCollections = XboxSaves.CollectAll(XboxSavesLocation.FindAll());
            if (xboxCollections.Count == 0)
                xboxCollections.Add(XboxSaves.MakePlaceholderCollection());


            var manualSaves = ManualSaves.CollectAll(settings, savesService);
            var fakeSaves = VirtualSaves.CollectAll(settings, savesService);

            return [
                ..steamCollections, ..xboxCollections, manualSaves, fakeSaves
            ];
        }
    }
}
