using System.Windows.Controls;

namespace POS.Views
{
    public partial class PurchasesView : UserControl
    {
        public PurchasesView()
        {
            InitializeComponent();
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
    }
}
