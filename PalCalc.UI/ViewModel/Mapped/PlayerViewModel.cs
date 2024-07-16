using PalCalc.Model;
using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Mapped
{
    public class PlayerViewModel
    {
        public PlayerViewModel(PlayerInstance player)
        {
            ModelObject = player;
            Name = IsWildcard
                ? LocalizationCodes.LC_ANY_PLAYER.Bind()
                : new HardCodedText(ModelObject.Name);
        }

        public PlayerInstance ModelObject { get; }

        public bool IsWildcard => ModelObject == null;

        public ILocalizedText Name { get; }

        public static readonly PlayerViewModel Any = new PlayerViewModel(null);
    }
}
