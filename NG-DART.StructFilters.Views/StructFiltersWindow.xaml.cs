using System.Windows;
using System.Windows.Controls;

namespace NGDartStructFilters.Views {
    /// <summary>
    /// Interaction logic for StructFiltersWindow.xaml
    /// </summary>
    public partial class StructFiltersWindow : Window {
        public StructFiltersWindow() {
            InitializeComponent();
        }

        // Based on https://stackoverflow.com/a/9494484/5652483
        private void StructsTreeView_SelectedItemChanged(object sender, RoutedEventArgs e) {
            if (sender is TreeViewItem item) {
                item.BringIntoView();
                e.Handled = true;
            }
        }

    } // class StructFiltersWindow

} // namespace NGDartStructFilters.Views
