using AdonisUI.Controls;
using System.ComponentModel;

namespace PalCalc.UI.View.Main
{
    /// <summary>
    /// Interaction logic for SolverToolWindow.xaml
    /// </summary>
    public partial class SolverToolWindow : AdonisWindow
    {
        public SolverToolWindow()
        {
            InitializeComponent();
        }

        // (same as `SaveInspectorWindow`)
        protected override void OnClosing(CancelEventArgs e)
        {
            Owner = null;
            base.OnClosing(e);
        }
    }
}
