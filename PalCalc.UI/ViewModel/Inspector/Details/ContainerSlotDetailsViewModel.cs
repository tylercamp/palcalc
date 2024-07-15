using PalCalc.Model;
using PalCalc.SaveReader.SaveFile.Support.Level;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
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
        public ILocalizedText DisplayName { get; } =
            pal != null
                ? new PalViewModel(pal.Pal).Name
                : new HardCodedText(rawPal?.CharacterId ?? instanceId);

        public ImageSource Icon => pal == null ? PalIcon.DefaultIcon : PalIcon.Images[pal.Pal];
        public ImageBrush IconBrush => pal == null ? PalIcon.DefaultIconBrush : PalIcon.ImageBrushes[pal.Pal];

        public string InstanceId => instanceId;
        public PalInstance Instance => pal;
        public GvasCharacterInstance RawInstance => rawPal;
    }

    public class EmptyPalContainerSlotDetailsViewModel : IContainerSlotDetailsViewModel { }
}
