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
    public class TraitCollectionViewModel
    {
        // for XAML designer view
        public TraitCollectionViewModel() : this(
            new List<TraitViewModel>()
            {
                new TraitViewModel(),
                new TraitViewModel(),
                new TraitViewModel(),
            })
        {
        }

        public TraitCollectionViewModel(List<TraitViewModel> traits)
        {
            Traits = traits;

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
        }

        public List<TraitViewModel> Traits { get; }

        public int Spacing => 3;

        public int EntriesPerRow => 2;

        public int NumRows
        {
            get
            {
                if (Traits.Count <= 2) return 1;
                else return 3;
            }
        }

        public List<RowDefinition> RowDefinitions { get; } = new List<RowDefinition>();
        public List<ColumnDefinition> ColumnDefinitions { get; } = new List<ColumnDefinition>();

        public int RowIndexOf(TraitViewModel trait)
        {
            var mainRow = Traits.IndexOf(trait) / EntriesPerRow;
            if (mainRow == 0) return mainRow;
            else return mainRow + 1;
        }

        public int ColumnIndexOf(TraitViewModel trait)
        {
            var mainColumn = Traits.IndexOf(trait) % EntriesPerRow;
            if (mainColumn == 0) return mainColumn;
            else return mainColumn + 1;
        }
    }
}
