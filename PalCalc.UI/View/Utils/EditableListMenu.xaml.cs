using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using AdonisMessageBox = AdonisUI.Controls.MessageBox;
using AdonisMessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using AdonisMessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace PalCalc.UI.View.Utils
{
    public class CreateNewItemEntry
    {
        private CreateNewItemEntry() { }

        public static readonly List<CreateNewItemEntry> AsList = [new CreateNewItemEntry()];
    }

    public interface IEditableListItem
    {
        string Name { get; }

        public string DeleteConfirmTitle { get; }
        public string DeleteConfirmMessage { get; }

        public string OverwriteConfirmTitle { get; }
        public string OverwriteConfirmMessage { get; }

        public string RenamePopupTitle { get; }
        public string RenamePopupInputLabel { get; }
    }

    /// <summary>
    /// Interaction logic for EditableListMenu.xaml
    /// </summary>
    public partial class EditableListMenu : UserControl
    {
        public record class SelectCommandArgs(object Item);
        public record class CreateCommandArgs(string NewName);
        public record class DeleteCommandArgs(object Item);
        public record class RenameCommandArgs(object Item, string NewName);
        public record class OverwriteCommandArgs(object Item);

        public static readonly DependencyProperty NewItemContentProperty = DependencyProperty.Register(nameof(NewItemContent), typeof(FrameworkElement), typeof(EditableListMenu));
        public static readonly DependencyProperty ItemContentTemplateProperty = DependencyProperty.Register(nameof(ItemContentTemplate), typeof(DataTemplate), typeof(EditableListMenu));
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable<IEditableListItem>), typeof(EditableListMenu));
        public static readonly DependencyProperty SelectCommandProperty = DependencyProperty.Register(nameof(SelectCommand), typeof(IRelayCommand<SelectCommandArgs>), typeof(EditableListMenu));
        public static readonly DependencyProperty CreateCommandProperty = DependencyProperty.Register(nameof(CreateCommand), typeof(IRelayCommand<CreateCommandArgs>), typeof(EditableListMenu));
        public static readonly DependencyProperty DeleteCommandProperty = DependencyProperty.Register(nameof(DeleteCommand), typeof(IRelayCommand<DeleteCommandArgs>), typeof(EditableListMenu));
        public static readonly DependencyProperty RenameCommandProperty = DependencyProperty.Register(nameof(RenameCommand), typeof(IRelayCommand<RenameCommandArgs>), typeof(EditableListMenu));
        public static readonly DependencyProperty OverwriteCommandProperty = DependencyProperty.Register(nameof(OverwriteCommand), typeof(IRelayCommand<OverwriteCommandArgs>), typeof(EditableListMenu));

        public static readonly DependencyProperty DeleteButtonTooltipProperty = DependencyProperty.Register(nameof(DeleteButtonTooltip), typeof(string), typeof(EditableListMenu));
        public static readonly DependencyProperty RenameButtonTooltipProperty = DependencyProperty.Register(nameof(RenameButtonTooltip), typeof(string), typeof(EditableListMenu));
        public static readonly DependencyProperty OverwriteButtonTooltipProperty = DependencyProperty.Register(nameof(OverwriteButtonTooltip), typeof(string), typeof(EditableListMenu));

        public static readonly DependencyProperty CreatePopupTitleProperty = DependencyProperty.Register(nameof(CreatePopupTitle), typeof(string), typeof(EditableListMenu));
        public static readonly DependencyProperty CreatePopupInputLabelProperty = DependencyProperty.Register(nameof(CreatePopupInputLabel), typeof(string), typeof(EditableListMenu));

        public FrameworkElement NewItemContent
        {
            get => (FrameworkElement)GetValue(NewItemContentProperty);
            set => SetValue(NewItemContentProperty, value);
        }

        public DataTemplate ItemContentTemplate
        {
            get => (DataTemplate)GetValue(ItemContentTemplateProperty);
            set => SetValue(ItemContentTemplateProperty, value);
        }

        public IEnumerable<IEditableListItem> ItemsSource
        {
            get => (IEnumerable<IEditableListItem>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public IRelayCommand<SelectCommandArgs> SelectCommand
        {
            get => (IRelayCommand<SelectCommandArgs>)GetValue(SelectCommandProperty);
            set => SetValue(SelectCommandProperty, value);
        }

        public IRelayCommand<CreateCommandArgs> CreateCommand
        {
            get => (IRelayCommand<CreateCommandArgs>)GetValue(CreateCommandProperty);
            set => SetValue(OverwriteCommandProperty, value);
        }

        public IRelayCommand<DeleteCommandArgs> DeleteCommand
        {
            get => (IRelayCommand<DeleteCommandArgs>)GetValue(DeleteCommandProperty);
            set => SetValue(DeleteCommandProperty, value);
        }

        public IRelayCommand<RenameCommandArgs> RenameCommand
        {
            get => (IRelayCommand<RenameCommandArgs>)GetValue(RenameCommandProperty);
            set => SetValue(RenameCommandProperty, value);
        }

        public string DeleteButtonTooltip
        {
            get => (string)GetValue(DeleteButtonTooltipProperty);
            set => SetValue(DeleteButtonTooltipProperty, value);
        }

        public string RenameButtonTooltip
        {
            get => (string)GetValue(RenameButtonTooltipProperty);
            set => SetValue(RenameButtonTooltipProperty, value);
        }

        public string OverwriteButtonTooltip
        {
            get => (string)GetValue(OverwriteButtonTooltipProperty);
            set => SetValue(OverwriteButtonTooltipProperty, value);
        }

        public string CreatePopupTitle
        {
            get => (string)GetValue(CreatePopupTitleProperty);
            set => SetValue(CreatePopupTitleProperty, value);
        }

        public string CreatePopupInputLabel
        {
            get => (string)GetValue(CreatePopupInputLabelProperty);
            set => SetValue(CreatePopupInputLabelProperty, value);
        }

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button).DataContext as IEditableListItem;
            var existingOptions = ItemsSource.Select(i => i.Name).ToList();
            var renameWindow = new SimpleTextInputWindow()
            {
                Title = item.RenamePopupTitle,
                InputLabel = item.RenamePopupInputLabel,
                Result = item.Name,
                Validator = (name) => name.Length > 0 && !existingOptions.Contains(name),
                Owner = App.Current.MainWindow
            };

            if (renameWindow.ShowDialog() == true)
            {
                var newName = renameWindow.Result;
                RenameCommand?.Execute(new RenameCommandArgs(item, newName));
            }
        }

        private void OverwriteButton_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button).DataContext as IEditableListItem;
            if (AdonisMessageBox.Show(item.OverwriteConfirmMessage, item.OverwriteConfirmTitle, AdonisMessageBoxButton.YesNo) == AdonisMessageBoxResult.Yes)
            {
                OverwriteCommand?.Execute(new OverwriteCommandArgs(item));
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button).DataContext as IEditableListItem;
            if (AdonisMessageBox.Show(item.DeleteConfirmMessage, item.DeleteConfirmTitle, AdonisMessageBoxButton.YesNo) == AdonisMessageBoxResult.Yes)
            {
                DeleteCommand?.Execute(new DeleteCommandArgs(item));
            }
        }

        public IRelayCommand<OverwriteCommandArgs> OverwriteCommand
        {
            get => (IRelayCommand<OverwriteCommandArgs>)GetValue(OverwriteCommandProperty);
            set => SetValue(OverwriteCommandProperty, value);
        }

        private class EditableListMenuItemTemplateSelector(EditableListMenu menu) : DataTemplateSelector
        {
            DataTemplate newItemDataTemplate = menu.FindResource("NewItemDataTemplate") as DataTemplate;
            DataTemplate itemDataTemplate = menu.FindResource("ItemDataTemplate") as DataTemplate;

            public override DataTemplate SelectTemplate(object item, DependencyObject container)
            {
                if (item is CreateNewItemEntry)
                    return newItemDataTemplate;
                else
                    return itemDataTemplate;
            }
        }

        public DataTemplateSelector ItemTemplateSelector { get; }

        public EditableListMenu()
        {
            InitializeComponent();
            ItemTemplateSelector = new EditableListMenuItemTemplateSelector(this);
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_ListBox.SelectedItem != null)
            {
                switch (m_ListBox.SelectedItem)
                {
                    case CreateNewItemEntry:
                        var existingOptions = ItemsSource.Select(i => i.Name).ToList();

                        var newNameWindow = new SimpleTextInputWindow()
                        {
                            Title = CreatePopupTitle,
                            InputLabel = CreatePopupInputLabel,
                            Result = "",
                            Validator = (name) => name.Length > 0 && !existingOptions.Contains(name),
                            Owner = App.Current.MainWindow,
                        };

                        if (newNameWindow.ShowDialog() == true)
                        {
                            var newName = newNameWindow.Result;
                            CreateCommand?.Execute(new CreateCommandArgs(newName));
                        }
                        break;

                    default:
                        SelectCommand?.Execute(new SelectCommandArgs(m_ListBox.SelectedItem));
                        break;
                }

                m_ListBox.SelectedItem = null;
            }
        }
    }
}
