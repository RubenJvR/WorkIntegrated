using ADIX.Models;
using ADIX.ViewModels;
using ADIX.Services;
using Microsoft.Win32;
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

        private void SaveQuote_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "PNG Image (*.png)|*.png|PDF Document (*.pdf)|*.pdf",
                    FileName = $"Quote_{((QouteViewModel)DataContext).InvoiceNumber}_{System.DateTime.Now:yyyyMMddHHmmss}",
                    DefaultExt = ".png"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string filePath = saveFileDialog.FileName;
                    string extension = System.IO.Path.GetExtension(filePath).ToLower();

                    if (extension == ".png")
                    {
                        // Get the main content grid for saving
                        var mainContent = MainContentGrid;
                        PrintService.SaveAsPng(mainContent, filePath);
                    }
                    else if (extension == ".pdf")
                    {
                        MessageBox.Show("PDF export is currently not available. Please save as PNG for now.",
                            "Export Format", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error saving quote: {ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintQuote_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var viewModel = (QouteViewModel)DataContext;
                string description = $"Quote - {viewModel.InvoiceNumber} - {viewModel.BillTo}";

                // Get the main content grid for printing
                var mainContent = MainContentGrid;
                PrintService.PrintVisual(mainContent, description);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error printing quote: {ex.Message}", "Print Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}