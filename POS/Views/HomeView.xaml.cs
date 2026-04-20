using System.Windows.Controls;

namespace POS.Views
{
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();
        }
        private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.HomeViewModel vm)
            {
                vm.IsLowStockModalOpen = false;
            }
        }
    }
}
