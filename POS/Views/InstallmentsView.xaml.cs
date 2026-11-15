using System.Windows.Controls;
using System.Windows.Input;
using POS.ViewModels;

namespace POS.Views
{
    public partial class InstallmentsView : UserControl
    {
        public InstallmentsView() => InitializeComponent();

        private void OnContractRowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is InstallmentsViewModel vm && vm.SelectedContract != null)
            {
                vm.OpenPersonRecordsCommand.Execute(vm.SelectedContract);
            }
        }
    }
}
