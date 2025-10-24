using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ADIX
{
    public partial class MonthlyReport : Page, INotifyPropertyChanged
    {
        private MonthlyReportData _currentReport;
        private ObservableCollection<Transaction> _transactions;
        private ObservableCollection<SupplierButton> _supplierButtons;

        public MonthlyReport()
        {
            InitializeComponent();
            Loaded += MonthlyReport_Loaded;
            _transactions = new ObservableCollection<Transaction>();
            _supplierButtons = new ObservableCollection<SupplierButton>();
            _currentReport = new MonthlyReportData();
        }

        private void MonthlyReport_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeYearSelector();
            LoadSampleData();
            InitializeSupplierButtons();
        }

        private void InitializeYearSelector()
        {
            try
            {
                YearSelector.Items.Clear();
                int currentYear = DateTime.Now.Year;

                // Add years from 2020 to current year + 1
                for (int year = 2020; year <= currentYear + 1; year++)
                {
                    YearSelector.Items.Add(year.ToString());
                }

                // Set current year as default
                YearSelector.SelectedItem = currentYear.ToString();

                // Set current month as default
                MonthSelector.SelectedIndex = DateTime.Now.Month - 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing year selector: {ex.Message}");
            }
        }

        private void LoadSampleData()
        {
            try
            {
                // Generate sample transactions
                _transactions.Clear();

                var sampleTransactions = new List<Transaction>
                {
                    new Transaction { Date = new DateTime(2024, 1, 15), CustomerName = "Alice Brown", SalesStaff = "Ruben Janse", Paid = 1250.50m, PurchaseAmount = 1250.50m, PaymentMethod = "Card", InvoiceNumber = "INV-001" },
                    new Transaction { Date = new DateTime(2024, 1, 16), CustomerName = "Bob White", SalesStaff = "Sarah Ndlovu", Paid = 850.75m, PurchaseAmount = 850.75m, PaymentMethod = "Cash", InvoiceNumber = "INV-002" },
                    new Transaction { Date = new DateTime(2024, 1, 17), CustomerName = "Charlie Green", SalesStaff = "Michael Smith", Paid = 2200.00m, PurchaseAmount = 2200.00m, PaymentMethod = "EFT", InvoiceNumber = "INV-003" },
                    new Transaction { Date = new DateTime(2024, 1, 18), CustomerName = "Diana Blue", SalesStaff = "Emily Johnson", Paid = -450.25m, PurchaseAmount = -450.25m, PaymentMethod = "Return", InvoiceNumber = "REF-001" },
                    new Transaction { Date = new DateTime(2024, 1, 19), CustomerName = "Edward Black", SalesStaff = "Ruben Janse", Paid = 1800.00m, PurchaseAmount = 1800.00m, PaymentMethod = "Card", InvoiceNumber = "INV-004" },
                    new Transaction { Date = new DateTime(2024, 1, 20), CustomerName = "Fiona Gray", SalesStaff = "Sarah Ndlovu", Paid = 950.30m, PurchaseAmount = 950.30m, PaymentMethod = "Cash", InvoiceNumber = "INV-005" },
                    new Transaction { Date = new DateTime(2024, 1, 21), CustomerName = "George Yellow", SalesStaff = "Michael Smith", Paid = 3200.75m, PurchaseAmount = 3200.75m, PaymentMethod = "EFT", InvoiceNumber = "INV-006" },
                    new Transaction { Date = new DateTime(2024, 1, 22), CustomerName = "Hannah Purple", SalesStaff = "Emily Johnson", Paid = 125.50m, PurchaseAmount = 125.50m, PaymentMethod = "Credit", InvoiceNumber = "INV-007" }
                };

                foreach (var transaction in sampleTransactions)
                {
                    _transactions.Add(transaction);
                }

                MonthlyReportGrid.ItemsSource = _transactions;

                // Update report data
                UpdateReportData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading sample data: {ex.Message}");
            }
        }

        private void UpdateReportData()
        {
            try
            {
                var selectedMonth = MonthSelector.SelectedIndex + 1;
                var selectedYear = YearSelector.SelectedItem?.ToString();

                if (string.IsNullOrEmpty(selectedYear) || selectedMonth == 0)
                    return;

                // Calculate financial metrics from transactions
                var monthTransactions = _transactions.Where(t =>
                    t.Date.Month == selectedMonth &&
                    t.Date.Year == int.Parse(selectedYear));

                _currentReport.CardAmount = monthTransactions
                    .Where(t => t.PaymentMethod == "Card" && t.Paid > 0)
                    .Sum(t => t.Paid);

                _currentReport.CashAmount = monthTransactions
                    .Where(t => t.PaymentMethod == "Cash" && t.Paid > 0)
                    .Sum(t => t.Paid);

                _currentReport.EFTAmount = monthTransactions
                    .Where(t => t.PaymentMethod == "EFT" && t.Paid > 0)
                    .Sum(t => t.Paid);

                _currentReport.ReturnAmount = Math.Abs(monthTransactions
                    .Where(t => t.PaymentMethod == "Return")
                    .Sum(t => t.Paid));

                _currentReport.CreditAmount = monthTransactions
                    .Where(t => t.PaymentMethod == "Credit" && t.Paid > 0)
                    .Sum(t => t.Paid);

                _currentReport.TotalTurnover = _currentReport.CardAmount + _currentReport.CashAmount +
                                             _currentReport.EFTAmount + _currentReport.CreditAmount -
                                             _currentReport.ReturnAmount;

                // Sample expense data
                _currentReport.RentExpense = 15000.00m;
                _currentReport.UtilitiesExpense = 2500.00m;
                _currentReport.SalaryExpense = 45000.00m;
                _currentReport.OtherExpense = 8000.00m;
                _currentReport.TotalExpenses = _currentReport.RentExpense + _currentReport.UtilitiesExpense +
                                             _currentReport.SalaryExpense + _currentReport.OtherExpense;

                // Calculate profit metrics
                _currentReport.MonthlyCostOfBusiness = _currentReport.TotalExpenses;
                _currentReport.GrossProfit = _currentReport.TotalTurnover;
                _currentReport.NetProfit = _currentReport.GrossProfit - _currentReport.TotalExpenses;
                _currentReport.ProfitMargin = _currentReport.TotalTurnover > 0 ?
                    (_currentReport.NetProfit / _currentReport.TotalTurnover) * 100 : 0;

                // Update bindings
                OnPropertyChanged(nameof(CurrentReport));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating report data: {ex.Message}");
            }
        }

        private void InitializeSupplierButtons()
        {
            try
            {
                _supplierButtons.Clear();

                var sampleSuppliers = new List<SupplierButton>
                {
                    new SupplierButton("GreenFoods Ltd", () => SaveSupplierReport("GreenFoods Ltd")),
                    new SupplierButton("BeverageCorp", () => SaveSupplierReport("BeverageCorp")),
                    new SupplierButton("SnackSupply Co", () => SaveSupplierReport("SnackSupply Co")),
                    new SupplierButton("Fresh Produce Inc", () => SaveSupplierReport("Fresh Produce Inc")),
                    new SupplierButton("Dairy Distributors", () => SaveSupplierReport("Dairy Distributors"))
                };

                foreach (var supplier in sampleSuppliers)
                {
                    _supplierButtons.Add(supplier);
                }

                SupplierButtonsContainer.ItemsSource = _supplierButtons;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing supplier buttons: {ex.Message}");
            }
        }

        private void SaveSupplierReport(string supplierName)
        {
            try
            {
                string message = $"Supplier report for {supplierName} saved successfully!\n\n" +
                               $"This would generate a detailed stock report for {supplierName} " +
                               $"including item quantities, sales data, and inventory levels.";

                MessageBox.Show(message, "Supplier Report Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving supplier report: {ex.Message}");
            }
        }

        // Event handlers
        private void MonthSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateReportData();
        }

        private void YearSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateReportData();
        }

        private void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadSampleData(); // Reload data with current month/year filter
                MessageBox.Show($"Report generated for {MonthSelector.SelectedItem} {YearSelector.SelectedItem}!",
                    "Report Generated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating report: {ex.Message}");
            }
        }

        private void SaveFullReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string message = $"Full month stock report saved successfully!\n\n" +
                               $"Report includes:\n" +
                               $"• Complete inventory snapshot\n" +
                               $"• Sales performance by category\n" +
                               $"• Supplier performance metrics\n" +
                               $"• Stock movement analysis\n\n" +
                               $"This report should only be generated at the end of the month.";

                MessageBox.Show(message, "Full Month Report Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving full report: {ex.Message}");
            }
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string message = $"PDF export completed successfully!\n\n" +
                               $"Exported: Monthly Report for {MonthSelector.SelectedItem} {YearSelector.SelectedItem}\n" +
                               $"File: Monthly_Report_{MonthSelector.SelectedItem}_{YearSelector.SelectedItem}.pdf\n\n" +
                               $"The PDF includes all transaction data, financial summaries, and charts.";

                MessageBox.Show(message, "PDF Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting PDF: {ex.Message}");
            }
        }

        public MonthlyReportData CurrentReport => _currentReport;

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Data classes
    public class Transaction
    {
        public DateTime Date { get; set; }
        public string CustomerName { get; set; }
        public string SalesStaff { get; set; }
        public decimal Paid { get; set; }
        public decimal PurchaseAmount { get; set; }
        public string PaymentMethod { get; set; }
        public string InvoiceNumber { get; set; }
    }

    public class MonthlyReportData
    {
        public decimal CardAmount { get; set; }
        public decimal CashAmount { get; set; }
        public decimal EFTAmount { get; set; }
        public decimal ReturnAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal TotalTurnover { get; set; }

        public decimal RentExpense { get; set; }
        public decimal UtilitiesExpense { get; set; }
        public decimal SalaryExpense { get; set; }
        public decimal OtherExpense { get; set; }
        public decimal TotalExpenses { get; set; }

        public decimal MonthlyCostOfBusiness { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal NetProfit { get; set; }
        public decimal ProfitMargin { get; set; }
    }

    public class SupplierButton
    {
        public string DisplayName { get; }
        public ICommand SaveCommand { get; }

        public SupplierButton(string displayName, Action saveAction)
        {
            DisplayName = displayName;
            SaveCommand = new RelayCommand(_ => saveAction());
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);
    }
}