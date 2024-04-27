using PalCalc.Model;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PalCalc.UI.ViewModel.Mapped
{
    public class TraitViewModel
    {
        public static Dictionary<int, Color> RankColors = new Dictionary<int, Color>()
        {
            { -3, new Color() { R = 247, G = 63, B = 63, A = 255 } },
            { -2, new Color() { R = 247, G = 63, B = 63, A = 255 } },
            { -1, new Color() { R = 247, G = 63, B = 63, A = 255 } },
            { 0, new Color() { R = 230, G = 231, B = 223, A = 255 } },
            { 1, new Color() { R = 230, G = 231, B = 223, A = 255 } },
            { 2, new Color() { R = 255, G = 221, B = 0, A = 255 } },
            { 3, new Color() { R = 255, G = 221, B = 0, A = 255 } },
        };

        // for XAML designer view
        public TraitViewModel()
        {
            ModelObject = new Trait("Runner", "runner", 2);
        }

        public TraitViewModel(Trait trait)
        {
            ModelObject = trait;
        }

        public Trait ModelObject { get; }

        public ImageSource RankIcon => TraitIcon.Images[ModelObject.Rank];
        public Color RankColor => RankColors[ModelObject.Rank];

        public string Name => ModelObject?.Name ?? "None";

        public override bool Equals(object obj) => ModelObject.Equals((obj as TraitViewModel)?.ModelObject);
        public override int GetHashCode() => ModelObject.GetHashCode();
    }
}
