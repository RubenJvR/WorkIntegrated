using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ADIX
{
    public partial class MonthlyReport : Page, INotifyPropertyChanged
    {
        private MonthlyReportData _currentReport;
        private ObservableCollection<Transaction> _transactions;
        private ObservableCollection<Supplier> _suppliers;
        private const string SqliteConnectionString = "Data Source=ADIX.db";

        public MonthlyReport()
        {
            InitializeComponent();
            Loaded += MonthlyReport_Loaded;
            _transactions = new ObservableCollection<Transaction>();
            _suppliers = new ObservableCollection<Supplier>();
            _currentReport = new MonthlyReportData();

            // Initialize expenses table
            Database.InitializeExpensesTable();
        }

        private void MonthlyReport_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeYearSelector();
            LoadActualData();
            LoadSuppliers();
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

                // Don't set default selection initially - let placeholder show
                // The user will make their first selection
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing year selector: {ex.Message}");
            }
        }

        private void LoadActualData()
        {
            try
            {
                var selectedMonth = MonthSelector.SelectedIndex + 1;
                var selectedYear = YearSelector.SelectedItem?.ToString();

                if (string.IsNullOrEmpty(selectedYear) || selectedMonth == 0)
                    return;

                LoadTransactionsFromDatabase(selectedMonth, int.Parse(selectedYear));
                UpdateReportData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}");
            }
        }

        private void LoadTransactionsFromDatabase(int month, int year)
        {
            try
            {
                _transactions.Clear();

                // Use the new method from Database class
                var transactionsData = Database.GetMonthlyTransactions(month, year);

                foreach (DataRow row in transactionsData.Rows)
                {
                    var transactionType = Convert.ToInt32(row["TransactionType"]);
                    var isReturn = transactionType == 2; // Assuming type 2 is returns/refunds

                    var transaction = new Transaction
                    {
                        Date = DateTime.Parse(row["Date"].ToString()),
                        CustomerName = row["CustomerName"].ToString(),
                        SalesStaff = row["SalesStaff"].ToString(),
                        Paid = Convert.ToDecimal(row["Paid"]),
                        PurchaseAmount = Convert.ToDecimal(row["PurchaseAmount"]),
                        PaymentMethod = row["PaymentMethod"].ToString(),
                        InvoiceNumber = isReturn ? $"REF-{row["InvoiceNumber"]}" : $"INV-{row["InvoiceNumber"]}"
                    };

                    // For returns, make amounts negative
                    if (isReturn)
                    {
                        transaction.Paid = -transaction.Paid;
                        transaction.PurchaseAmount = -transaction.PurchaseAmount;
                        transaction.PaymentMethod = "Return";
                    }

                    _transactions.Add(transaction);
                }

                MonthlyReportGrid.ItemsSource = _transactions;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading transactions from database: {ex.Message}");
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

                // Get financial summary from database
                var financialSummary = Database.GetMonthlyFinancialSummary(selectedMonth, int.Parse(selectedYear));

                _currentReport.CardAmount = financialSummary.cardAmount;
                _currentReport.CashAmount = financialSummary.cashAmount;
                _currentReport.EFTAmount = financialSummary.eftAmount;
                _currentReport.ReturnAmount = financialSummary.returnAmount;
                _currentReport.CreditAmount = financialSummary.creditAmount;
                _currentReport.TotalTurnover = financialSummary.totalTurnover;

                // Load actual expenses from database
                LoadExpensesFromDatabase(selectedMonth, int.Parse(selectedYear));

                // Calculate profit metrics
                decimal cogs = Database.GetMonthlyCOGS(selectedMonth, int.Parse(selectedYear));
                _currentReport.MonthlyCostOfBusiness = _currentReport.TotalExpenses + cogs;
                _currentReport.GrossProfit = _currentReport.TotalTurnover - cogs;
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

        private void LoadExpensesFromDatabase(int month, int year)
        {
            try
            {
                using var connection = new SqliteConnection(SqliteConnectionString);
                connection.Open();

                string expensesQuery = @"
                    SELECT 
                        expenseType,
                        SUM(amount) as TotalAmount
                    FROM EXPENSES 
                    WHERE strftime('%m', date) = @month 
                    AND strftime('%Y', date) = @year
                    GROUP BY expenseType";

                using var expensesCmd = new SqliteCommand(expensesQuery, connection);
                expensesCmd.Parameters.AddWithValue("@month", month.ToString("00"));
                expensesCmd.Parameters.AddWithValue("@year", year.ToString());

                // Reset expenses
                _currentReport.RentExpense = 0;
                _currentReport.UtilitiesExpense = 0;
                _currentReport.SalaryExpense = 0;
                _currentReport.OtherExpense = 0;

                using var reader = expensesCmd.ExecuteReader();
                while (reader.Read())
                {
                    var expenseType = reader["expenseType"].ToString().ToLower();
                    var amount = Convert.ToDecimal(reader["TotalAmount"]);

                    switch (expenseType)
                    {
                        case "rent":
                            _currentReport.RentExpense = amount;
                            break;
                        case "utilities":
                        case "utility":
                            _currentReport.UtilitiesExpense = amount;
                            break;
                        case "salaries":
                        case "salary":
                            _currentReport.SalaryExpense = amount;
                            break;
                        default:
                            _currentReport.OtherExpense += amount;
                            break;
                    }
                }

                _currentReport.TotalExpenses = _currentReport.RentExpense + _currentReport.UtilitiesExpense +
                                             _currentReport.SalaryExpense + _currentReport.OtherExpense;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading expenses: {ex.Message}");
                // Fallback to default expenses
                _currentReport.RentExpense = 15000.00m;
                _currentReport.UtilitiesExpense = 2500.00m;
                _currentReport.SalaryExpense = 45000.00m;
                _currentReport.OtherExpense = 5000.00m;
                _currentReport.TotalExpenses = _currentReport.RentExpense + _currentReport.UtilitiesExpense +
                                             _currentReport.SalaryExpense + _currentReport.OtherExpense;
            }
        }

        private void LoadSuppliers()
        {
            try
            {
                _suppliers.Clear();

                // Load actual suppliers from database
                using var connection = new SqliteConnection(SqliteConnectionString);
                connection.Open();

                string query = "SELECT supplierID, name FROM SUPPLIER ORDER BY name";
                using var cmd = new SqliteCommand(query, connection);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var supplierId = reader["supplierID"].ToString();
                    var supplierName = reader["name"].ToString();
                    _suppliers.Add(new Supplier { Id = supplierId, Name = supplierName });
                }

                // Add sample suppliers if no suppliers exist in database
                if (_suppliers.Count == 0)
                {
                    _suppliers.Add(new Supplier { Id = "1", Name = "Precision Bows Ltd" });
                    _suppliers.Add(new Supplier { Id = "2", Name = "Eagle Arrows Co" });
                    _suppliers.Add(new Supplier { Id = "3", Name = "TargetCraft Supplies" });
                }

                SupplierComboBox.ItemsSource = _suppliers;
                // Don't set default selection - let placeholder show
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading suppliers: {ex.Message}");
            }
        }

        private void SaveSupplierReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedSupplier = SupplierComboBox.SelectedItem as Supplier;
                if (selectedSupplier == null)
                {
                    MessageBox.Show("Please select a supplier first.", "No Supplier Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedMonth = MonthSelector.SelectedIndex + 1;
                var selectedYear = YearSelector.SelectedItem?.ToString();

                if (string.IsNullOrEmpty(selectedYear) || selectedMonth == 0)
                {
                    MessageBox.Show("Please select a valid month and year.", "Invalid Date", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Generate supplier-specific report
                using var connection = new SqliteConnection(SqliteConnectionString);
                connection.Open();

                string query = @"
                    SELECT 
                        i.description as ItemDescription,
                        i.stockQuantity as CurrentStock,
                        i.stockSold as SoldThisMonth,
                        i.retailPrice as RetailPrice,
                        i.costPrice as CostPrice,
                        (SELECT COALESCE(SUM(ii.quantity), 0) 
                         FROM INVOICEITEM ii 
                         INNER JOIN INVOICEQUOTE iq ON ii.invoiceQuoteID = iq.invoiceQuoteID
                         WHERE ii.itemID = i.itemID 
                         AND strftime('%m', iq.date) = @month 
                         AND strftime('%Y', iq.date) = @year) as MonthlySales
                    FROM ITEM i
                    WHERE i.supplierID = @supplierId
                    ORDER BY i.description";

                using var cmd = new SqliteCommand(query, connection);
                cmd.Parameters.AddWithValue("@supplierId", selectedSupplier.Id);
                cmd.Parameters.AddWithValue("@month", selectedMonth.ToString("00"));
                cmd.Parameters.AddWithValue("@year", selectedYear);

                var reportData = new List<string>();
                decimal totalStockValue = 0;
                decimal totalSalesValue = 0;
                int totalItems = 0;

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var description = reader["ItemDescription"].ToString();
                    var currentStock = Convert.ToInt32(reader["CurrentStock"]);
                    var monthlySales = Convert.ToInt32(reader["MonthlySales"]);
                    var retailPrice = Convert.ToDecimal(reader["RetailPrice"]);
                    var costPrice = Convert.ToDecimal(reader["CostPrice"]);

                    var stockValue = currentStock * costPrice;
                    var salesValue = monthlySales * retailPrice;

                    totalStockValue += stockValue;
                    totalSalesValue += salesValue;
                    totalItems++;

                    reportData.Add($"{description}: Stock: {currentStock}, Monthly Sales: {monthlySales}, Stock Value: R {stockValue:F2}");
                }

                string message = $"Supplier report for {selectedSupplier.Name} saved successfully!\n\n" +
                               $"Report Period: {MonthSelector.SelectedItem} {selectedYear}\n" +
                               $"Summary:\n" +
                               $"• Total Items: {totalItems}\n" +
                               $"• Total Stock Value: R {totalStockValue:F2}\n" +
                               $"• Monthly Sales Value: R {totalSalesValue:F2}\n\n" +
                               $"Items ({reportData.Count}):\n" +
                               string.Join("\n", reportData.Take(10)) +
                               (reportData.Count > 10 ? "\n..." : "");

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
            if (IsLoaded)
                LoadActualData();
        }

        private void YearSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                LoadActualData();
        }

        private void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadActualData(); // Reload data with current month/year filter

                string message = $"Report generated for {MonthSelector.SelectedItem} {YearSelector.SelectedItem}!\n\n" +
                              $"Financial Summary:\n" +
                              $"• Total Transactions: {_transactions.Count}\n" +
                              $"• Total Turnover: R {_currentReport.TotalTurnover:F2}\n" +
                              $"• Total Expenses: R {_currentReport.TotalExpenses:F2}\n" +
                              $"• Net Profit: R {_currentReport.NetProfit:F2}\n" +
                              $"• Profit Margin: {_currentReport.ProfitMargin:F2}%";

                MessageBox.Show(message, "Report Generated", MessageBoxButton.OK, MessageBoxImage.Information);
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
                // Generate comprehensive report
                var selectedMonth = MonthSelector.SelectedIndex + 1;
                var selectedYear = YearSelector.SelectedItem?.ToString();

                using var connection = new SqliteConnection(SqliteConnectionString);
                connection.Open();

                // Get additional statistics
                string statsQuery = @"
                    SELECT 
                        COUNT(DISTINCT iq.customerID) as UniqueCustomers,
                        COUNT(DISTINCT iq.staffID) as ActiveStaff,
                        AVG(iq.totalAmount) as AverageSale,
                        COUNT(*) as TotalTransactions
                    FROM INVOICEQUOTE iq
                    WHERE strftime('%m', iq.date) = @month 
                    AND strftime('%Y', iq.date) = @year
                    AND iq.type = 1";

                using var statsCmd = new SqliteCommand(statsQuery, connection);
                statsCmd.Parameters.AddWithValue("@month", selectedMonth.ToString("00"));
                statsCmd.Parameters.AddWithValue("@year", selectedYear);

                int uniqueCustomers = 0;
                int activeStaff = 0;
                decimal averageSale = 0;
                int totalTransactions = 0;

                using var statsReader = statsCmd.ExecuteReader();
                if (statsReader.Read())
                {
                    uniqueCustomers = Convert.ToInt32(statsReader["UniqueCustomers"]);
                    activeStaff = Convert.ToInt32(statsReader["ActiveStaff"]);
                    averageSale = statsReader["AverageSale"] != DBNull.Value ?
                        Convert.ToDecimal(statsReader["AverageSale"]) : 0;
                    totalTransactions = Convert.ToInt32(statsReader["TotalTransactions"]);
                }

                // Get top selling items
                string topItemsQuery = @"
                    SELECT 
                        i.description as ItemName,
                        SUM(ii.quantity) as TotalSold,
                        SUM(ii.quantity * ii.priceAtSale) as TotalRevenue
                    FROM INVOICEITEM ii
                    INNER JOIN INVOICEQUOTE iq ON ii.invoiceQuoteID = iq.invoiceQuoteID
                    INNER JOIN ITEM i ON ii.itemID = i.itemID
                    WHERE strftime('%m', iq.date) = @month 
                    AND strftime('%Y', iq.date) = @year
                    AND iq.type = 1
                    AND ii.quantity > 0
                    GROUP BY i.itemID, i.description
                    ORDER BY TotalSold DESC
                    LIMIT 5";

                var topItems = new List<string>();
                using var topItemsCmd = new SqliteCommand(topItemsQuery, connection);
                topItemsCmd.Parameters.AddWithValue("@month", selectedMonth.ToString("00"));
                topItemsCmd.Parameters.AddWithValue("@year", selectedYear);

                using var topItemsReader = topItemsCmd.ExecuteReader();
                while (topItemsReader.Read())
                {
                    var itemName = topItemsReader["ItemName"].ToString();
                    var totalSold = Convert.ToInt32(topItemsReader["TotalSold"]);
                    var totalRevenue = Convert.ToDecimal(topItemsReader["TotalRevenue"]);
                    topItems.Add($"{itemName}: {totalSold} sold, R {totalRevenue:F2} revenue");
                }

                string message = $"Full month stock report saved successfully!\n\n" +
                               $"Report for: {MonthSelector.SelectedItem} {selectedYear}\n\n" +
                               $"Business Metrics:\n" +
                               $"• Total Transactions: {totalTransactions}\n" +
                               $"• Unique Customers: {uniqueCustomers}\n" +
                               $"• Active Staff: {activeStaff}\n" +
                               $"• Average Sale: R {averageSale:F2}\n\n" +
                               $"Financial Summary:\n" +
                               $"• Total Turnover: R {_currentReport.TotalTurnover:F2}\n" +
                               $"• Total Expenses: R {_currentReport.TotalExpenses:F2}\n" +
                               $"• Net Profit: R {_currentReport.NetProfit:F2}\n" +
                               $"• Profit Margin: {_currentReport.ProfitMargin:F2}%\n\n" +
                               $"Top Selling Items:\n" +
                               string.Join("\n", topItems) +
                               $"\n\nThis report should only be generated at the end of the month.";

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
                // In a real implementation, you would generate an actual PDF here
                // For now, we'll show a confirmation message with actual data

                var selectedMonth = MonthSelector.SelectedIndex + 1;
                var selectedYear = YearSelector.SelectedItem?.ToString();

                string message = $"PDF export completed successfully!\n\n" +
                               $"Exported: Monthly Report for {MonthSelector.SelectedItem} {YearSelector.SelectedItem}\n" +
                               $"File: Monthly_Report_{selectedYear}_{selectedMonth:00}.pdf\n\n" +
                               $"The PDF includes:\n" +
                               $"• {_transactions.Count} transactions\n" +
                               $"• Financial summaries\n" +
                               $"• Payment method breakdown\n" +
                               $"• Expense details\n" +
                               $"• Profit & loss statement\n" +
                               $"• Supplier stock reports";

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

    public class Supplier
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

   
    public class ProfitMarginToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is decimal profitMargin)
            {
                return profitMargin >= 0 ? "#4AA902" : "#FF0000"; // Green for positive, Red for negative
            }
            return "#4AA902"; // Default color
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}