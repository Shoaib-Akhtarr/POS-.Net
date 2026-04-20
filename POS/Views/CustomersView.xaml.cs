using System.Windows.Controls;

namespace POS.Views
{
    public partial class CustomersView : UserControl
    {
        public CustomersView()
        {
            InitializeComponent();
        }

        private void ArchiveRow_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.DataContext is Models.Sale sale)
            {
                if (DataContext is ViewModels.CustomersViewModel vm)
                {
                    vm.OpenReceiptCommand.Execute(sale);
                }
            }
        }
    }
}
