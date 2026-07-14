using PalCalc.SaveReader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal interface ISavesService
    {
        SaveGameViewModel2? TryAddSave(SaveType type);

        bool TryRemoveSave(SaveGameViewModel2 save);

        void OpenSavesLocationFolder(SavesCollectionViewModel location);
        void OpenSaveGameFolder(SaveGameViewModel2 save);
    }
}
