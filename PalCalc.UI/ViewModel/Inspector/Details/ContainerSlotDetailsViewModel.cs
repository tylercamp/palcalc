using PalCalc.Model;
using PalCalc.SaveReader.SaveFile.Support.Level;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PalCalc.UI.ViewModel.Inspector.Details
{
    public interface IContainerSlotDetailsViewModel { }

    public class PalContainerSlotDetailsViewModel(string instanceId, PalInstance pal, GvasCharacterInstance rawPal) : IContainerSlotDetailsViewModel
    {
        public string DisplayName => pal?.Pal?.Name ?? rawPal?.CharacterId ?? InstanceId;
        public ImageSource Icon =>
            pal == null
                ? PalIcon.DefaultIcon
                : PalIcon.Images[pal.Pal];

        public string InstanceId => instanceId;
        public PalInstance Instance => pal;
        public GvasCharacterInstance RawInstance => rawPal;
    }

    public class EmptyPalContainerSlotDetailsViewModel : IContainerSlotDetailsViewModel { }
}
