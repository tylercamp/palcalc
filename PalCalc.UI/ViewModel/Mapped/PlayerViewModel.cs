using PalCalc.Model;
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
        }

        public PlayerInstance ModelObject { get; }

        public bool IsWildcard => ModelObject == null;

        public string Name => IsWildcard ? "Any Player" : ModelObject.Name;

        public static readonly PlayerViewModel Any = new PlayerViewModel(null);
    }
}
