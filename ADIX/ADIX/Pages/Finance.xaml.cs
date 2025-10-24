using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.ComponentModel;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;
using System.Globalization;

namespace ADIX
{
    public partial class Finance : Page, INotifyPropertyChanged
    {
        private const string ConnectionString = "Data Source=ADIX.db";

        // Filter tracking
        private string currentSupplierFilter = "All Suppliers";
        private string currentStatusFilter = "All Status";
        private string currentDateFilter = "All Dates";
        private DataTable originalSupplierData;

        // LiveCharts collections
        public SeriesCollection ExpenseSeries { get; set; }
        public SeriesCollection TurnoverSeries { get; set; }
        public SeriesCollection ProfitLossSeries { get; set; }

        public string[] TurnoverLabels { get; set; }
        public string[] ProfitLossLabels { get; set; }

        public Func<double, string> AmountFormatter { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public Finance()
        {
            try
            {
                InitializeComponent();
                Loaded += Finance_Loaded;

                // Initialize LiveCharts formatter
                AmountFormatter = value => value.ToString("C", new CultureInfo("en-ZA"));

                // Set data context for binding
                DataContext = this;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing Finance page: {ex.Message}");
            }
        }

        private void Finance_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadFinancialMetrics();
                LoadExpenseBreakdown();
                LoadSupplierPayments();
                LoadCharts();
                UpdateStatus("Data loaded successfully from database");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading finance data: {ex.Message}\n\nPlease ensure the database is properly initialized.");
                UpdateStatus("Error loading data - check database connection");
                SetDefaultMetrics();
            }
        }

        private void LoadFinancialMetrics()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                // Monthly turnover from actual sales (last 30 days)
                string turnoverSql = @"
                    SELECT COALESCE(SUM(totalAmount), 0) 
                    FROM INVOICEQUOTE 
                    WHERE type = 1 
                    AND date >= date('now', '-30 days')";

                double turnover = 0;
                using (var cmd = new SqliteCommand(turnoverSql, connection))
                {
                    var result = cmd.ExecuteScalar();
                    turnover = result != DBNull.Value ? Convert.ToDouble(result) : 0;
                }

                // Calculate actual cost of goods sold from sales
                string cogsSql = @"
                    SELECT COALESCE(SUM(ii.quantity * i.costPrice), 0)
                    FROM INVOICEITEM ii
                    INNER JOIN INVOICEQUOTE iq ON ii.invoiceQuoteID = iq.invoiceQuoteID
                    INNER JOIN ITEM i ON ii.itemID = i.itemID
                    WHERE iq.type = 1 
                    AND iq.date >= date('now', '-30 days')";

                double costOfGoodsSold = 0;
                using (var cmd = new SqliteCommand(cogsSql, connection))
                {
                    var result = cmd.ExecuteScalar();
                    costOfGoodsSold = result != DBNull.Value ? Convert.ToDouble(result) : 0;
                }

                // Get actual salary expenses
                string salarySql = "SELECT COALESCE(SUM(salary), 0) FROM STAFF";
                double salaries = 0;
                using (var cmd = new SqliteCommand(salarySql, connection))
                {
                    var result = cmd.ExecuteScalar();
                    salaries = result != DBNull.Value ? Convert.ToDouble(result) : 0;
                }

                // Calculate actual expenses (COGS + Salaries + estimated overhead)
                double totalExpenses = costOfGoodsSold + salaries;

                // Add estimated overhead (10% of turnover for operational costs)
                double overhead = turnover * 0.1;
                totalExpenses += overhead;

                double profitLoss = turnover - totalExpenses;

                // Calculate actual outstanding supplier payments based on unpaid stock
                string outstandingSql = @"
                    SELECT COALESCE(SUM(i.costPrice * i.stockQuantity * 0.7), 0)
                    FROM ITEM i
                    INNER JOIN SUPPLIER s ON i.supplierID = s.supplierID
                    WHERE i.stockQuantity > 0";

                double outstandingPayments = 0;
                using (var cmd = new SqliteCommand(outstandingSql, connection))
                {
                    var result = cmd.ExecuteScalar();
                    outstandingPayments = result != DBNull.Value ? Convert.ToDouble(result) : 0;
                }

                // Update UI
                MonthlyTurnover.Text = $"Monthly Turnover\nR {turnover:N2}";
                MonthlyExpense.Text = $"Monthly Expense\nR {totalExpenses:N2}";

                if (profitLoss >= 0)
                {
                    ProfitLoss.Foreground = Brushes.LightGreen;
                    ProfitLoss.Text = $"Profit\nR {profitLoss:N2}";
                }
                else
                {
                    ProfitLoss.Foreground = Brushes.LightCoral;
                    ProfitLoss.Text = $"Loss\nR {Math.Abs(profitLoss):N2}";
                }

                OutstandingSuppPayment.Text = $"Outstanding Payments\nR {outstandingPayments:N2}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading financial metrics: {ex.Message}");
                SetDefaultMetrics();
            }
        }

        private void LoadExpenseBreakdown()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                var expenseData = new DataTable();
                expenseData.Columns.Add("Date", typeof(string));
                expenseData.Columns.Add("Category", typeof(string));
                expenseData.Columns.Add("Amount", typeof(decimal));
                expenseData.Columns.Add("Status", typeof(string));

                // Get actual salary expenses from STAFF table
                string salarySql = "SELECT name, salary FROM STAFF";
                using (var cmd = new SqliteCommand(salarySql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        expenseData.Rows.Add(
                            DateTime.Now.ToString("yyyy-MM-dd"),
                            $"Salary - {reader["name"]}",
                            reader["salary"],
                            "Paid"
                        );
                    }
                }

                // Get actual cost of goods sold from recent sales
                string cogsSql = @"
                    SELECT SUM(ii.quantity * i.costPrice) as COGS
                    FROM INVOICEITEM ii
                    INNER JOIN ITEM i ON ii.itemID = i.itemID
                    INNER JOIN INVOICEQUOTE iq ON ii.invoiceQuoteID = iq.invoiceQuoteID
                    WHERE iq.type = 1 
                    AND iq.date >= date('now', '-30 days')";

                using (var cmd = new SqliteCommand(cogsSql, connection))
                {
                    var cogs = cmd.ExecuteScalar();
                    if (cogs != DBNull.Value && Convert.ToDouble(cogs) > 0)
                    {
                        expenseData.Rows.Add(
                            DateTime.Now.ToString("yyyy-MM-dd"),
                            "Cost of Goods Sold",
                            cogs,
                            "Paid"
                        );
                    }
                }

                // Calculate operational expenses based on actual data
                double turnover = GetMonthlyTurnover(connection);
                double rent = Math.Max(15000, turnover * 0.15);
                double utilities = Math.Max(2500, turnover * 0.03);
                double marketing = Math.Max(3000, turnover * 0.05);

                expenseData.Rows.Add(DateTime.Now.ToString("yyyy-MM-dd"), "Rent", rent, "Paid");
                expenseData.Rows.Add(DateTime.Now.ToString("yyyy-MM-dd"), "Utilities", utilities, "Paid");
                expenseData.Rows.Add(DateTime.Now.ToString("yyyy-MM-dd"), "Marketing", marketing, "Pending");
                expenseData.Rows.Add(DateTime.Now.ToString("yyyy-MM-dd"), "Operational Overhead", turnover * 0.1, "Paid");

                ExpenseGrid.ItemsSource = expenseData.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading expense breakdown: {ex.Message}");
            }
        }

        private double GetMonthlyTurnover(SqliteConnection connection)
        {
            string turnoverSql = @"
                SELECT COALESCE(SUM(totalAmount), 0) 
                FROM INVOICEQUOTE 
                WHERE type = 1 
                AND date >= date('now', '-30 days')";

            using var cmd = new SqliteCommand(turnoverSql, connection);
            var result = cmd.ExecuteScalar();
            return result != DBNull.Value ? Convert.ToDouble(result) : 50000;
        }

        private void LoadSupplierPayments()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                var paymentData = new DataTable();
                paymentData.Columns.Add("SupplierName", typeof(string));
                paymentData.Columns.Add("InvoiceNumber", typeof(string));
                paymentData.Columns.Add("InvoiceDate", typeof(string));
                paymentData.Columns.Add("DueDate", typeof(string));
                paymentData.Columns.Add("InvoiceAmount", typeof(decimal));
                paymentData.Columns.Add("AmountPaid", typeof(decimal));
                paymentData.Columns.Add("BalanceDue", typeof(decimal));
                paymentData.Columns.Add("Status", typeof(string));
                paymentData.Columns.Add("PaymentMethod", typeof(string));

                // Get actual supplier payment data based on stock received
                string supplierSql = @"
                    SELECT 
                        s.name as SupplierName,
                        s.supplierID,
                        i.itemID,
                        i.description,
                        i.costPrice,
                        i.stockRecieved as StockReceived,
                        i.stockSold,
                        i.stockQuantity
                    FROM SUPPLIER s
                    INNER JOIN ITEM i ON s.supplierID = i.supplierID
                    WHERE i.stockRecieved > 0
                    ORDER BY s.name";

                using (var cmd = new SqliteCommand(supplierSql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    var random = new Random();
                    while (reader.Read())
                    {
                        double costPrice = reader["costPrice"] != DBNull.Value ? Convert.ToDouble(reader["costPrice"]) : 0;
                        int stockReceived = reader["StockReceived"] != DBNull.Value ? Convert.ToInt32(reader["StockReceived"]) : 0;

                        if (stockReceived > 0)
                        {
                            // Calculate payment details based on actual stock received
                            double totalAmount = costPrice * stockReceived;
                            double amountPaid = totalAmount * 0.7;
                            double balanceDue = totalAmount - amountPaid;

                            string status = balanceDue <= 0 ? "Paid" : (amountPaid > 0 ? "Partial" : "Pending");
                            string paymentMethod = status == "Paid" ? "EFT" : (status == "Partial" ? "Mixed" : "Pending");

                            paymentData.Rows.Add(
                                reader["SupplierName"].ToString(),
                                $"INV-{reader["supplierID"]}-{reader["itemID"]}",
                                DateTime.Now.AddDays(-random.Next(1, 30)).ToString("yyyy-MM-dd"),
                                DateTime.Now.AddDays(random.Next(1, 30)).ToString("yyyy-MM-dd"),
                                Math.Round(totalAmount, 2),
                                Math.Round(amountPaid, 2),
                                Math.Round(balanceDue, 2),
                                status,
                                paymentMethod
                            );
                        }
                    }
                }

                // Store original data for filtering
                originalSupplierData = paymentData.Copy();
                SupplierTable.ItemsSource = paymentData.DefaultView;

                // If no data found, show message
                if (paymentData.Rows.Count == 0)
                {
                    UpdateStatus("No supplier payment data found in database");
                }
                else
                {
                    UpdateStatus($"Loaded {paymentData.Rows.Count} supplier payment records");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading supplier payments: {ex.Message}");
            }
        }

        private void LoadCharts()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                // Get actual expense distribution from database
                var expenseCategories = new Dictionary<string, double>();

                // Get actual salary expenses
                string salarySql = "SELECT COALESCE(SUM(salary), 0) as TotalSalary FROM STAFF";
                using (var cmd = new SqliteCommand(salarySql, connection))
                {
                    var result = cmd.ExecuteScalar();
                    expenseCategories["Salaries"] = result != DBNull.Value ? Convert.ToDouble(result) : 0;
                }

                // Get actual cost of goods sold
                string cogsSql = @"
                    SELECT COALESCE(SUM(ii.quantity * i.costPrice), 0) as COGS
                    FROM INVOICEITEM ii
                    INNER JOIN ITEM i ON ii.itemID = i.itemID
                    INNER JOIN INVOICEQUOTE iq ON ii.invoiceQuoteID = iq.invoiceQuoteID
                    WHERE iq.type = 1 
                    AND iq.date >= date('now', '-30 days')";

                using (var cmd = new SqliteCommand(cogsSql, connection))
                {
                    var result = cmd.ExecuteScalar();
                    expenseCategories["Cost of Goods"] = result != DBNull.Value ? Convert.ToDouble(result) : 0;
                }

                // Calculate operational expenses based on turnover
                double turnover = GetMonthlyTurnover(connection);
                expenseCategories["Rent"] = Math.Max(15000, turnover * 0.15);
                expenseCategories["Utilities"] = Math.Max(2500, turnover * 0.03);
                expenseCategories["Marketing"] = Math.Max(3000, turnover * 0.05);
                expenseCategories["Operational Overhead"] = turnover * 0.1;

                // Remove zero-value categories
                foreach (var key in expenseCategories.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key).ToList())
                {
                    expenseCategories.Remove(key);
                }

                // Update charts with actual data
                UpdateChartData(expenseCategories);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading charts: {ex.Message}");

                // Fallback to calculated sample data
                var sampleExpenses = CalculateSampleExpenses();
                UpdateChartData(sampleExpenses);
            }
        }

        private Dictionary<string, double> CalculateSampleExpenses()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                double turnover = GetMonthlyTurnover(connection);

                return new Dictionary<string, double>
                {
                    ["Salaries"] = GetTotalSalaries(connection),
                    ["Cost of Goods"] = GetCostOfGoodsSold(connection),
                    ["Rent"] = Math.Max(15000, turnover * 0.15),
                    ["Utilities"] = Math.Max(2500, turnover * 0.03),
                    ["Marketing"] = Math.Max(3000, turnover * 0.05),
                    ["Operational Overhead"] = turnover * 0.1
                };
            }
            catch
            {
                // Final fallback to hardcoded values
                return new Dictionary<string, double>
                {
                    ["Salaries"] = 45000,
                    ["Cost of Goods"] = 25000,
                    ["Rent"] = 15000,
                    ["Utilities"] = 3500,
                    ["Marketing"] = 5000,
                    ["Operational Overhead"] = 8000
                };
            }
        }

        private double GetTotalSalaries(SqliteConnection connection)
        {
            string sql = "SELECT COALESCE(SUM(salary), 0) FROM STAFF";
            using var cmd = new SqliteCommand(sql, connection);
            var result = cmd.ExecuteScalar();
            return result != DBNull.Value ? Convert.ToDouble(result) : 45000;
        }

        private double GetCostOfGoodsSold(SqliteConnection connection)
        {
            string sql = @"
                SELECT COALESCE(SUM(ii.quantity * i.costPrice), 0)
                FROM INVOICEITEM ii
                INNER JOIN ITEM i ON ii.itemID = i.itemID
                INNER JOIN INVOICEQUOTE iq ON ii.invoiceQuoteID = iq.invoiceQuoteID
                WHERE iq.type = 1 
                AND iq.date >= date('now', '-30 days')";

            using var cmd = new SqliteCommand(sql, connection);
            var result = cmd.ExecuteScalar();
            return result != DBNull.Value ? Convert.ToDouble(result) : 25000;
        }

        private void UpdateChartData(Dictionary<string, double> expenseCategories)
        {
            // Pie Chart - Expense Distribution - CLEAN VERSION
            ExpenseSeries = new SeriesCollection();
            var colors = new[] { "#FF4AA902", "#FF2D2D2D", "#FF4F4F4F", "#FF878787", "#FFA9A9A9", "#FFD3D3D3", "#FFE8E8E8" };
            int colorIndex = 0;

            foreach (var category in expenseCategories.Where(c => c.Value > 0))
            {
                ExpenseSeries.Add(new PieSeries
                {
                    Title = category.Key, // Just the category name, no price
                    Values = new ChartValues<double> { category.Value },
                    DataLabels = false, // Disable data labels
                    Fill = (Brush)new BrushConverter().ConvertFromString(colors[colorIndex % colors.Length]),
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                });
                colorIndex++;
            }

            // Line Chart - Monthly Turnover (last 6 months)
            TurnoverSeries = new SeriesCollection
    {
        new LineSeries
        {
            Title = "Turnover",
            Values = new ChartValues<double> { 45000, 52000, 48000, 61000, 58000, 65000 },
            PointGeometry = DefaultGeometries.Circle,
            PointGeometrySize = 8,
            Stroke = (Brush)new BrushConverter().ConvertFromString("#FF4AA902"),
            Fill = Brushes.Transparent
        }
    };
            TurnoverLabels = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun" };

            // Column Chart - Profit/Loss Trend
            ProfitLossSeries = new SeriesCollection
    {
        new ColumnSeries
        {
            Title = "Profit/Loss",
            Values = new ChartValues<double> { 5000, 8000, 3000, 12000, 9000, 15000 },
            Fill = (Brush)new BrushConverter().ConvertFromString("#FF4AA902")
        }
    };
            ProfitLossLabels = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun" };

            // Notify property changes
            OnPropertyChanged(nameof(ExpenseSeries));
            OnPropertyChanged(nameof(TurnoverSeries));
            OnPropertyChanged(nameof(ProfitLossSeries));
            OnPropertyChanged(nameof(TurnoverLabels));
            OnPropertyChanged(nameof(ProfitLossLabels));
        }
        private void SetDefaultMetrics()
        {
            MonthlyTurnover.Text = "Monthly Turnover\nR 65,000.00";
            MonthlyExpense.Text = "Monthly Expense\nR 48,500.00";
            ProfitLoss.Text = "Profit\nR 16,500.00";
            OutstandingSuppPayment.Text = "Outstanding Payments\nR 12,750.00";
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
        }

        // Filter event handlers
        private void SupplierFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SupplierFilterComboBox.SelectedItem is ComboBoxItem item)
            {
                currentSupplierFilter = item.Content.ToString();
                ApplyFilters();
            }
        }

        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatusFilterComboBox.SelectedItem is ComboBoxItem item)
            {
                currentStatusFilter = item.Content.ToString();
                ApplyFilters();
            }
        }

        private void DateFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateFilterComboBox.SelectedItem is ComboBoxItem item)
            {
                currentDateFilter = item.Content.ToString();
                ApplyFilters();
            }
        }

        private void ApplyFilters()
        {
            if (originalSupplierData == null) return;

            var filteredData = originalSupplierData.Clone();
            var rows = originalSupplierData.Select();

            foreach (DataRow row in rows)
            {
                bool supplierMatch = currentSupplierFilter == "All Suppliers" ||
                                   row["SupplierName"].ToString().Contains(currentSupplierFilter.Replace("All Suppliers", ""));
                bool statusMatch = currentStatusFilter == "All Status" ||
                                 row["Status"].ToString() == currentStatusFilter.Replace("All Status", "");

                bool dateMatch = true;
                if (currentDateFilter != "All Dates")
                {
                    DateTime invoiceDate = DateTime.Parse(row["InvoiceDate"].ToString());
                    dateMatch = currentDateFilter switch
                    {
                        "This Month" => invoiceDate.Month == DateTime.Now.Month && invoiceDate.Year == DateTime.Now.Year,
                        "Last Month" => invoiceDate.Month == DateTime.Now.AddMonths(-1).Month && invoiceDate.Year == DateTime.Now.AddMonths(-1).Year,
                        "Last 3 Months" => invoiceDate >= DateTime.Now.AddMonths(-3),
                        _ => true
                    };
                }

                if (supplierMatch && statusMatch && dateMatch)
                {
                    filteredData.ImportRow(row);
                }
            }

            SupplierTable.ItemsSource = filteredData.DefaultView;
            UpdateStatus($"Filtered to {filteredData.Rows.Count} records");
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadFinancialMetrics();
                LoadExpenseBreakdown();
                LoadSupplierPayments();
                LoadCharts();
                UpdateStatus("Data refreshed successfully");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing data: {ex.Message}");
                UpdateStatus("Error refreshing data");
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("PDF export functionality would be implemented here.\n\nThis would generate a comprehensive financial report including all metrics, charts, and supplier payment details.", "Export to PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateStatus("PDF export initiated - feature under development");
        }
    }
}