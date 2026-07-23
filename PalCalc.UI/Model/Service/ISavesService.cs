using PalCalc.SaveReader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Model.Service
{
    public interface ISavesService
    {
        void AddVirtualSave(VirtualSaveGame virtualSave);

        void AddManualSave(StandardSaveGame manualSave);

        void RemoveVirtualSave(VirtualSaveGame virtualSave);

        void RemoveManualSave(StandardSaveGame manualSave);
    }
}
