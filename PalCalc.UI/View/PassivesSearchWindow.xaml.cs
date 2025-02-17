using AdonisUI.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using FuzzySharp;
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
using System.Windows.Shapes;

namespace PalCalc.UI.View
{
    [ObservableObject]
    public partial class PassivesSearchWindow : AdonisWindow
    {
        [NotifyPropertyChangedFor(nameof(DisplayedOptions))]
        [ObservableProperty]
        private string searchText;

        private bool Matches(string text) => text.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || Fuzz.WeightedRatio(SearchText, text) > 70;

        public List<PassiveSkillViewModel> DisplayedOptions =>
            PassiveSkillViewModel.All.Where(p =>
                string.IsNullOrEmpty(SearchText) ||
                Matches(p.Name.Value) ||
                Matches(p.Description.Value)
            ).ToList();

        public PassivesSearchWindow()
        {
            InitializeComponent();

            Loaded += (_, _) => m_TextBox.Focus();
        }
    }
}
