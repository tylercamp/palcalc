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
using System.Windows.Threading;

namespace PalCalc.UI.View
{
    /// <summary>
    /// Interaction logic for PalSourceTreeView.xaml
    /// </summary>
    public partial class PalSourceTreeView : UserControl
    {
        public PalSourceTreeView()
        {
            InitializeComponent();
        }

        public PalSourceTreeViewModel ViewModel => DataContext as PalSourceTreeViewModel;

        private bool IsSwitchingDataContext = false;

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(DataContext)) IsSwitchingDataContext = true;

            base.OnPropertyChanged(e);

            IsSwitchingDataContext = false;
            if (e.Property.Name == nameof(DataContext))
            {
                if (e.OldValue != null) (e.OldValue as PalSourceTreeViewModel).PropertyChanged -= ViewModel_PropertyChanged;

                if (e.NewValue != null)
                {
                    ExpandItemContainers(m_TreeView);

                    var newVm = e.NewValue as PalSourceTreeViewModel;
                    if (m_TreeView.SelectedItem != newVm.SelectedSource)
                    {
                        if (ViewModel.SelectedSource == null)
                        {
                            ItemContainerFor(m_TreeView, m_TreeView.SelectedItem).IsSelected = false;
                        }
                        else
                        {
                            SelectById(newVm.SelectedSource.Id);
                        }
                    }

                    newVm.PropertyChanged += ViewModel_PropertyChanged;
                }
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.SelectedNode))
            {
                if (m_TreeView.SelectedItem != ViewModel.SelectedNode)
                    ItemContainerFor(m_TreeView, ViewModel.SelectedNode).IsSelected = true;
            }
        }

        private TreeViewItem ItemContainerFor(ItemsControl container, object node)
        {
            var direct = container.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
            if (direct != null) return direct;

            foreach (var item in container.Items)
            {
                var inner = container.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (inner == null) continue;

                var recursed = ItemContainerFor(inner, node);
                if (recursed != null) return recursed;
            }

            return null;
        }

        private void ExpandItemContainers(ItemsControl container)
        {
            foreach (var item in container.Items)
            {
                var tvi = container.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (tvi == null) continue;

                tvi.IsExpanded = true;
                ExpandItemContainers(tvi);
            }
        }

        public void SelectById(string id)
        {
            var node = ViewModel.FindById(id);
            if (node != null)
            {
                var entry = ItemContainerFor(m_TreeView, node);

                if (entry != null) entry.IsSelected = true;
                else Dispatcher.BeginInvoke(() => SelectById(id), DispatcherPriority.Background);
            }
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ViewModel == null || IsSwitchingDataContext) return;

            if (e.NewValue != null && e.NewValue is not IPalSourceTreeNode) throw new InvalidOperationException();

            ViewModel.SelectedNode = e.NewValue as IPalSourceTreeNode;
        }
    }
}
