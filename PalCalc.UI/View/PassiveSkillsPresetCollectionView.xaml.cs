using PalCalc.UI.ViewModel;
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
    /// Interaction logic for PassiveSkillsPresetCollectionView.xaml
    /// </summary>
    public partial class PassiveSkillsPresetCollectionView : UserControl
    {
        public PassiveSkillsPresetCollectionView()
        {
            InitializeComponent();
        }

        public PassiveSkillsPresetCollectionViewModel ViewModel => DataContext as PassiveSkillsPresetCollectionViewModel;

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_ListBox.SelectedItem != null)
            {
                ViewModel?.SelectPresetCommand?.Execute(m_ListBox.SelectedItem as IPassiveSkillsPresetViewModel);
                m_ListBox.SelectedItem = null;
            }
        }
    }
}
