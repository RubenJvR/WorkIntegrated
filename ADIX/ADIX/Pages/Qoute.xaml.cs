using ADIX.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace ADIX
{
    public partial class Qoute : Page
    {
        public Qoute()
        {
            InitializeComponent();
            // Default constructor with empty data
            DataContext = new QouteViewModel();
        }

        // Overloaded constructor to accept data from PointOfSale
        public Qoute(string customerName, string selectedStaff, string vatAmount, string paymentMethod, string customerAddress)
        {
            InitializeComponent();

            var viewModel = new QouteViewModel
            {
                BillTo = customerName,
                StaffID = selectedStaff,
                VATNumber = vatAmount,
                Payment = paymentMethod,
                CustomerAddress = customerAddress,
                // Set other default values
                InvoiceDate = System.DateTime.Now.ToString("yyyy-MM-dd"),
                InvoiceNumber = "Q-" + System.DateTime.Now.ToString("yyyyMMddHHmmss"),
                // You can calculate these based on your business logic
                SubTotal = 0, // Set based on your items
                TotalDiscount = 0, // Set based on your discount logic
                GrandTotal = 0 // Set based on your total calculation
            };

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