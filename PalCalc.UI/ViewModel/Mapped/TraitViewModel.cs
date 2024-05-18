using PalCalc.Model;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace PalCalc.UI.ViewModel.Mapped
{
    public class RankColorConverter : IValueConverter
    {
        private Dictionary<Color, SolidColorBrush> brushes = new Dictionary<Color, SolidColorBrush>();

        private SolidColorBrush MakeBrush(Color color) => brushes.ContainsKey(color) ? brushes[color] : brushes[color] = new SolidColorBrush(color) { Opacity = 1 };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return MakeBrush((int)value switch
            {
                < 0 => new Color() { R = 247, G = 63, B = 63, A = 255 },
                > 1 => new Color() { R = 255, G = 221, B = 0, A = 255 },
                _ => new Color() { R = 230, G = 231, B = 223, A = 255 },
            });
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TraitViewModel
    {

        // for XAML designer view
        public TraitViewModel()
        {
            ModelObject = new Trait("Runner", "runner", 2);
        }

        private int hash;
        private static Random random = new Random();
        public TraitViewModel(Trait trait)
        {
            ModelObject = trait;

            if (trait is RandomTrait) hash = random.Next();
            else hash = trait.GetHashCode();
        }

        public Trait ModelObject { get; }

        public ImageSource RankIcon => TraitIcon.Images[ModelObject.Rank];

        public int Rank => ModelObject.Rank;
        
        public string Name => ModelObject?.Name ?? "None";

        public override bool Equals(object obj) => ModelObject is RandomTrait ? ReferenceEquals(this, obj) : ModelObject.Equals((obj as TraitViewModel)?.ModelObject);

        public override int GetHashCode() => hash;
    }
}
