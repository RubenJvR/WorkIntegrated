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

            // Setup auto-refresh timer 
            _refreshTimer = new System.Windows.Threading.DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(120);
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


    }
}