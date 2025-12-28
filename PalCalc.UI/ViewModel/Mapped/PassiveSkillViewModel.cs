using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using Windows.UI.Notifications;

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
                4 => new Color() { R = 104, G = 255, B = 216, A = 255 },
                > 1 => new Color() { R = 255, G = 221, B = 0, A = 255 },
                _ => new Color() { R = 230, G = 231, B = 223, A = 255 },
            });
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PassiveSkillViewModel
    {
        private static readonly DerivedLocalizableText<PassiveSkill> NameLocalizer = new DerivedLocalizableText<PassiveSkill>(
            (locale, passive) => passive switch
            {
                RandomPassiveSkill => Translator.Localizations[locale][LocalizationCodes.LC_RANDOM_TRAIT],
                _ => passive.LocalizedNames?.GetValueOrElse(locale.ToFormalName(), passive.Name) ?? passive.InternalName
            }
        );

        private static readonly DerivedLocalizableText<PassiveSkill> DescriptionLocalizer = new DerivedLocalizableText<PassiveSkill>(
            (locale, passive) => passive switch
            {
                RandomPassiveSkill => null,
                _ => passive.LocalizedDescriptions?.GetValueOrElse(locale.ToFormalName(), passive.Description)
            }
        );

        private static Dictionary<string, PassiveSkillViewModel> unrecognizedInstances = new();
        private static Dictionary<PassiveSkill, PassiveSkillViewModel> instances;
        private static ILocalizedText randomPassiveLabel;
        public static PassiveSkillViewModel Make(PassiveSkill passive)
        {
            if (passive == null) return null;

            if (instances == null)
            {
                instances = PalDB.LoadEmbedded().StandardPassiveSkills.ToDictionary(
                    t => t,
                    t => new PassiveSkillViewModel(t, NameLocalizer.Bind(t), DescriptionLocalizer.Bind(t))
                );
            }

            if (passive is RandomPassiveSkill)
            {
                randomPassiveLabel ??= NameLocalizer.Bind(passive);

                return new PassiveSkillViewModel(passive, randomPassiveLabel, null);
            }
            else if (passive is UnrecognizedPassiveSkill)
            {
                if (!unrecognizedInstances.TryGetValue(passive.InternalName, out PassiveSkillViewModel value))
                {
                    var name = LocalizationCodes.LC_TRAIT_LABEL_UNRECOGNIZED.Bind(passive.InternalName);
                    value = new PassiveSkillViewModel(passive, name, null);
                    unrecognizedInstances.Add(passive.InternalName, value);
                    allPassives.Add(value);
                }

                return value;
            }
            else
            {
                return instances[passive];
            }
        }

        static PassiveSkillViewModel()
        {
            allPassives = new ObservableCollection<PassiveSkillViewModel>(PalDB.LoadEmbedded().StandardPassiveSkills.Select(Make).OrderBy(p => p.Name.Value));

            All = new ReadOnlyObservableCollection<PassiveSkillViewModel>(allPassives);
        }

        private static ObservableCollection<PassiveSkillViewModel> allPassives;
        public static ReadOnlyObservableCollection<PassiveSkillViewModel> All { get; }

        // for XAML designer view
        public PassiveSkillViewModel() : this(new PassiveSkill("Runner", "runner", 2), new HardCodedText("Runner"), new HardCodedText("Runner description"))
        {
        }

        private int hash;
        private static Random random = new Random();
        private PassiveSkillViewModel(PassiveSkill passive, ILocalizedText name, ILocalizedText description)
        {
            ModelObject = passive;
            Name = name;
            Description = description;

            if (passive is RandomPassiveSkill) hash = random.Next();
            else hash = passive.GetHashCode();
        }

        public PassiveSkill ModelObject { get; }

        public ImageSource RankIcon => PassiveSkillIcon.Images[ModelObject.Rank];

        public int Rank => ModelObject.Rank;

        public ILocalizedText Name { get; }

        public ILocalizedText Description { get; }

        public override bool Equals(object obj) => ModelObject is RandomPassiveSkill ? ReferenceEquals(this, obj) : ModelObject.Equals((obj as PassiveSkillViewModel)?.ModelObject);

        public override int GetHashCode() => hash;
    }
}
