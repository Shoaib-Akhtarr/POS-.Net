using System.Windows.Controls;
using POS.ViewModels;

namespace POS.Views
{
    public partial class POSView : UserControl
    {
        public POSView()
        {
            InitializeComponent();
        }

        private void AmountPaidBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb && tb.IsKeyboardFocusWithin && DataContext is POSViewModel vm)
            {
                vm.MarkCustomPaymentCommand.Execute(null);
            }
        }
    }
}
