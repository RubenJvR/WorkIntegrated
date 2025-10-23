using ADIX.ViewModels;
using System.Windows;
using System.Windows.Controls;

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