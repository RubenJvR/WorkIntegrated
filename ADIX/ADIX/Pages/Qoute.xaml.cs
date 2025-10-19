using ADIX.Models;
using ADIX.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace ADIX
{
    public partial class Qoute : Page
    {
        // Updated constructor to accept overall discount
        public Qoute(System.Collections.Generic.List<POSItem> cartItems, string customerName, string selectedStaff, string vatAmount, string paymentMethod, string customerAddress, decimal overallDiscountPercent)
        {
            InitializeComponent();

            var viewModel = new QouteViewModel(cartItems, customerName, selectedStaff, vatAmount, paymentMethod, customerAddress, overallDiscountPercent);
            DataContext = viewModel;
        }

        // Overloaded constructor to accept data from PointOfSale (updated with discount)
        public Qoute(string customerName, string selectedStaff, string vatAmount, string paymentMethod, string customerAddress, decimal overallDiscountPercent)
        {
            InitializeComponent();

            var viewModel = new QouteViewModel
            {
                BillTo = customerName,
                StaffID = selectedStaff,
                VATNumber = vatAmount,
                Payment = paymentMethod,
                CustomerAddress = customerAddress,
                OverallDiscountPercent = overallDiscountPercent,
                // Set other default values
                InvoiceDate = System.DateTime.Now.ToString("yyyy-MM-dd"),
                InvoiceNumber = "Q-" + System.DateTime.Now.ToString("yyyyMMddHHmmss"),
                // These will be calculated automatically when items are added
                SubTotal = 0,
                TotalItemDiscounts = 0,
                OverallDiscountAmount = 0,
                TotalDiscount = 0,
                GrandTotal = 0
            };

            DataContext = viewModel;
        }

        // Parameterless constructor for design time or default usage
        public Qoute()
        {
            InitializeComponent();

            var viewModel = new QouteViewModel();
            DataContext = viewModel;
        }

        private void BackToPOS_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;

            // Check if we already have a PointOfSale instance in navigation history
            if (mainWindow?.MainFrame.Content is PointOfSale existingPOS)
            {
                // Navigate back to the existing instance
                mainWindow.MainFrame.GoBack();
            }
            else
            {
                // Create new instance if none exists
                mainWindow?.MainFrame.Navigate(new PointOfSale());
            }
        }
    }
}