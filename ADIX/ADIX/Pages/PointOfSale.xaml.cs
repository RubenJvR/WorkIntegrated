using ADIX.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ADIX
{
    public partial class PointOfSale : Page
    {
        private PointOfSaleViewModel _viewModel;

        public PointOfSale()
        {
            InitializeComponent();
            _viewModel = ViewModelManager.PointOfSaleViewModel;
            DataContext = _viewModel;
            this.Loaded += PointOfSale_Loaded;
        }

        private void PointOfSale_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.ReloadItemsFromDatabase();
        }

        private void Quote_Click(object sender, RoutedEventArgs e)
        {
            var cartItems = _viewModel.GetCartItemsForExport();
            string customerName = _viewModel.CustomerName ?? "";
            string selectedStaff = _viewModel.SelectedStaff?.Name ?? "Not Selected";
            string vatAmount = _viewModel.VATAmount.ToString("F2");
            string paymentMethod = _viewModel.SelectedPaymentMethod ?? "";
            string customerAddress = _viewModel.Address ?? "";
            decimal overallDiscountPercent = _viewModel.DiscountPercent;

            var quotePage = new Qoute(cartItems, customerName, selectedStaff, vatAmount, paymentMethod, customerAddress, overallDiscountPercent);
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(quotePage);
        }

        private void Invoice_Click(object sender, RoutedEventArgs e)
        {
            var cartItems = _viewModel.GetCartItemsForExport();
            string customerName = _viewModel.CustomerName ?? "";
            string selectedStaff = _viewModel.SelectedStaff?.Name ?? "Not Selected";
            string vatAmount = _viewModel.VATAmount.ToString("F2");
            string paymentMethod = _viewModel.SelectedPaymentMethod ?? "";
            string customerAddress = _viewModel.Address ?? "";
            decimal overallDiscountPercent = _viewModel.DiscountPercent;

            var invoicePage = new Invoice(cartItems, customerName, selectedStaff, vatAmount, paymentMethod, customerAddress, overallDiscountPercent);
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(invoicePage);
        }

        // Autocomplete event handlers
        private void ProductSearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Show suggestions when search box gets focus if there's text
            if (!string.IsNullOrWhiteSpace(ProductSearchTextBox.Text))
            {
                _viewModel.IsAutoCompleteOpen = true;
            }
        }

        private void ProductSearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && _viewModel.IsAutoCompleteOpen && _viewModel.FilteredProducts.Count > 0)
            {
                // Move focus to autocomplete list
                AutoCompleteListBox.Focus();
                AutoCompleteListBox.SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // Close autocomplete
                _viewModel.IsAutoCompleteOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && _viewModel.FilteredProducts.Count > 0)
            {
                // Select first item on Enter
                var firstItem = _viewModel.FilteredProducts[0];
                _viewModel.AddProductToCart(firstItem);
                _viewModel.ProductSearchText = "";
                _viewModel.IsAutoCompleteOpen = false;
                e.Handled = true;
            }
        }

        private void AutoCompleteListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AutoCompleteListBox.SelectedItem != null)
            {
                var selectedProduct = AutoCompleteListBox.SelectedItem as ADIX.Models.POSItem;
                if (selectedProduct != null)
                {
                    _viewModel.AddProductToCart(selectedProduct);
                    _viewModel.ProductSearchText = "";
                    _viewModel.IsAutoCompleteOpen = false;

                    // Return focus to search box for next item
                    ProductSearchTextBox.Focus();
                }
            }
        }

        private void AutoCompleteListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && AutoCompleteListBox.SelectedItem != null)
            {
                var selectedProduct = AutoCompleteListBox.SelectedItem as ADIX.Models.POSItem;
                if (selectedProduct != null)
                {
                    _viewModel.AddProductToCart(selectedProduct);
                    _viewModel.ProductSearchText = "";
                    _viewModel.IsAutoCompleteOpen = false;
                    ProductSearchTextBox.Focus();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                _viewModel.IsAutoCompleteOpen = false;
                ProductSearchTextBox.Focus();
                e.Handled = true;
            }
        }

        // Quantity adjustment buttons
        private void IncreaseQuantity_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button?.Tag as ADIX.Models.POSItem;
            if (item != null)
            {
                if (item.Quantity < item.InStock)
                {
                    item.Quantity++;
                }
                else
                {
                    MessageBox.Show($"Cannot exceed available stock ({item.InStock})",
                        "Stock Limit", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void DecreaseQuantity_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button?.Tag as ADIX.Models.POSItem;
            if (item != null && item.Quantity > 0)
            {
                item.Quantity--;
            }
        }
    }
}