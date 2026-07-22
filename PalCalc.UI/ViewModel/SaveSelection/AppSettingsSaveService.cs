using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Model;
using PalCalc.UI.View.Inspector;
using PalCalc.UI.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal class AppSettingsSaveService(AppSettings settings) : ISavesService
    {
        public void AddManualSave(StandardSaveGame manualSave)
        {
            settings.ExtraSaveLocations.Add(manualSave.BasePath);
            Storage.SaveAppSettings(settings);
        }

        public void AddVirtualSave(VirtualSaveGame virtualSave)
        {
            settings.FakeSaveNames.Add(FakeSaveGame.GetLabel(virtualSave));
            Storage.SaveAppSettings(settings);
        }

        public void RemoveManualSave(StandardSaveGame manualSave)
        {
            SaveInspectorWindowManager.CloseAll(manualSave);
            SaveCustomizationsViewModel.RemoveFor(manualSave);
            settings.ExtraSaveLocations.Remove(manualSave.BasePath);
            Storage.SaveAppSettings(settings);

            Storage.RemoveSave(manualSave);
            manualSave.Dispose();
        }

        public void RemoveVirtualSave(VirtualSaveGame virtualSave)
        {
            SaveInspectorWindowManager.CloseAll(virtualSave);
            SaveCustomizationsViewModel.RemoveFor(virtualSave);

            settings.FakeSaveNames.Remove(FakeSaveGame.GetLabel(virtualSave));

            Storage.RemoveSave(virtualSave);
            virtualSave.Dispose();

            Storage.SaveAppSettings(settings);
        }
    }
}
