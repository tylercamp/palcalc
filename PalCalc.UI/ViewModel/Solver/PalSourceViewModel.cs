using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.ViewModel.SaveSelection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Solver
{
    public partial class PalSourceViewModel : ObservableObject
    {
        private SaveGameViewModel sourceSave;

        public PalSourceViewModel(SaveGameViewModel sourceSave, List<IPalSourceTreeSelection> initialTreeSelections)
        {
            this.sourceSave = sourceSave;
            PlayerSources = new PalSourceTreeViewModel(sourceSave.CachedValue);

            if (initialTreeSelections != null)
                PlayerSources.Selections = initialTreeSelections;

            PropertyChangedEventManager.AddHandler(PlayerSources, OnTreeSelectionChanged, nameof(PlayerSources.Selections));
        }

        private void OnTreeSelectionChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(AvailablePals));
        }

        public PalSourceTreeViewModel PlayerSources { get; }

        [NotifyPropertyChangedFor(nameof(AvailablePals))]
        [ObservableProperty]
        private bool includeBasePals = true;

        [NotifyPropertyChangedFor(nameof(AvailablePals))]
        [ObservableProperty]
        private bool includeCustomPals = true;

        [NotifyPropertyChangedFor(nameof(AvailablePals))]
        [ObservableProperty]
        private bool includeCagedPals = true;

        [NotifyPropertyChangedFor(nameof(AvailablePals))]
        [ObservableProperty]
        private bool includeGlobalStoragePals = true;

        [NotifyPropertyChangedFor(nameof(AvailablePals))]
        [ObservableProperty]
        private bool includeExpeditionPals = true;

        public IEnumerable<PalInstance> AvailablePals
        {
            get
            {
                if (PlayerSources?.Selections != null)
                {
                    var cachedSave = sourceSave.CachedValue;
                    var selections = PlayerSources.Selections;
                    foreach (var pal in cachedSave.OwnedPals)
                    {
                        if (!selections.Any(s => s.Matches(cachedSave, pal)))
                            continue;

                        if (pal.Location.Type == LocationType.GlobalPalStorage)
                            continue;

                        if (!IncludeBasePals && pal.Location.Type == LocationType.Base)
                            continue;

                        if (!IncludeCagedPals && pal.Location.Type == LocationType.ViewingCage)
                            continue;

                        if (!IncludeExpeditionPals && pal.IsOnExpedition)
                            continue;

                        yield return pal;
                    }
                }

                if (IncludeGlobalStoragePals)
                {
                    foreach (var pal in sourceSave.CachedValue.OwnedPals.Where(p => p.Location.Type == LocationType.GlobalPalStorage))
                    {
                        yield return pal;
                    }
                }

                if (IncludeCustomPals)
                {
                    foreach (var pal in sourceSave.Customizations.CustomContainers.SelectMany(c => c.Contents))
                        if (pal.IsValid)
                            yield return pal.ModelObject;
                }
            }
        }
    }
}
