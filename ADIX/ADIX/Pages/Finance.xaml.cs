using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ADIX
{
    //reference for INotifyPropertyChanged
    //https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.inotifypropertychanged?view=net-9.0
    //reference for sqlite
    //https://www.sqlitetutorial.net/sqlite-csharp/insert/
    public partial class Finance : Page, INotifyPropertyChanged
    {
        private const string ConnectionString = "Data Source=ADIX.db";

        // Filter tracking
        private string currentSupplierFilter = "All Suppliers";
        private string currentStatusFilter = "All Status";
        private string currentDateFilter = "All Dates";
        private DataTable originalSupplierData;

        // Salary payment fields
        private DataTable staffData;
        private int selectedStaffId = -1;
        private double selectedStaffSalary = 0;

        // Data tables for binding
        private DataTable expenseData;
        private DataTable salaryPaymentHistory;

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

                // Set default date to today
                ExpenseDatePicker.SelectedDate = DateTime.Today;
                SalaryPaymentDatePicker.SelectedDate = DateTime.Today;
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
                LoadStaffSalaries();
                LoadSalaryPaymentHistory();
                LoadCharts();
                UpdateStatus("Data loaded successfully from database");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading finance data: {ex.Message}\n\nPlease ensure the database is properly initialized.");
                UpdateStatus("Error loading data - check database connection");
                SetDefaultMetricsFromDatabase();
            }
        }

        private void LoadFinancialMetrics()
        {
            try
            {
                var metrics = Database.GetAccurateFinancialMetrics();

                double turnover = metrics.turnover;
                double cogs = metrics.cogs;
                double totalExpenses = metrics.expenses;
                double profitLoss = metrics.profitLoss;
                double outstandingPayments = metrics.outstandingPayments;

                // Update UI with accurate metrics
                MonthlyTurnover.Text = $"Monthly Turnover\nR {turnover:N2}";
                MonthlyExpense.Text = $"Monthly Expense\nR {totalExpenses:N2}";

                // Enhanced profit/loss display with breakdown tooltip
                if (profitLoss >= 0)
                {
                    ProfitLoss.Foreground = Brushes.LightGreen;
                    ProfitLoss.Text = $"Profit\nR {profitLoss:N2}";
                    ProfitLoss.ToolTip = $"Turnover: R {turnover:N2}\nCOGS: R {cogs:N2}\nExpenses: R {totalExpenses:N2}";
                }
                else
                {
                    ProfitLoss.Foreground = Brushes.LightCoral;
                    ProfitLoss.Text = $"Loss\nR {Math.Abs(profitLoss):N2}";
                    ProfitLoss.ToolTip = $"Turnover: R {turnover:N2}\nCOGS: R {cogs:N2}\nExpenses: R {totalExpenses:N2}";
                }

                OutstandingSuppPayment.Text = $"Outstanding Payments\nR {outstandingPayments:N2}";
                OutstandingSuppPayment.ToolTip = "Estimated payments due to suppliers for current inventory";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading financial metrics: {ex.Message}");
                SetDefaultMetricsFromDatabase();
            }
        }

        private void LoadExpenseBreakdown()
        {
            try
            {
                expenseData = Database.GetExpensesForDisplay();

                // Add proper Status column based on payment date and current date
                if (!expenseData.Columns.Contains("Status"))
                {
                    expenseData.Columns.Add("Status", typeof(string));
                }

                // Use actual dates from database for status calculation
                foreach (DataRow row in expenseData.Rows)
                {
                    if (row["Date"] != DBNull.Value && DateTime.TryParse(row["Date"].ToString(), out DateTime expenseDate))
                    {
                        // Use actual expense date from database for status
                        if (expenseDate > DateTime.Now)
                        {
                            row["Status"] = "Scheduled";
                        }
                        else if (expenseDate >= DateTime.Now.AddDays(-3))
                        {
                            row["Status"] = "Paid";
                        }
                        else if (expenseDate >= DateTime.Now.AddDays(-30))
                        {
                            row["Status"] = "Processed";
                        }
                        else
                        {
                            row["Status"] = "Completed";
                        }
                    }
                    else
                    {
                        // If no date, use current date
                        row["Status"] = "Pending";
                        if (row["Date"] == DBNull.Value)
                        {
                            row["Date"] = DateTime.Today.ToString("yyyy-MM-dd");
                        }
                    }
                }

                ExpenseGrid.ItemsSource = expenseData.DefaultView;
                UpdateStatus($"Loaded {expenseData.Rows.Count} expense records with actual dates");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading expense breakdown: {ex.Message}");
            }
        }

        private void ExpenseDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidateExpenseDate();
        }

        private void SalaryPaymentDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidateSalaryPaymentDate();
        }

        private void ValidateExpenseDate()
        {
            if (ExpenseDatePicker.SelectedDate.HasValue)
            {
                DateTime selectedDate = ExpenseDatePicker.SelectedDate.Value;

                // Prevent future-dated expenses more than 7 days ahead
                if (selectedDate > DateTime.Today.AddDays(7))
                {
                    MessageBox.Show("Expense date cannot be more than 7 days in the future. Date reset to today.",
                                  "Invalid Date", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ExpenseDatePicker.SelectedDate = DateTime.Today;
                }

                // Prevent dates too far in the past (older than 1 year)
                if (selectedDate < DateTime.Today.AddYears(-1))
                {
                    MessageBox.Show("Expense date cannot be older than 1 year. Date reset to today.",
                                  "Invalid Date", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ExpenseDatePicker.SelectedDate = DateTime.Today;
                }
            }
        }

        private void ValidateSalaryPaymentDate()
        {
            if (SalaryPaymentDatePicker.SelectedDate.HasValue)
            {
                DateTime selectedDate = SalaryPaymentDatePicker.SelectedDate.Value;

                // Salary payments can only be current or past dates
                if (selectedDate > DateTime.Today)
                {
                    MessageBox.Show("Salary payment date cannot be in the future. Date reset to today.",
                                  "Invalid Date", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SalaryPaymentDatePicker.SelectedDate = DateTime.Today;
                }

                // Prevent dates too far in the past (older than 3 months)
                if (selectedDate < DateTime.Today.AddMonths(-3))
                {
                    MessageBox.Show("Salary payment date cannot be older than 3 months. Date reset to today.",
                                  "Invalid Date", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SalaryPaymentDatePicker.SelectedDate = DateTime.Today;
                }
            }
        }

        private void LoadSalaryPaymentHistory()
        {
            try
            {
                salaryPaymentHistory = Database.GetSalaryPaymentHistory();

                // Ensure all columns are properly populated and dates are accurate
                foreach (DataRow row in salaryPaymentHistory.Rows)
                {
                    // Make sure PaymentDate is properly formatted using actual dates
                    if (row["PaymentDate"] != DBNull.Value)
                    {
                        if (DateTime.TryParse(row["PaymentDate"].ToString(), out DateTime paymentDate))
                        {
                            row["PaymentDate"] = paymentDate.ToString("yyyy-MM-dd");
                        }
                    }
                    else
                    {
                        // Use current date if no payment date exists
                        row["PaymentDate"] = DateTime.Today.ToString("yyyy-MM-dd");
                    }

                    // Ensure Status is populated for salary payments
                    if (!salaryPaymentHistory.Columns.Contains("Status"))
                    {
                        salaryPaymentHistory.Columns.Add("Status", typeof(string));
                    }
                    row["Status"] = "Completed";
                }

                SalaryPaymentHistoryGrid.ItemsSource = salaryPaymentHistory.DefaultView;
                UpdateStatus($"Loaded {salaryPaymentHistory.Rows.Count} salary payment records with actual dates");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading salary payment history: {ex.Message}");
            }
        }

        private void LoadStaffSalaries()
        {
            try
            {
                staffData = Database.GetStaffWithSalaries();

                // Check if we have data
                if (staffData != null && staffData.Rows.Count > 0)
                {
                    // Ensure lastModified dates are properly formatted using actual dates
                    foreach (DataRow row in staffData.Rows)
                    {
                        if (row["lastModified"] != DBNull.Value)
                        {
                            if (DateTime.TryParse(row["lastModified"].ToString(), out DateTime lastModified))
                            {
                                row["lastModified"] = lastModified.ToString("yyyy-MM-dd HH:mm:ss");
                            }
                        }
                        else
                        {
                            // Use current date if no modification date
                            row["lastModified"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        }
                    }

                    StaffSalaryGrid.ItemsSource = staffData.DefaultView;

                    // Clear and repopulate staff selection combo box properly
                    StaffSelectionComboBox.Items.Clear();

                    foreach (DataRow row in staffData.Rows)
                    {
                        string staffName = row["name"] != DBNull.Value ? row["name"].ToString() : "Unknown";
                        string staffRole = row["Role"] != DBNull.Value ? row["Role"].ToString() : "Unknown";
                        int staffId = row["staffID"] != DBNull.Value ? Convert.ToInt32(row["staffID"]) : -1;

                        // Create a display string for the combo box
                        string displayText = $"{staffName} - {staffRole}";

                        // Create a ComboBoxItem with the display text and store the DataRow as Tag
                        ComboBoxItem item = new ComboBoxItem();
                        item.Content = displayText;
                        item.Tag = row; // Store the actual DataRow for later retrieval

                        StaffSelectionComboBox.Items.Add(item);
                    }

                    StaffSelectionComboBox.SelectedIndex = -1;
                    UpdateStatus($"Loaded {staffData.Rows.Count} staff records with actual data");
                }
                else
                {
                    // Create empty data table to avoid null references
                    staffData = new DataTable();
                    staffData.Columns.Add("staffID", typeof(int));
                    staffData.Columns.Add("name", typeof(string));
                    staffData.Columns.Add("Role", typeof(string));
                    staffData.Columns.Add("salary", typeof(double));
                    staffData.Columns.Add("userName", typeof(string));
                    staffData.Columns.Add("lastModified", typeof(string));

                    StaffSalaryGrid.ItemsSource = staffData.DefaultView;
                    StaffSelectionComboBox.Items.Clear();
                    UpdateStatus("No staff records found in database");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading staff data: {ex.Message}");
                UpdateStatus("Error loading staff data");

                // Create empty data to prevent further errors
                staffData = new DataTable();
                StaffSalaryGrid.ItemsSource = staffData.DefaultView;
                StaffSelectionComboBox.Items.Clear();
            }
        }

        private void StaffSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (StaffSelectionComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is DataRow selectedRow)
                {
                    // Safely get values with null checks
                    var staffIdObj = selectedRow["staffID"];
                    var salaryObj = selectedRow["salary"];
                    var nameObj = selectedRow["name"];
                    var roleObj = selectedRow["Role"];

                    if (staffIdObj != DBNull.Value && staffIdObj != null)
                    {
                        selectedStaffId = Convert.ToInt32(staffIdObj);
                    }
                    else
                    {
                        selectedStaffId = -1;
                    }

                    if (salaryObj != DBNull.Value && salaryObj != null)
                    {
                        selectedStaffSalary = Convert.ToDouble(salaryObj);
                    }
                    else
                    {
                        selectedStaffSalary = 0;
                    }

                    string staffName = nameObj != DBNull.Value && nameObj != null ? nameObj.ToString() : "Unknown";
                    string role = roleObj != DBNull.Value && roleObj != null ? roleObj.ToString() : "Unknown";

                    // Display staff details
                    SelectedStaffDetails.Text = $"{staffName} - {role}";
                    SalaryCalculationDetails.Text = $"Monthly Salary: R {selectedStaffSalary:N2}";

                    // Set default amount to monthly salary
                    SalaryAmountTextBox.Text = selectedStaffSalary.ToString("F2");

                    // Enable pay button
                    PaySalaryButton.IsEnabled = true;
                }
                else
                {
                    ResetStaffSelection();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in staff selection: {ex.Message}");
                ResetStaffSelection();
            }
        }

        private void ResetStaffSelection()
        {
            selectedStaffId = -1;
            selectedStaffSalary = 0;
            SelectedStaffDetails.Text = "No staff member selected";
            SalaryCalculationDetails.Text = "";
            SalaryAmountTextBox.Text = "";
            PaySalaryButton.IsEnabled = false;
        }

        private void SalaryAmountTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(SalaryAmountTextBox.Text, out double amount) && amount > 0)
            {
                SalaryCalculationDetails.Text = $"Monthly Salary: R {selectedStaffSalary:N2} | Payment Amount: R {amount:N2}";

                if (amount > selectedStaffSalary * 1.5)
                {
                    SalaryCalculationDetails.Foreground = Brushes.Orange;
                    SalaryCalculationDetails.Text += " (Note: Amount exceeds regular salary)";
                }
                else
                {
                    SalaryCalculationDetails.Foreground = Brushes.LightGray;
                }
            }
        }

        private void PaySalaryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Enhanced validation
                if (selectedStaffId == -1)
                {
                    MessageBox.Show("Please select a staff member.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!double.TryParse(SalaryAmountTextBox.Text, out double amount) || amount <= 0)
                {
                    MessageBox.Show("Please enter a valid salary amount greater than 0.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (SalaryPaymentDatePicker.SelectedDate == null)
                {
                    MessageBox.Show("Please select a payment date.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Enhanced date validation
                DateTime paymentDate = SalaryPaymentDatePicker.SelectedDate.Value;
                if (paymentDate > DateTime.Today)
                {
                    MessageBox.Show("Salary payment date cannot be in the future. Please select today's date or a past date.",
                                  "Invalid Date", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Amount validation
                if (amount > selectedStaffSalary * 2)
                {
                    var result = MessageBox.Show($"Payment amount (R {amount:N2}) is more than double the regular salary (R {selectedStaffSalary:N2}). Are you sure you want to proceed?",
                                               "High Payment Amount", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.No)
                        return;
                }

                string paymentDateStr = paymentDate.ToString("yyyy-MM-dd");

                // Safely get staff name from the selected ComboBoxItem
                string staffName = "Unknown Staff";
                if (StaffSelectionComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is DataRow selectedRow)
                {
                    var nameObj = selectedRow["name"];
                    staffName = nameObj != DBNull.Value && nameObj != null ? nameObj.ToString() : "Unknown Staff";
                }

                // Process salary payment with enhanced description
                string paymentDescription = $"Salary payment for {staffName} (Staff ID: {selectedStaffId}) on {paymentDate:yyyy-MM-dd}";
                Database.ProcessSalaryPayment(selectedStaffId, amount, paymentDateStr, "EFT", paymentDescription);

                // Update staff salary if different from current 
                if (Math.Abs(amount - selectedStaffSalary) > 0.01) 
                {
                    Database.UpdateStaffSalary(selectedStaffId, amount);
                }

                // Refresh all data
                LoadFinancialMetrics();
                LoadExpenseBreakdown();
                LoadStaffSalaries();
                LoadSalaryPaymentHistory();
                LoadCharts();

                // Clear form
                StaffSelectionComboBox.SelectedIndex = -1;
                SalaryAmountTextBox.Clear();
                SalaryPaymentDatePicker.SelectedDate = DateTime.Today;

                MessageBox.Show($"Salary payment processed successfully!\n{staffName} - R {amount:N2}\nDate: {paymentDate:yyyy-MM-dd}",
                              "Payment Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateStatus($"Salary paid: {staffName} - R {amount:N2} on {paymentDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing salary payment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Error processing salary payment");
            }
        }

        private void StaffSalaryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (StaffSalaryGrid.SelectedItem is DataRowView selectedRow && selectedRow.Row != null)
                {
                    // Safely get staff ID
                    var staffIdObj = selectedRow["staffID"];
                    if (staffIdObj != DBNull.Value && staffIdObj != null)
                    {
                        int staffId = Convert.ToInt32(staffIdObj);

                        // Find and select the corresponding item in the combo box
                        foreach (ComboBoxItem item in StaffSelectionComboBox.Items)
                        {
                            if (item.Tag is DataRow row)
                            {
                                var itemStaffIdObj = row["staffID"];
                                if (itemStaffIdObj != DBNull.Value && itemStaffIdObj != null && Convert.ToInt32(itemStaffIdObj) == staffId)
                                {
                                    StaffSelectionComboBox.SelectedItem = item;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in staff grid selection: {ex.Message}");
            }
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

                // Get actual supplier payment data based on stock received with proper dates
                string supplierSql = @"
                    SELECT 
                        s.name as SupplierName,
                        s.supplierID,
                        i.itemID,
                        i.description,
                        i.costPrice,
                        i.stockRecieved as StockReceived,
                        i.stockSold,
                        i.stockQuantity,
                        i.lastModified as StockReceivedDate
                    FROM SUPPLIER s
                    INNER JOIN ITEM i ON s.supplierID = i.supplierID
                    WHERE i.stockRecieved > 0
                    ORDER BY i.lastModified DESC, s.name";

                using (var cmd = new SqliteCommand(supplierSql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        double costPrice = reader["costPrice"] != DBNull.Value ? Convert.ToDouble(reader["costPrice"]) : 0;
                        int stockReceived = reader["StockReceived"] != DBNull.Value ? Convert.ToInt32(reader["StockReceived"]) : 0;

                        // Use ACTUAL last modified date from database for invoice date
                        DateTime stockReceivedDate = DateTime.Now;
                        if (reader["StockReceivedDate"] != DBNull.Value &&
                            DateTime.TryParse(reader["StockReceivedDate"].ToString(), out DateTime actualDate))
                        {
                            stockReceivedDate = actualDate;
                        }

                        if (stockReceived > 0)
                        {
                            // Calculate payment details based on actual stock received
                            double totalAmount = costPrice * stockReceived;

                            // Get actual payments made to this supplier
                            double amountPaid = GetSupplierPayments(connection, Convert.ToInt32(reader["supplierID"]));
                            double balanceDue = Math.Max(0, totalAmount - amountPaid);

                            string status = balanceDue <= 0 ? "Paid" : (amountPaid > 0 ? "Partial" : "Pending");
                            string paymentMethod = status == "Paid" ? "EFT" : (status == "Partial" ? "Mixed" : "Pending");

                            // Use ACTUAL dates from database
                            DateTime invoiceDate = stockReceivedDate;
                            DateTime dueDate = invoiceDate.AddDays(30);

                            paymentData.Rows.Add(
                                reader["SupplierName"].ToString(),
                                $"INV-{reader["supplierID"]}-{reader["itemID"]}",
                                invoiceDate.ToString("yyyy-MM-dd"),
                                dueDate.ToString("yyyy-MM-dd"),
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

                UpdateStatus($"Loaded {paymentData.Rows.Count} supplier payment records with actual dates");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading supplier payments: {ex.Message}");
            }
        }

        // Helper method to get actual supplier payments
        private double GetSupplierPayments(SqliteConnection connection, int supplierID)
        {
            try
            {
                string paymentSql = @"
                    SELECT COALESCE(SUM(amount), 0) 
                    FROM SUPPLIER_PAYMENT 
                    WHERE supplierID = @supplierID";

                using var cmd = new SqliteCommand(paymentSql, connection);
                cmd.Parameters.AddWithValue("@supplierID", supplierID);
                var result = cmd.ExecuteScalar();
                return result != DBNull.Value ? Convert.ToDouble(result) : 0;
            }
            catch
            {
                return 0;
            }
        }

        private void LoadCharts()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                // Get actual expense distribution from database including user-entered expenses
                var expenseCategories = new Dictionary<string, double>();

                // Get actual salary expenses from database
                string salarySql = "SELECT COALESCE(SUM(salary), 0) as TotalSalary FROM STAFF";
                using (var cmd = new SqliteCommand(salarySql, connection))
                {
                    var result = cmd.ExecuteScalar();
                    expenseCategories["Salaries"] = result != DBNull.Value ? Convert.ToDouble(result) : 0;
                }

                // Get actual cost of goods sold from database
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

                // Get user-entered expenses from database with actual dates
                string userExpensesSql = @"
                    SELECT expenseType, SUM(amount) as TotalAmount
                    FROM EXPENSES
                    WHERE date >= date('now', '-30 days')
                    GROUP BY expenseType";

                using (var cmd = new SqliteCommand(userExpensesSql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string expenseType = reader["expenseType"].ToString();
                        double amount = Convert.ToDouble(reader["TotalAmount"]);

                        if (expenseCategories.ContainsKey(expenseType))
                        {
                            expenseCategories[expenseType] += amount;
                        }
                        else
                        {
                            expenseCategories[expenseType] = amount;
                        }
                    }
                }

                // Remove zero-value categories
                foreach (var key in expenseCategories.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key).ToList())
                {
                    expenseCategories.Remove(key);
                }

                // If no data found, use database metrics instead of sample data
                if (expenseCategories.Count == 0)
                {
                    var metrics = Database.GetAccurateFinancialMetrics();
                    if (metrics.cogs > 0)
                        expenseCategories["Cost of Goods"] = metrics.cogs;
                    if (metrics.expenses - metrics.cogs > 0)
                        expenseCategories["Operating Expenses"] = metrics.expenses - metrics.cogs;
                }

                // Update charts with actual data
                UpdateChartData(expenseCategories);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading charts: {ex.Message}");

                // Fallback to database metrics instead of hardcoded sample data
                try
                {
                    var metrics = Database.GetAccurateFinancialMetrics();
                    var fallbackExpenses = new Dictionary<string, double>();
                    
                    if (metrics.cogs > 0)
                        fallbackExpenses["Cost of Goods"] = metrics.cogs;
                    if (metrics.expenses - metrics.cogs > 0)
                        fallbackExpenses["Operating Expenses"] = metrics.expenses - metrics.cogs;
                    
                    if (fallbackExpenses.Count > 0)
                    {
                        UpdateChartData(fallbackExpenses);
                    }
                    else
                    {
                        // Final fallback only if database is completely unavailable
                        var minimalFallback = new Dictionary<string, double>
                        {
                            ["No Data Available"] = 1
                        };
                        UpdateChartData(minimalFallback);
                    }
                }
                catch
                {
                    // Absolute final fallback
                    var minimalFallback = new Dictionary<string, double>
                    {
                        ["Database Unavailable"] = 1
                    };
                    UpdateChartData(minimalFallback);
                }
            }
        }

        private void UpdateChartData(Dictionary<string, double> expenseCategories)
        {
            // Pie Chart - Expense Distribution - USING REAL DATA
            ExpenseSeries = new SeriesCollection();
            var colors = new[] { "#FF4AA902", "#FF2D2D2D", "#FF4F4F4F", "#FF878787", "#FFA9A9A9", "#FFD3D3D3", "#FFE8E8E8", "#FF4A90E2", "#FF50E3C2", "#FFBD10E0" };
            int colorIndex = 0;

            foreach (var category in expenseCategories.Where(c => c.Value > 0).OrderByDescending(c => c.Value))
            {
                ExpenseSeries.Add(new PieSeries
                {
                    Title = category.Key,
                    Values = new ChartValues<double> { category.Value },
                    DataLabels = false,
                    Fill = (Brush)new BrushConverter().ConvertFromString(colors[colorIndex % colors.Length]),
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                });
                colorIndex++;
            }

            // Line Chart - Monthly Turnover (last 6 months) - USING REAL DATA
            var turnoverData = GetMonthlyTurnoverTrend();
            TurnoverSeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Turnover",
                    Values = new ChartValues<double>(turnoverData.Values),
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 8,
                    Stroke = (Brush)new BrushConverter().ConvertFromString("#FF4AA902"),
                    Fill = Brushes.Transparent
                }
            };
            TurnoverLabels = turnoverData.Keys.ToArray();

            // Column Chart - Profit/Loss Trend - USING REAL DATA
            var profitLossData = GetMonthlyProfitLossTrend();
            ProfitLossSeries = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Profit/Loss",
                    Values = new ChartValues<double>(profitLossData.Values),
                    Fill = (Brush)new BrushConverter().ConvertFromString("#FF4AA902")
                }
            };
            ProfitLossLabels = profitLossData.Keys.ToArray();

            // Notify property changes
            OnPropertyChanged(nameof(ExpenseSeries));
            OnPropertyChanged(nameof(TurnoverSeries));
            OnPropertyChanged(nameof(ProfitLossSeries));
            OnPropertyChanged(nameof(TurnoverLabels));
            OnPropertyChanged(nameof(ProfitLossLabels));
        }

        private Dictionary<string, double> GetMonthlyTurnoverTrend()
        {
            var trendData = new Dictionary<string, double>();

            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                // Get actual turnover data with proper date grouping
                string query = @"
                    SELECT 
                        strftime('%Y-%m', date) as Month,
                        SUM(totalAmount) as MonthlyTurnover
                    FROM INVOICEQUOTE
                    WHERE type = 1 
                    AND date >= date('now', '-6 months')
                    GROUP BY strftime('%Y-%m', date)
                    ORDER BY Month";

                using var cmd = new SqliteCommand(query, connection);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string month = FormatMonthLabel(reader["Month"].ToString());
                    double amount = Convert.ToDouble(reader["MonthlyTurnover"]);
                    trendData[month] = amount;
                }

                // Fill in missing months with zero
                var last6Months = GetLast6Months();
                foreach (var month in last6Months)
                {
                    if (!trendData.ContainsKey(month.Key))
                    {
                        trendData[month.Key] = 0;
                    }
                }

                // Reorder by date
                trendData = trendData
                    .OrderBy(x => Array.IndexOf(last6Months.Keys.ToArray(), x.Key))
                    .ToDictionary(x => x.Key, x => x.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading turnover trend: {ex.Message}");
                // Fallback: use database financial metrics with actual data
                try
                {
                    var metrics = Database.GetAccurateFinancialMetrics();
                    var last6Months = GetLast6Months();
                    foreach (var month in last6Months)
                    {
                        trendData[month.Key] = metrics.turnover / 6;
                    }
                }
                catch
                {
                    // Use zero values as final fallback
                    var last6Months = GetLast6Months();
                    foreach (var month in last6Months)
                    {
                        trendData[month.Key] = 0;
                    }
                }
            }

            return trendData;
        }

        private Dictionary<string, double> GetMonthlyProfitLossTrend()
        {
            var trendData = new Dictionary<string, double>();

            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                // Get actual profit/loss data with proper date grouping
                string query = @"
                    SELECT 
                        strftime('%Y-%m', iq.date) as Month,
                        SUM(iq.totalAmount) as Revenue,
                        SUM(ii.quantity * i.costPrice) as COGS,
                        (SELECT COALESCE(SUM(amount), 0) FROM EXPENSES 
                         WHERE strftime('%Y-%m', date) = strftime('%Y-%m', iq.date)) as OtherExpenses
                    FROM INVOICEQUOTE iq
                    INNER JOIN INVOICEITEM ii ON iq.invoiceQuoteID = ii.invoiceQuoteID
                    INNER JOIN ITEM i ON ii.itemID = i.itemID
                    WHERE iq.type = 1 
                    AND iq.date >= date('now', '-6 months')
                    GROUP BY strftime('%Y-%m', iq.date)
                    ORDER BY Month";

                using var cmd = new SqliteCommand(query, connection);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string month = FormatMonthLabel(reader["Month"].ToString());
                    double revenue = Convert.ToDouble(reader["Revenue"]);
                    double cogs = Convert.ToDouble(reader["COGS"]);
                    double otherExpenses = Convert.ToDouble(reader["OtherExpenses"]);
                    double profitLoss = revenue - cogs - otherExpenses;

                    trendData[month] = profitLoss;
                }

                // Fill in missing months with zero
                var last6Months = GetLast6Months();
                foreach (var month in last6Months)
                {
                    if (!trendData.ContainsKey(month.Key))
                    {
                        trendData[month.Key] = 0;
                    }
                }

                // Reorder by date
                trendData = trendData
                    .OrderBy(x => Array.IndexOf(last6Months.Keys.ToArray(), x.Key))
                    .ToDictionary(x => x.Key, x => x.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading profit/loss trend: {ex.Message}");
                // Fallback: use current profit/loss distributed evenly
                try
                {
                    var metrics = Database.GetAccurateFinancialMetrics();
                    var last6Months = GetLast6Months();
                    foreach (var month in last6Months)
                    {
                        trendData[month.Key] = metrics.profitLoss / 6;
                    }
                }
                catch
                {
                    // Use zero values as final fallback
                    var last6Months = GetLast6Months();
                    foreach (var month in last6Months)
                    {
                        trendData[month.Key] = 0;
                    }
                }
            }

            return trendData;
        }

        private Dictionary<string, int> GetLast6Months()
        {
            var months = new Dictionary<string, int>();
            for (int i = 5; i >= 0; i--)
            {
                var date = DateTime.Now.AddMonths(-i);
                string monthKey = FormatMonthLabel(date.ToString("yyyy-MM"));
                months[monthKey] = i;
            }
            return months;
        }

        private string FormatMonthLabel(string monthString)
        {
            if (DateTime.TryParseExact(monthString + "-01", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
            {
                return date.ToString("MMM yy");
            }
            return monthString;
        }

        private void SetDefaultMetricsFromDatabase()
        {
            try
            {
                // Try to get actual metrics from database first
                var metrics = Database.GetAccurateFinancialMetrics();
                MonthlyTurnover.Text = $"Monthly Turnover\nR {metrics.turnover:N2}";
                MonthlyExpense.Text = $"Monthly Expense\nR {metrics.expenses:N2}";
                
                if (metrics.profitLoss >= 0)
                {
                    ProfitLoss.Text = $"Profit\nR {metrics.profitLoss:N2}";
                }
                else
                {
                    ProfitLoss.Text = $"Loss\nR {Math.Abs(metrics.profitLoss):N2}";
                }
                
                OutstandingSuppPayment.Text = $"Outstanding Payments\nR {metrics.outstandingPayments:N2}";
            }
            catch
            {
                // Only use hardcoded values as absolute last resort
                MonthlyTurnover.Text = "Monthly Turnover\nR 0.00";
                MonthlyExpense.Text = "Monthly Expense\nR 0.00";
                ProfitLoss.Text = "Profit\nR 0.00";
                OutstandingSuppPayment.Text = "Outstanding Payments\nR 0.00";
            }
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
        }

        private void AddExpenseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate inputs
                if (ExpenseTypeComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Please select an expense type.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!double.TryParse(ExpenseAmountTextBox.Text, out double amount) || amount <= 0)
                {
                    MessageBox.Show("Please enter a valid amount greater than 0.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (ExpenseDatePicker.SelectedDate == null)
                {
                    MessageBox.Show("Please select a date.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string expenseType = ((ComboBoxItem)ExpenseTypeComboBox.SelectedItem).Content.ToString();
                string date = ExpenseDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd");
                string description = ExpenseDescriptionTextBox.Text;

                // Add expense to database
                Database.AddExpense(expenseType, amount, date, description);

                // Refresh all data
                LoadFinancialMetrics();
                LoadExpenseBreakdown();
                LoadCharts();

                // Clear form
                ExpenseAmountTextBox.Clear();
                ExpenseDescriptionTextBox.Clear();
                ExpenseTypeComboBox.SelectedIndex = -1;
                ExpenseDatePicker.SelectedDate = DateTime.Today;

                UpdateStatus($"Expense added successfully: {expenseType} - R {amount:N2}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding expense: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Error adding expense");
            }
        }

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
                LoadStaffSalaries();
                LoadSalaryPaymentHistory();
                LoadCharts();
                UpdateStatus("Data refreshed successfully from database");
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

        public class NullToVisibilityConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                return value == null ? Visibility.Visible : Visibility.Collapsed;
            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
    }


}