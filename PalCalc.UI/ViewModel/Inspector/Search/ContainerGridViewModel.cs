using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PalCalc.UI.ViewModel.Inspector.Search
{
    public interface IContainerGridSlotViewModel {}

    public class ContainerGridPalSlotViewModel : IContainerGridSlotViewModel
    {
        public PalInstance PalInstance { get; set; }

        public PalViewModel Pal => new PalViewModel(PalInstance.Pal);
    }

    public class ContainerGridEmptySlotViewModel : IContainerGridSlotViewModel { }

    public partial class ContainerGridViewModel : ObservableObject
    {
        private static ContainerGridViewModel designerInstance;
        public static ContainerGridViewModel DesignerInstance
        {
            get
            {
                if (designerInstance == null)
                {
                    var c = CachedSaveGame.SampleForDesignerView.OwnedPals.GroupBy(p => p.Location.ContainerId).First();

                    designerInstance = new ContainerGridViewModel()
                    {
                        PerRow = 5,
                        Contents = c.ToList()
                    };
                }
                return designerInstance;
            }
        }

        [ObservableProperty]
        private int perRow = 5;

        [NotifyPropertyChangedFor(nameof(Slots))]
        [ObservableProperty]
        private List<PalInstance> contents;

        //public int NumRows => (int)Math.Ceiling(Contents.Max(p => p.Location.Index + 1) / (float)PerRow);

        public List<IContainerGridSlotViewModel> Slots => Contents == null ? [] :
            Enumerable
                .Range(0, Contents.Max(p => p.Location.Index) + 1)
                .Select<int, IContainerGridSlotViewModel>(i =>
                {
                    var p = Contents.FirstOrDefault(p => p.Location.Index == i);
                    if (p == null) return new ContainerGridEmptySlotViewModel();
                    else return new ContainerGridPalSlotViewModel() { PalInstance = p };
                })
                .ToList();
    }
}
