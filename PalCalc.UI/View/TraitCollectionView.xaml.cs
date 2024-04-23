using PalCalc.UI.ViewModel;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PalCalc.UI.View
{
    /// <summary>
    /// Interaction logic for TraitCollectionView.xaml
    /// </summary>
    public partial class TraitCollectionView : Grid
    {
        public TraitCollectionView()
        {
            InitializeComponent();

            DataContextChanged += TraitCollectionView_DataContextChanged;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            var vm = DataContext as TraitCollectionViewModel;
            if (vm == null) return;

            foreach (var child in Children)
            {
                var traitView = child as TraitView;
                if (traitView == null) continue;

                // force child sizing (otherwise the '*' column sizing doesn't stay proportional)
                traitView.Width = (ActualWidth - vm.Spacing) / 2;
            }
        }

        private void TraitCollectionView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var newModel = e.NewValue as TraitCollectionViewModel;
            if (newModel == null) return;

            Children.Clear();

            // (though we clear the set of row/column definitions, we can still get an exception due
            // to "reuse" of VM Row/ColumnDefinitions, so we make copies here instead of direct refs)

            ColumnDefinitions.Clear();
            foreach (var cdef in newModel.ColumnDefinitions)
                ColumnDefinitions.Add(new ColumnDefinition() { Width = cdef.Width });

            RowDefinitions.Clear();
            foreach (var rdef in newModel.RowDefinitions)
                RowDefinitions.Add(new RowDefinition() { Height = rdef.Height });

            

            foreach (var vm in newModel.Traits)
            {
                var traitView = new TraitView();
                traitView.DataContext = vm;

                Grid.SetRow(traitView, newModel.RowIndexOf(vm));
                Grid.SetColumn(traitView, newModel.ColumnIndexOf(vm));

                Children.Add(traitView);
            }
        }
    }
}
