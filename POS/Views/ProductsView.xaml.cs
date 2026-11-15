using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using POS.Models;
using POS.ViewModels;

namespace POS.Views
{
    public partial class ProductsView : UserControl
    {
        public ProductsView()
        {
            InitializeComponent();
        }

        private void DataGridRow_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            // If the user clicked inside an action Button (e.g., Delete button), do not trigger row edit
            DependencyObject? dep = e.OriginalSource as DependencyObject;
            while (dep != null && dep is not DataGridRow)
            {
                if (dep is Button) return;
                dep = VisualTreeHelper.GetParent(dep);
            }

            if (sender is DataGridRow row && row.Item is Product product)
            {
                if (DataContext is ProductsViewModel vm)
                {
                    vm.OpenEditModalCommand.Execute(product);
                }
            }
        }
    }
}
