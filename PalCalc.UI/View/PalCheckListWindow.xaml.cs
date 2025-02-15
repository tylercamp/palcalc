using AdonisUI.Controls;
using PalCalc.UI.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Shapes;
using static QuickGraph.Algorithms.AssigmentProblem.HungarianAlgorithm;

namespace PalCalc.UI.View
{
    /// <summary>
    /// Interaction logic for PalCheckListWindow.xaml
    /// </summary>
    public partial class PalCheckListWindow : AdonisWindow
    {
        public PalCheckListWindow()
        {
            InitializeComponent();

            Wpf.Util.GridViewSort.ApplySort(m_ListView.Items, nameof(PalCheckListEntryViewModel.PaldexNoValue));
        }
    }
}
