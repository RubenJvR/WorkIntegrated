using ADIX.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace ADIX
{
    public partial class PointOfSale : Page
    {
        private PointOfSaleViewModel _viewModel;
        private static PointOfSaleViewModel? _storedViewModel;

        public PointOfSale()
        {
            InitializeComponent();

            // Use stored ViewModel if available, otherwise create new one
            if (_storedViewModel != null)
            {
                _viewModel = _storedViewModel;
                _storedViewModel = null; // Clear after use
            }
            else
            {
                _viewModel = new PointOfSaleViewModel();
            }

            DataContext = _viewModel;
        }

        private void Quote_Click(object sender, RoutedEventArgs e)
        {
            // Store the current ViewModel before navigating away
            _storedViewModel = _viewModel;

            // Get the current ViewModel data
            string customerName = _viewModel.CustomerName ?? "";
            string selectedStaff = _viewModel.SelectedStaff?.Name ?? "Not Selected";
            string vatAmount = _viewModel.VATAmount.ToString("F2");
            string paymentMethod = _viewModel.SelectedPaymentMethod ?? "";
            string customerAddress = _viewModel.Address ?? "";

            var qoutePage = new Qoute(customerName, selectedStaff, vatAmount, paymentMethod, customerAddress);

            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(qoutePage);
        }

        private void Invoice_Click(object sender, RoutedEventArgs e)
        {
            // Store the current ViewModel before navigating away
            _storedViewModel = _viewModel;

            // Get the current ViewModel data
            string customerName = _viewModel.CustomerName ?? "";
            string selectedStaff = _viewModel.SelectedStaff?.Name ?? "Not Selected";
            string vatAmount = _viewModel.VATAmount.ToString("F2");
            string paymentMethod = _viewModel.SelectedPaymentMethod ?? "";
            string customerAddress = _viewModel.Address ?? "";

            var invoicePage = new Invoice(customerName, selectedStaff, vatAmount, paymentMethod, customerAddress);

            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(invoicePage);
        }
    }
}