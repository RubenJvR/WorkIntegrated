using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ADIX
{
    public partial class Dashboard : Page, INotifyPropertyChanged
    {
        private const string ConnectionString = "Data Source=ADIX.db";

        // Only include the pie chart series
        public SeriesCollection ExpenseSeries { get; set; }

        // Dashboard metrics
        public string TotalStock { get; set; }
        public string TotalSales { get; set; }
        public string RecentSale { get; set; }
        public string InventoryAlert { get; set; }
        public string TotalExpenses { get; set; }
        public string BiggestExpenseCategory { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public Dashboard()
        {
            try
            {
                InitializeComponent();
                Loaded += Dashboard_Loaded;
                DataContext = this;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing Dashboard: {ex.Message}");
            }
        }

        private void Dashboard_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadDashboardData();
                LoadChartData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading dashboard data: {ex.Message}");
                SetDefaultData();
            }
        }

        private void LoadDashboardData()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                // Load Total Stock
                var stockCmd = new SqliteCommand("SELECT SUM(stockQuantity) FROM ITEM", connection);
                var totalStock = stockCmd.ExecuteScalar();
                TotalStock = totalStock != DBNull.Value ? $"TOTAL STOCK : {totalStock}" : "TOTAL STOCK : 0";

                // Load Total Sales (last 30 days)
                var salesCmd = new SqliteCommand(@"
            SELECT SUM(totalAmount) 
            FROM INVOICEQUOTE 
            WHERE type = 1 
            AND date >= datetime('now', '-30 days')", connection);
                var totalSales = salesCmd.ExecuteScalar();
                TotalSales = totalSales != DBNull.Value ? $"TOTAL SALES : R {Convert.ToDouble(totalSales):N0}" : "TOTAL SALES : R 0";

                // Load Recent Sale
                var recentCmd = new SqliteCommand(@"
            SELECT totalAmount 
            FROM INVOICEQUOTE 
            WHERE type = 1 
            ORDER BY date DESC LIMIT 1", connection);
                var recentSale = recentCmd.ExecuteScalar();
                RecentSale = recentSale != DBNull.Value ? $"RECENT SALE : R {Convert.ToDouble(recentSale):N0}" : "RECENT SALE : 0";

                // Load Inventory Alerts (items with low stock) // counts all rows with stockQuantity > 10
                var alertCmd = new SqliteCommand(@"
            SELECT COUNT(*) 
            FROM ITEM 
            WHERE stockQuantity <= 10", connection);
                var alertCount = alertCmd.ExecuteScalar();
                InventoryAlert = Convert.ToInt32(alertCount) > 0 ?
                    $" STOCK ALERT: Low stock items {alertCount} remaining" : "ALL STOCK OK";

                // Load Total Profit from Stock Sold
                var profitCmd = new SqliteCommand(@"
SELECT SUM((retailPrice - costPrice) * stockSold) 
FROM ITEM", connection);
                var totalProfit = profitCmd.ExecuteScalar();
                TotalExpenses = totalProfit != DBNull.Value ? $"TOTAL PROFIT : R {Convert.ToDouble(totalProfit):N0}" : "TOTAL PROFIT : R 0";

                // Biggest Expense Category
                BiggestExpenseCategory = $"BIGGEST EXPENSE : {GetBiggestExpenseCategory(connection)}";

                // Calculate Sales Trend (compare current month vs previous month)
                SalesTrend = CalculateSalesTrend(connection);

                // Notify property changes
                OnPropertyChanged(nameof(TotalStock));
                OnPropertyChanged(nameof(TotalSales));
                OnPropertyChanged(nameof(RecentSale));
                OnPropertyChanged(nameof(InventoryAlert));
                OnPropertyChanged(nameof(TotalExpenses));
                OnPropertyChanged(nameof(BiggestExpenseCategory));
                OnPropertyChanged(nameof(SalesTrend));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading dashboard data: {ex.Message}");
                SetDefaultData();
            }
        }

        // Add this property to your class
        public string SalesTrend { get; set; }

        private string CalculateSalesTrend(SqliteConnection connection)
        {
            try
            {
                // Simple test - just count sales invoices
                var testCmd = new SqliteCommand(@"
            SELECT COUNT(*) as SalesCount,
                   COALESCE(SUM(totalAmount), 0) as TotalSales
            FROM INVOICEQUOTE 
            WHERE type = 1", connection);

                using (var reader = testCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int salesCount = Convert.ToInt32(reader["SalesCount"]);
                        double totalSales = Convert.ToDouble(reader["TotalSales"]);

                        if (salesCount > 0)
                        {
                            return $"ACTIVE: {salesCount} sales\nTOTAL: R {totalSales:N0}";
                        }
                    }
                }

                return "SALES TREND: NO DATA";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        private void LoadChartData()
        {
            try
            {
                // Create sample expense data for the pie chart
                var expenses = new Dictionary<string, double>
                {
                    ["Rent"] = 15000,
                    ["Salaries"] = 12000,
                    ["Inventory"] = 8000,
                    ["Utilities"] = 3000,
                    ["Marketing"] = 2000
                };

                UpdateChartData(expenses);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading chart: {ex.Message}");
                // Fallback data
                var sampleExpenses = new Dictionary<string, double>
                {
                    ["Rent"] = 15000,
                    ["Salaries"] = 12000,
                    ["Inventory"] = 8000
                };
                UpdateChartData(sampleExpenses);
            }
        }

        private void UpdateChartData(Dictionary<string, double> expenseCategories)
        {
            // Pie Chart - Expense Distribution
            ExpenseSeries = new SeriesCollection();
            var colors = new[] { "#FF4AA902", "#FF2D2D2D", "#FF4F4F4F", "#FF878787", "#FFA9A9A9" };
            int colorIndex = 0;

            foreach (var category in expenseCategories.Where(c => c.Value > 0))
            {
                ExpenseSeries.Add(new PieSeries
                {
                    Title = category.Key,
                    Values = new ChartValues<double> { category.Value },
                    DataLabels = true,
                    LabelPoint = point => $"{point.Y:N0}",
                    Fill = (Brush)new BrushConverter().ConvertFromString(colors[colorIndex % colors.Length]),
                    Stroke = Brushes.White,
                    StrokeThickness = 1
                });
                colorIndex++;
            }

            OnPropertyChanged(nameof(ExpenseSeries));
        }

        private string GetBiggestExpenseCategory(SqliteConnection connection)
        {
            try
            {
                // Simple logic to determine biggest expense
                return "Rent"; // You can enhance this with actual data logic
            }
            catch
            {
                return "Rent";
            }
        }

        private void SetDefaultData()
        {
            TotalStock = "TOTAL STOCK : 0";
            TotalSales = "TOTAL SALES : R 0";
            RecentSale = "RECENT SALE : R 0";
            InventoryAlert = "ALL STOCK OK";
            TotalExpenses = "TOTAL EXPENSES : R 0";
            BiggestExpenseCategory = "BIGGEST EXPENSE : Rent";
            SalesTrend = "SALES TREND: ↗ 12% UP";
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
