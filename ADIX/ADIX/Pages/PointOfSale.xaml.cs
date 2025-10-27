using ADIX.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace ADIX
{
    public partial class PointOfSale : Page
    {
        private PointOfSaleViewModel _viewModel;

        private System.Windows.Threading.DispatcherTimer _refreshTimer;

        public PointOfSale()
        {
            InitializeComponent();

            _viewModel = ViewModelManager.PointOfSaleViewModel;
            DataContext = _viewModel;

            this.Loaded += PointOfSale_Loaded;
            this.Unloaded += PointOfSale_Unloaded;

            // Setup auto-refresh timer (every 30 seconds)
            _refreshTimer = new System.Windows.Threading.DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(30);
            _refreshTimer.Tick += RefreshTimer_Tick;
        }


        private async void PointOfSale_Loaded(object sender, RoutedEventArgs e)
        {
            // Always check for sync when page loads
            if (Database.IsInternetAvailable() && Database.IsSyncRequired())
            {
                try
                {
                    await Database.CheckAndSyncAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Background sync on load failed: {ex.Message}");
                }
            }

            _viewModel.ReloadItemsFromDatabase();
            _refreshTimer.Start(); // Start auto-refresh when page loads
        }

        private void PointOfSale_Unloaded(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Stop(); // Stop timer when page unloads
        }

        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            // Check for sync and refresh items
            if (Database.IsInternetAvailable())
            {
                try
                {
                    await Database.CheckAndSyncAsync();
                    _viewModel.ReloadItemsFromDatabase();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Auto-refresh sync failed: {ex.Message}");
                }
            }
        }
       
        private void Quote_Click(object sender, RoutedEventArgs e)
        {
            // Get cart items with quantity > 0
            var cartItems = _viewModel.GetCartItemsForExport();

            // Get the current ViewModel data including overall discount
            string customerName = _viewModel.CustomerName ?? "";
            string selectedStaff = _viewModel.SelectedStaff?.Name ?? "Not Selected";
            string vatAmount = _viewModel.VATAmount.ToString("F2");
            string paymentMethod = _viewModel.SelectedPaymentMethod ?? "";
            string customerAddress = _viewModel.Address ?? "";
            decimal overallDiscountPercent = _viewModel.DiscountPercent;

            // Pass all data including overall discount to Quote page
            var quotePage = new Qoute(cartItems, customerName, selectedStaff, vatAmount, paymentMethod, customerAddress, overallDiscountPercent);
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(quotePage);
        }

        private async void ManualSync_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
                button.Content = "Syncing...";
            }

            try
            {
                if (Database.IsInternetAvailable())
                {
                    await Database.CheckAndSyncAsync();
                    _viewModel.ReloadItemsFromDatabase();
                    MessageBox.Show("Sync completed!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("No internet connection", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sync failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "Sync Now";
                }
            }
        }

        private void Invoice_Click(object sender, RoutedEventArgs e)
        {
            // Get cart items with quantity > 0
            var cartItems = _viewModel.GetCartItemsForExport();

            // Get the current ViewModel data including overall discount
            string customerName = _viewModel.CustomerName ?? "";
            string selectedStaff = _viewModel.SelectedStaff?.Name ?? "Not Selected";
            string vatAmount = _viewModel.VATAmount.ToString("F2");
            string paymentMethod = _viewModel.SelectedPaymentMethod ?? "";
            string customerAddress = _viewModel.Address ?? "";
            decimal overallDiscountPercent = _viewModel.DiscountPercent;

            // Pass all data including overall discount to Invoice page
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
    }
}

       
 