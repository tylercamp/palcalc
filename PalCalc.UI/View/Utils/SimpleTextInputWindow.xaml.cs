using AdonisUI.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json.Serialization;
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

namespace PalCalc.UI.View.Utils
{
    /// <summary>
    /// Interaction logic for SimpleTextInputWindow.xaml
    /// </summary>
    [ObservableObject]
    public partial class SimpleTextInputWindow : AdonisWindow
    {
        public SimpleTextInputWindow() : this("") { }

        public SimpleTextInputWindow(string defaultValue)
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

            // note: must be AFTER the SaveCommand/CancelCommand assignment since we don't raise property changed
            //       events for those, view will onnly fetch these values once
            InitializeComponent();

            Result = defaultValue;

            m_TextBox.Focus();

            if (defaultValue.Length > 0)
                m_TextBox.SelectAll();
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

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelCommand.Execute(null);
            }
            else
            {
                base.OnPreviewKeyDown(e);
            }
        }

        private void m_TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && SaveCommand.CanExecute(null))
            {
                SaveCommand.Execute(null);
            }
        }
    }
}
