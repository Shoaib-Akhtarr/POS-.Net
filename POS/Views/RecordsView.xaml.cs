using System.Windows.Controls;
using POS.ViewModels;

namespace POS.Views
{
    public partial class RecordsView : UserControl
    {
        public RecordsView()
        {
            InitializeComponent();
        }

        private void RecordRow_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.DataContext is UnifiedRecord record)
            {
                if (DataContext is RecordsViewModel vm)
                {
                    vm.OpenDetailCommand.Execute(record);
                }
            }
        }
    }
}
