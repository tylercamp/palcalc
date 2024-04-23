using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.Model
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

        public PalRefLocationViewModel Location { get; }

        public string Gender { get; }
    }
}
