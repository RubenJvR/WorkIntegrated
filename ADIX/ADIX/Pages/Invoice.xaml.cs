using ADIX.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace ADIX
{
    public partial class Invoice : Page
    {
        public Invoice()
        {
            InitializeComponent();
            DataContext = new InvoiceViewModel();
            Loaded += OnInvoiceLoaded;
        }

        // Overloaded constructor to accept data from PointOfSale
        public Invoice(string customerName, string selectedStaff, string vatAmount, string paymentMethod, string customerAddress)
        {
            InitializeComponent();

            var viewModel = new InvoiceViewModel(customerName, selectedStaff, vatAmount, paymentMethod, customerAddress);
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