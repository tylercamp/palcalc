using PalCalc.Model;
using PalCalc.UI.Localization;
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
        private static readonly DerivedLocalizableText<Trait> NameLocalizer = new DerivedLocalizableText<Trait>(
            (locale, trait) => trait switch
            {
                RandomTrait => Translator.Localizations[locale][LocalizationCodes.LC_RANDOM_TRAIT],
                _ => trait.LocalizedNames?.GetValueOrElse(locale.ToFormalName(), trait.Name) ?? trait.InternalName
            }
        );

        private static Dictionary<Trait, TraitViewModel> instances;
        private static Dictionary<Trait, TraitViewModel> Instance
        {
            get
            {
                if (instances == null)
                {
                    instances = PalDB.LoadEmbedded().Traits.ToDictionary(t => t, t => new TraitViewModel(t, NameLocalizer.Bind(t)));
                }
                return instances;
            }
        }

        private static ILocalizedText randomTraitLabel;
        public static TraitViewModel Make(Trait trait)
        {
            if (trait is RandomTrait)
            {
                randomTraitLabel ??= NameLocalizer.Bind(trait);

                return new TraitViewModel(trait, randomTraitLabel);
            }
            else
            {
                return Instance[trait];
            }
        }

        // for XAML designer view
        public TraitViewModel() : this(new Trait("Runner", "runner", 2), new HardCodedText("Runner"))
        {
        }

        private int hash;
        private static Random random = new Random();
        private TraitViewModel(Trait trait, ILocalizedText name)
        {
            ModelObject = trait;
            Name = name;

            if (trait is RandomTrait) hash = random.Next();
            else hash = trait.GetHashCode();
        }

        public Trait ModelObject { get; }

        public ImageSource RankIcon => TraitIcon.Images[ModelObject.Rank];

        public int Rank => ModelObject.Rank;

        public ILocalizedText Name { get; }

        public override bool Equals(object obj) => ModelObject is RandomTrait ? ReferenceEquals(this, obj) : ModelObject.Equals((obj as TraitViewModel)?.ModelObject);

        public override int GetHashCode() => hash;
    }
}
