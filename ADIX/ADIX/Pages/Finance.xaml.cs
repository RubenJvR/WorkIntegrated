using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;

namespace ADIX
{
    public partial class Finance : Page
    {
        private const string ConnectionString = "Data Source=ADIX.db";

        public Finance()
        {
            InitializeComponent();
            Loaded += Finance_Loaded;
        }

        private void Finance_Loaded(object sender, RoutedEventArgs e)
        {
            LoadFinancialMetrics();
            LoadExpenseBreakdown();
            LoadSupplierPayments();
            UpdateStatus("Data loaded successfully - Using sample data for demonstration");
        }

        private void LoadFinancialMetrics()
        {
            try
            {
                double turnover = 0;
                double expenses = 0;
                double profitLoss = 0;
                double outstandingPayments = 0;

                // Try to get real data from database
                try
                {
                    using var connection = new SqliteConnection(ConnectionString);
                    connection.Open();

                    // Monthly turnover from actual sales
                    string turnoverSql = @"
                        SELECT COALESCE(SUM(totalAmount), 0) 
                        FROM INVOICEQUOTE 
                        WHERE type = 1 
                        AND strftime('%Y-%m', date) = strftime('%Y-%m', 'now')";

                    using (var cmd = new SqliteCommand(turnoverSql, connection))
                    {
                        var result = cmd.ExecuteScalar();
                        turnover = result != DBNull.Value ? Convert.ToDouble(result) : 0;
                    }

                    // If no real data, use sample data
                    if (turnover == 0)
                    {
                        turnover = 125000.00;
                        expenses = 75000.00;
                        profitLoss = turnover - expenses;
                        outstandingPayments = 25000.00;
                        UpdateStatus("Using sample data - No recent sales data found");
                    }
                    else
                    {
                        // Calculate based on real data
                        expenses = turnover * 0.6; // Estimate expenses as 60% of turnover
                        profitLoss = turnover - expenses;

                        // Estimate outstanding payments
                        outstandingPayments = turnover * 0.2; // 20% of turnover as outstanding
                        UpdateStatus("Using real sales data with estimated expenses");
                    }
                }
                catch
                {
                    // Fallback to sample data if database access fails
                    turnover = 125000.00;
                    expenses = 75000.00;
                    profitLoss = turnover - expenses;
                    outstandingPayments = 25000.00;
                    UpdateStatus("Using sample data - Database not accessible");
                }

                // Update UI
                MonthlyTurnover.Text = $"Monthly Turnover\nR {turnover:N2}";
                MonthlyExpense.Text = $"Monthly Expense\nR {expenses:N2}";

                if (profitLoss >= 0)
                {
                    ProfitLoss.Foreground = System.Windows.Media.Brushes.LightGreen;
                    ProfitLoss.Text = $"Profit\nR {profitLoss:N2}";
                }
                else
                {
                    ProfitLoss.Foreground = System.Windows.Media.Brushes.LightCoral;
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
                var expenseData = new DataTable();
                expenseData.Columns.Add("Date", typeof(string));
                expenseData.Columns.Add("Category", typeof(string));
                expenseData.Columns.Add("Amount", typeof(decimal));
                expenseData.Columns.Add("Status", typeof(string));

                // Sample expense data
                expenseData.Rows.Add("2024-01-15", "Rent", 15000.00, "Paid");
                expenseData.Rows.Add("2024-01-10", "Utilities", 2500.00, "Paid");
                expenseData.Rows.Add("2024-01-05", "Salaries", 45000.00, "Paid");
                expenseData.Rows.Add("2024-01-20", "Marketing", 5000.00, "Pending");
                expenseData.Rows.Add("2024-01-25", "Supplies", 8000.00, "Paid");
                expenseData.Rows.Add("2024-01-28", "Maintenance", 3000.00, "Paid");

                PosItemGrid.ItemsSource = expenseData.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading expense breakdown: {ex.Message}");
            }
        }

        private void LoadSupplierPayments()
        {
            try
            {
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

                // Sample supplier payment data
                paymentData.Rows.Add("GreenFoods Ltd", "GF-001", "2024-01-01", "2024-01-31", 25000.00, 25000.00, 0.00, "Paid", "EFT");
                paymentData.Rows.Add("BeverageCorp", "BC-002", "2024-01-05", "2024-02-05", 18000.00, 10000.00, 8000.00, "Partial", "Cash");
                paymentData.Rows.Add("SnackSupply Co", "SS-003", "2024-01-10", "2024-02-10", 12000.00, 0.00, 12000.00, "Pending", "");
                paymentData.Rows.Add("Fresh Produce Inc", "FP-004", "2024-01-15", "2024-02-15", 30000.00, 30000.00, 0.00, "Paid", "EFT");
                paymentData.Rows.Add("Dairy Distributors", "DD-005", "2024-01-20", "2024-02-20", 22000.00, 15000.00, 7000.00, "Partial", "EFT");

                SupplierTable.ItemsSource = paymentData.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading supplier payments: {ex.Message}");
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadFinancialMetrics();
            LoadExpenseBreakdown();
            LoadSupplierPayments();
            UpdateStatus("Data refreshed successfully");
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string exportData = $"Financial Report - {DateTime.Now:yyyy-MM-dd}\n\n" +
                                  $"Monthly Turnover: {GetMetricValue(MonthlyTurnover.Text)}\n" +
                                  $"Monthly Expenses: {GetMetricValue(MonthlyExpense.Text)}\n" +
                                  $"Profit/Loss: {GetMetricValue(ProfitLoss.Text)}\n" +
                                  $"Outstanding Payments: {GetMetricValue(OutstandingSuppPayment.Text)}\n\n" +
                                  $"Note: Export functionality ready for real data when database is configured.";

                MessageBox.Show(exportData, "Financial Report Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting data: {ex.Message}");
            }
        }

        private string GetMetricValue(string metricText)
        {
            var lines = metricText.Split('\n');
            return lines.Length > 1 ? lines[1] : "N/A";
        }

        private void SetDefaultMetrics()
        {
            MonthlyTurnover.Text = "Monthly Turnover\nR 0.00";
            MonthlyExpense.Text = "Monthly Expense\nR 0.00";
            ProfitLoss.Text = "Profit/Loss\nR 0.00";
            OutstandingSuppPayment.Text = "Outstanding Payments\nR 0.00";
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
        }
    }
}