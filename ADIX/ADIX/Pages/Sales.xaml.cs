using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PdfDocument = iTextSharp.text.Document;
using PdfParagraph = iTextSharp.text.Paragraph;

namespace ADIX
{
    public partial class Sales : Page
    {
        // Observable collection for binding to DataGrid
        private ObservableCollection<SaleTransaction> allTransactions = new();
        private ObservableCollection<SaleTransaction> filteredTransactions = new();

        public Sales()
        {
            InitializeComponent();
            InitializeData();
            ApplyFilters();
        }

        private void InitializeData()
        {
            // Initialize with sample data - replace with database calls
            allTransactions = new ObservableCollection<SaleTransaction>
            {
                new SaleTransaction
                {
                    InvoiceNumber = "120718",
                    Date = DateTime.Now.AddDays(-5),
                    SalesStuff = "Oliver",
                    CustomerName = "Richard",
                    PurchaseAmount = 4698.00m,
                    PaymentMethod = "Credit",
                    Paid = "Yes"
                },
                new SaleTransaction
                {
                    InvoiceNumber = "INV-002",
                    Date = DateTime.Now.AddDays(-3),
                    SalesStuff = "Tristan",
                    CustomerName = "Phil",
                    PurchaseAmount = 225.00m,
                    PaymentMethod = "EFT",
                    Paid = "Yes"
                },
                new SaleTransaction
                {
                    InvoiceNumber = "INV-003",
                    Date = DateTime.Now.AddDays(-1),
                    SalesStuff = "Ivan",
                    CustomerName = "Hylton",
                    PurchaseAmount = 244.00m,
                    PaymentMethod = "EFT",
                    Paid = "No"
                },

                 new SaleTransaction
                {
                    InvoiceNumber = "INV-003",
                    Date = DateTime.Now.AddDays(-1),
                    SalesStuff = "Mom",
                    CustomerName = "Carol White",
                    PurchaseAmount = 2100.75m,
                    PaymentMethod = "Credit",
                    Paid = "No"
                },

                  new SaleTransaction
                {
                    InvoiceNumber = "INV-003",
                    Date = DateTime.Now.AddDays(-1),
                    SalesStuff = "April",
                    CustomerName = "Keagan Smit",
                    PurchaseAmount = 1250.00m,
                    PaymentMethod = "Cash",
                    Paid = "Yes"
                }
            };

            filteredTransactions = new ObservableCollection<SaleTransaction>(allTransactions);
            SalesGrid.ItemsSource = filteredTransactions;
        }

        // Date range selection changed
        private void SalesDate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SalesDate.SelectedItem == null) return;

            var selectedItem = (ComboBoxItem)SalesDate.SelectedItem;
            string selection = selectedItem.Content.ToString() ?? string.Empty;

            if (selection == "Custom")
            {
                CustomDatePanel.Visibility = Visibility.Visible;
                StartDatePicker.SelectedDateChanged += CustomDate_Changed;
                EndDatePicker.SelectedDateChanged += CustomDate_Changed;
            }
            else
            {
                CustomDatePanel.Visibility = Visibility.Collapsed;
                ApplyFilters();
            }
        }

        private void CustomDate_Changed(object? sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        // Apply all filters
        private void ApplyFilters()
        {
            if (allTransactions == null) return;

            var filtered = allTransactions.AsEnumerable();

            // Date filter
            filtered = ApplyDateFilter(filtered);

            // Search filter
            if (!string.IsNullOrWhiteSpace(TransactionSearch.Text))
            {
                string searchText = TransactionSearch.Text.ToLower();
                filtered = filtered.Where(t =>
                    t.InvoiceNumber.ToLower().Contains(searchText) ||
                    t.CustomerName.ToLower().Contains(searchText) ||
                    t.SalesStuff.ToLower().Contains(searchText));
            }

            // Payment method filter
            if (PaymentMethod.SelectedIndex >= 0)
            {
                var paymentItem = (ComboBoxItem)PaymentMethod.SelectedItem;
                string payment = paymentItem.Content.ToString() ?? string.Empty;
                filtered = filtered.Where(t => t.PaymentMethod == payment);
            }

            filteredTransactions.Clear();
            foreach (var transaction in filtered)
            {
                filteredTransactions.Add(transaction);
            }
        }

        private IEnumerable<SaleTransaction> ApplyDateFilter(IEnumerable<SaleTransaction> transactions)
        {
            if (SalesDate.SelectedIndex < 0) return transactions;

            var selectedItem = (ComboBoxItem)SalesDate.SelectedItem;
            string selection = selectedItem.Content.ToString() ?? string.Empty;

            DateTime startDate;
            DateTime endDate = DateTime.Now.Date.AddDays(1).AddSeconds(-1);

            switch (selection)
            {
                case "This Month":
                    startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    break;
                case "Last 30 Days":
                    startDate = DateTime.Now.Date.AddDays(-30);
                    break;
                case "Custom":
                    if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue)
                    {
                        startDate = StartDatePicker.SelectedDate.Value.Date;
                        endDate = EndDatePicker.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1);
                    }
                    else
                    {
                        return transactions;
                    }
                    break;
                default:
                    return transactions;
            }

            return transactions.Where(t => t.Date >= startDate && t.Date <= endDate);
        }

        // Generate Table button click
        private void GenerateTable_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
            MessageBox.Show($"Table generated with {filteredTransactions.Count} transactions.",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Refund button click
        private void Refund_Click(object sender, RoutedEventArgs e)
        {
            if (SalesGrid.SelectedItem == null)
            {
                MessageBox.Show("Please select a transaction to refund.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var transaction = (SaleTransaction)SalesGrid.SelectedItem;
            var result = MessageBox.Show($"Are you sure you want to refund transaction {transaction.InvoiceNumber}?",
                "Confirm Refund", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Process refund logic here
                transaction.PaymentMethod = "Return";
                transaction.Paid = "Refunded";
                SalesGrid.Items.Refresh();
                MessageBox.Show("Refund processed successfully.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Export as PDF button click
        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = $"Sales_Report_{DateTime.Now:yyyyMMdd}.pdf"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    GeneratePdfReport(saveDialog.FileName);
                    MessageBox.Show("PDF exported successfully!",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting PDF: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GeneratePdfReport(string filePath)
        {
            PdfDocument document = new PdfDocument(PageSize.A4.Rotate());
            PdfWriter.GetInstance(document, new FileStream(filePath, FileMode.Create));
            document.Open();

            // Title
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
            var title = new PdfParagraph("Sales Report\n\n", titleFont);
            title.Alignment = Element.ALIGN_CENTER;
            document.Add(title);

            // Date info
            var dateFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
            document.Add(new PdfParagraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm}\n\n", dateFont));

            // Table
            PdfPTable table = new PdfPTable(7);
            table.WidthPercentage = 100;

            // Headers
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            string[] headers = { "Invoice", "Date", "Sales Staff", "Customer", "Amount", "Payment", "Paid" };

            foreach (var header in headers)
            {
                var cell = new PdfPCell(new Phrase(header, headerFont));
                cell.BackgroundColor = new BaseColor(45, 45, 45);
                cell.HorizontalAlignment = Element.ALIGN_CENTER;
                table.AddCell(cell);
            }

            // Data
            var dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
            decimal totalAmount = 0;

            foreach (var transaction in filteredTransactions)
            {
                table.AddCell(new Phrase(transaction.InvoiceNumber, dataFont));
                table.AddCell(new Phrase(transaction.Date.ToString("yyyy-MM-dd"), dataFont));
                table.AddCell(new Phrase(transaction.SalesStuff, dataFont));
                table.AddCell(new Phrase(transaction.CustomerName, dataFont));
                table.AddCell(new Phrase($"R {transaction.PurchaseAmount:N2}", dataFont));
                table.AddCell(new Phrase(transaction.PaymentMethod, dataFont));
                table.AddCell(new Phrase(transaction.Paid, dataFont));

                totalAmount += transaction.PurchaseAmount;
            }

            document.Add(table);

            // Summary
            document.Add(new PdfParagraph($"\nTotal Transactions: {filteredTransactions.Count}", headerFont));
            document.Add(new PdfParagraph($"Total Amount: R {totalAmount:N2}", headerFont));

            document.Close();
        }

        // Wire up search text changed
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            if (TransactionSearch != null)
                TransactionSearch.TextChanged += (s, args) => ApplyFilters();

            if (PaymentMethod != null)
                PaymentMethod.SelectionChanged += (s, args) => ApplyFilters();
        }

        // Handle ComboBox border click to open dropdown
        private void ComboBoxBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border != null)
            {
                var comboBox = FindParent<ComboBox>(border);
                if (comboBox != null)
                {
                    comboBox.IsDropDownOpen = !comboBox.IsDropDownOpen;
                    e.Handled = true;
                }
            }
        }

        // Helper method to find parent control
        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindParent<T>(parentObject);
        }
    }

    // Sale Transaction Model
    public class SaleTransaction
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string SalesStuff { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal PurchaseAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Paid { get; set; } = string.Empty;
    }
}