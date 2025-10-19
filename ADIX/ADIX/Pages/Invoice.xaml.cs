using ADIX.Models;
using ADIX.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace ADIX
{
    public partial class Invoice : Page
    {
        // Updated constructor to accept overall discount
        public Invoice(System.Collections.Generic.List<POSItem> cartItems, string customerName, string selectedStaff, string vatAmount, string paymentMethod, string customerAddress, decimal overallDiscountPercent)
        {
            InitializeComponent();

            var viewModel = new InvoiceViewModel(cartItems, customerName, selectedStaff, vatAmount, paymentMethod, customerAddress, overallDiscountPercent);
            DataContext = viewModel;

            Loaded += OnInvoiceLoaded;
        }

        // Overloaded constructor to accept data from PointOfSale (updated with discount)
        public Invoice(string customerName, string selectedStaff, string vatAmount, string paymentMethod, string customerAddress, decimal overallDiscountPercent)
        {
            InitializeComponent();

            var viewModel = new InvoiceViewModel
            {
                BillTo = customerName,
                StaffID = selectedStaff,
                VATNumber = vatAmount,
                Payment = paymentMethod,
                CustomerAddress = customerAddress,
                OverallDiscountPercent = overallDiscountPercent,
                // Set other default values
                InvoiceDate = System.DateTime.Now.ToString("yyyy-MM-dd"),
                InvoiceNumber = "INV-" + System.DateTime.Now.ToString("yyyyMMddHHmmss"),
                // These will be calculated automatically when items are added
                SubTotal = 0,
                TotalItemDiscounts = 0,
                OverallDiscountAmount = 0,
                TotalDiscount = 0,
                GrandTotal = 0
            };

            DataContext = viewModel;

            Loaded += OnInvoiceLoaded;
        }

        // Parameterless constructor for design time or default usage
        public Invoice()
        {
            InitializeComponent();

            var viewModel = new InvoiceViewModel();
            DataContext = viewModel;

            Loaded += OnInvoiceLoaded;
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

        private void OnInvoiceLoaded(object sender, RoutedEventArgs e)
        {
            // Hide sidebar when this page is loaded
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                var sidebar = mainWindow.FindName("Sidebar") as Sidebar;
                if (sidebar != null)
                {
                    sidebar.Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}