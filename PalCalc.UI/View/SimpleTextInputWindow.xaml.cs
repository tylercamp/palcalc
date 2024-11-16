using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

namespace PalCalc.UI.View
{
    /// <summary>
    /// Interaction logic for SimpleTextInputWindow.xaml
    /// </summary>
    [ObservableObject]
    public partial class SimpleTextInputWindow : Window
    {
        public SimpleTextInputWindow()
        {
            SaveCommand = new RelayCommand(
                execute: () =>
                {
                    DialogResult = true;
                    this.Close();
                },
                canExecute: () =>
                {
                    return Result != null && (Validator == null || Validator(Result));
                }
            );

            CancelCommand = new RelayCommand(
                execute: () =>
                {
                    DialogResult = false;
                    Result = null;
                    this.Close();
                }
            );

            InitializeComponent();

            m_TextBox.Focus();
        }

        public delegate bool ValidatorCallback(string value);

        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        [ObservableProperty]
        private string result;

        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        [ObservableProperty]
        private ValidatorCallback validator;

        [ObservableProperty]
        private string inputLabel = "Test Label";

        public IRelayCommand SaveCommand { get; }
        public IRelayCommand CancelCommand { get; }
    }
}
