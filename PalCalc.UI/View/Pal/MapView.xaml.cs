using PalCalc.UI.ViewModel.Mapped;
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

namespace PalCalc.UI.View.Pal
{
    /// <summary>
    /// Interaction logic for MapView.xaml
    /// </summary>
    public partial class MapView : Grid
    {
        public MapView()
        {
            DataContextChanged += MapView_DataContextChanged;

            InitializeComponent();
        }

        private void MapView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var newModel = e.NewValue as MapCoordViewModel;
            if (newModel == null) return;

            ColumnDefinitions.Clear();
            RowDefinitions.Clear();

            foreach (var cdef in newModel.GridColumns)
                ColumnDefinitions.Add(new ColumnDefinition() { Width = cdef.Width });

            foreach (var rdef in newModel.GridRows)
                RowDefinitions.Add(new RowDefinition() { Height = rdef.Height });
        }
    }
}
