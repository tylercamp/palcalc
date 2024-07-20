using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PalCalc.UI.ViewModel
{
    public class PassiveSkillCollectionViewModel
    {
        // for XAML designer view
        public PassiveSkillCollectionViewModel() : this(
            new List<PassiveSkillViewModel>()
            {
                new PassiveSkillViewModel(),
                new PassiveSkillViewModel(),
                new PassiveSkillViewModel(),
            })
        {
        }

        public PassiveSkillCollectionViewModel(IEnumerable<PassiveSkillViewModel> passives)
        {
            Passives = passives.OrderBy(t => t.ModelObject.InternalName).ToList();

            RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            for (int i = 1; i < NumRows; i++)
            {
                if (i % 2 == 1)
                {
                    RowDefinitions.Add(new RowDefinition() { Height = new GridLength(Spacing) });
                }
                else
                {
                    RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                }
            }

            ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(Spacing) });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

            if (!Passives.Any())
            {
                Description = LocalizationCodes.LC_TRAITS_COUNT_EMPTY.Bind();
            }
            else
            {
                var definite = Passives.Where(t => t.ModelObject is not IUnknownPassive);
                var random = Passives.Where(t => t.ModelObject is RandomPassiveSkill);
                var unrecognized = Passives.Where(t => t.ModelObject is UnrecognizedPassiveSkill);

                var parts = new List<ILocalizedText>(definite.Select(t => t.Name));

                if (random.Any())
                    parts.Add(
                        LocalizationCodes.LC_TRAITS_COUNT_RANDOM.Bind(random.Count())
                    );

                if (unrecognized.Any())
                    parts.Add(
                        LocalizationCodes.LC_TRAITS_COUNT_UNRECOGNIZED.Bind(unrecognized.Count())
                    );

                Description = Translator.Join.Bind(parts);
            }

            RequiredDescription = LocalizationCodes.LC_REQUIRED_TRAITS_SUMMARY.Bind(Description);
            OptionalDescription = LocalizationCodes.LC_OPTIONAL_TRAITS_SUMMARY.Bind(Description);
        }

        public ILocalizedText Description { get; }

        public ILocalizedText RequiredDescription { get; }
        public ILocalizedText OptionalDescription { get; }

        public List<PassiveSkillViewModel> Passives { get; }

        public int Spacing => 3;

        public int EntriesPerRow => 2;

        public int NumRows
        {
            get
            {
                if (Passives.Count <= 2) return 1;
                else return 3;
            }
        }

        public List<RowDefinition> RowDefinitions { get; } = new List<RowDefinition>();
        public List<ColumnDefinition> ColumnDefinitions { get; } = new List<ColumnDefinition>();

        public int RowIndexOf(PassiveSkillViewModel passive)
        {
            var mainRow = Passives.IndexOf(passive) / EntriesPerRow;
            if (mainRow == 0) return mainRow;
            else return mainRow + 1;
        }

        public int ColumnIndexOf(PassiveSkillViewModel passive)
        {
            var mainColumn = Passives.IndexOf(passive) % EntriesPerRow;
            if (mainColumn == 0) return mainColumn;
            else return mainColumn + 1;
        }
    }
}
