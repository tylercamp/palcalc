using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.ViewModel;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace PalCalc.UI.Model
{
    public partial class BreedingTreeNodeViewModel : ObservableObject
    {
        private PalDB db;
        private SaveGameViewModel latestSave;
        public BreedingTreeNodeViewModel(SaveGameViewModel latestSave, CachedSaveGame source, IBreedingTreeNode node)
        {
            this.latestSave = latestSave;

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

        bool didInitOutdated = false;
        private void InitOutdatedInfo()
        {
            if (didInitOutdated) return;

            var originalInstance = (Value.PalRef as OwnedPalReference)?.UnderlyingInstance;

            outdatedErrorNoticeVisibility = Visibility.Collapsed;
            outdatedWarningNoticeVisibility = Visibility.Collapsed;

            if (originalInstance != null)
            {
                var latestInstance = latestSave.CachedValue.OwnedPals.FirstOrDefault(p => p.InstanceId == originalInstance.InstanceId);
                if (latestInstance == null)
                {
                    outdatedErrorNoticeVisibility = Visibility.Visible;
                    outdatedNoticeReason = "The referenced pal no longer exists";
                }
                else if (latestInstance.Location != originalInstance.Location)
                {
                    outdatedWarningNoticeVisibility = Visibility.Visible;
                    outdatedNoticeReason = "The referenced pal has been moved elsewhere";
                }
            }
        }

        // (tried to implement this with an IConverter but couldn't get the bindings to work right)
        private Visibility outdatedErrorNoticeVisibility;
        public Visibility OutdatedErrorNoticeVisibility
        {
            get
            {
                InitOutdatedInfo();
                return outdatedErrorNoticeVisibility;
            }
        }

        private Visibility outdatedWarningNoticeVisibility;
        public Visibility OutdatedWarningNoticeVisibility
        {
            get
            {
                InitOutdatedInfo();
                return outdatedWarningNoticeVisibility;
            }
        }

        private string outdatedNoticeReason;
        public string OutdatedNoticeReason
        {
            get
            {
                InitOutdatedInfo();
                return outdatedNoticeReason;
            }
        }

        public PalRefLocationViewModel Location { get; }

        public string Gender { get; }
    }
}
