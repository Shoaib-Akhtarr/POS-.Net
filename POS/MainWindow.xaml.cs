using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using POS.Services;
using POS.ViewModels;

namespace POS
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => ThemeService.ApplySavedPreferences();
        }

        private void LoginOverlay_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                LoginPinBox.Clear();
                Dispatcher.BeginInvoke(() => LoginPinBox.Focus());
            }
        }

        private void LoginPinBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.LoginPin = LoginPinBox.Password;
            }
        }

        private void LoginPinBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is MainViewModel vm)
            {
                vm.SignInSelectedUserCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not MainViewModel vm || vm.IsLoginModalOpen) return;

            if (e.Key == Key.F1)
            {
                vm.NavigateToHomeCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.F2)
            {
                vm.NavigateToPOSCommand.Execute(null);
                Dispatcher.BeginInvoke(() =>
                {
                    var searchBox = FindChildByName<TextBox>(this, "ProductSearchBox");
                    searchBox?.Focus();
                });
                e.Handled = true;
            }
            else if (e.Key == Key.F4)
            {
                vm.NavigateToPOSCommand.Execute(null);
                Dispatcher.BeginInvoke(() =>
                {
                    var custBox = FindChildByName<TextBox>(this, "CustomerSearchBox");
                    custBox?.Focus();
                });
                e.Handled = true;
            }
            else if (e.Key == Key.F8 && vm.CurrentViewModel is POSViewModel posHold)
            {
                CommitFocusedTextBox();
                posHold.HoldCurrentCartCommand.Execute(null);
                e.Handled = true;
            }
            else if ((e.Key == Key.F9 || (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)) &&
                     vm.CurrentViewModel is POSViewModel posCheckout)
            {
                CommitFocusedTextBox();
                posCheckout.CompleteSaleCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CloseTopModal(vm);
            }
        }

        private static void CommitFocusedTextBox()
        {
            if (Keyboard.FocusedElement is TextBox tb)
            {
                BindingOperations.GetBindingExpression(tb, TextBox.TextProperty)?.UpdateSource();
            }
        }

        private static T? FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T fe && fe.Name == name) return fe;
                var nested = FindChildByName<T>(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private static void CloseTopModal(MainViewModel vm)
        {
            switch (vm.CurrentViewModel)
            {
                case POSViewModel pos:
                    pos.IsHeldCartsModalOpen = false;
                    pos.IsItemEditModalOpen = false;
                    pos.IsPackCalcModalOpen = false;
                    pos.IsCustomerDropdownOpen = false;
                    break;
                case HomeViewModel home:
                    home.IsLowStockModalOpen = false;
                    break;
                case ProductsViewModel prod:
                    prod.CloseModalsCommand.Execute(null);
                    break;
                case PurchasesViewModel pur:
                    pur.CloseDetailCommand.Execute(null);
                    break;
                case CustomersViewModel cust:
                    cust.CloseModalsCommand.Execute(null);
                    cust.CloseReceiptCommand.Execute(null);
                    break;
                case RecordsViewModel rec:
                    rec.CloseDetailCommand.Execute(null);
                    rec.CloseReturnModalCommand.Execute(null);
                    break;
            }
        }
    }
}
