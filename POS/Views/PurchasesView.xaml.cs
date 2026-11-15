using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace POS.Views
{
    public partial class PurchasesView : UserControl
    {
        public PurchasesView()
        {
            InitializeComponent();
            Loaded += PurchasesView_Loaded;
        }

        private void PurchasesView_Loaded(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this);
            if (win != null)
            {
                win.PreviewKeyDown -= Window_PreviewKeyDown;
                win.PreviewKeyDown += Window_PreviewKeyDown;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && DataContext is ViewModels.PurchasesViewModel vm && vm.IsDetailModalOpen)
            {
                vm.IsDetailModalOpen = false;
                e.Handled = true;
            }
        }

        private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.PurchasesViewModel vm)
            {
                vm.IsDetailModalOpen = false;
            }
        }

        private void DataGridRow_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.DataContext is Models.Purchase purchase)
            {
                if (DataContext is ViewModels.PurchasesViewModel vm)
                {
                    vm.OpenDetailCommand.Execute(purchase);
                }
            }
        }

        private void ViewSlipButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Models.Purchase purchase)
            {
                if (DataContext is ViewModels.PurchasesViewModel vm)
                {
                    vm.OpenDetailCommand.Execute(purchase);
                }
            }
        }
    }
}
