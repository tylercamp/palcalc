using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel.GraphSharp
{
    public partial class BreedingTreeNodeViewModel : ObservableObject
    {
        private PalDB db;
        public BreedingTreeNodeViewModel(CachedSaveGame source, IBreedingTreeNode node)
        {
            Value = node;
            Pal = new PalViewModel(node.PalRef.Pal);
            Traits = node.PalRef.Traits.Select(t => new TraitViewModel(t)).ToList();
            TraitCollection = new TraitCollectionViewModel(Traits);
            Location = new PalRefLocationViewModel(source, node.PalRef.Location);
            Gender = node.PalRef.Gender.ToString();
        }

        public PalViewModel Pal { get; }

        public IBreedingTreeNode Value { get; }

        public List<TraitViewModel> Traits { get; }

        public TraitCollectionViewModel TraitCollection { get; }

        public Visibility TraitsVisibility => Traits.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EffortVisibility => Value.PalRef.BreedingEffort > TimeSpan.Zero ? Visibility.Visible : Visibility.Collapsed;
        public string Effort
        {
            get
            {
                var effort = Value.PalRef.BreedingEffort;
                var selfEffort = Value.PalRef.SelfBreedingEffort;

                if (effort != selfEffort && Value.PalRef is BredPalReference) return $"{selfEffort.TimeSpanMinutesStr()} ({effort.TimeSpanMinutesStr()})";
                else return effort.TimeSpanMinutesStr();
            }
        }

        public PalRefLocationViewModel Location { get; }

        public string Gender { get; }
    }
}
