using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal class AppSettingsSaveService : ISavesService
    {
        private void OpenInExplorer(string path)
        {
            var fullPath = System.IO.Path.GetFullPath(path);
            Process.Start("explorer.exe", fullPath);
        }

        public SaveGameViewModel2 TryAddSave(SaveType type)
        {
            throw new NotImplementedException();
        }

        public bool TryRemoveSave(SaveGameViewModel2 save)
        {
            throw new NotImplementedException();
        }

        public void OpenSavesLocationFolder(SavesCollectionViewModel location)
        {
            throw new NotImplementedException();
        }

        public void OpenSaveGameFolder(SaveGameViewModel2 save)
        {
            throw new NotImplementedException();
        }
    }
}
