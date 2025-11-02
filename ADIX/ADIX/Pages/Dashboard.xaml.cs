using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ADIX
{
    public partial class Dashboard : Page, INotifyPropertyChanged
    {
        // Chart series
        public SeriesCollection ExpenseSeries { get; set; }

        // Dashboard metrics
        public string TotalStock { get; set; }
        public string TotalSales { get; set; }
        public string RecentSale { get; set; }
        public string InventoryAlert { get; set; }
        public string TotalExpenses { get; set; }
        public string BiggestExpenseCategory { get; set; }
        public string SalesTrend { get; set; }
        public string TotalProfit { get; set; }

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
                using var connection = new SqliteConnection(Database.SqliteConnectionString);
                connection.Open();

                // 1. Load Total Stock Value (cost price * quantity) - ACCURATE DATA
                var stockCmd = new SqliteCommand(@"
                    SELECT COALESCE(SUM(stockQuantity * costPrice), 0) 
                    FROM ITEM 
                    WHERE stockQuantity > 0", connection);
                var totalStockValue = Convert.ToDouble(stockCmd.ExecuteScalar());
                TotalStock = $"R {totalStockValue:N2}";

                // 2. Load Total Sales (last 30 days) - ACCURATE DATA
                var salesCmd = new SqliteCommand(@"
                    SELECT COALESCE(SUM(totalAmount), 0) 
                    FROM INVOICEQUOTE 
                    WHERE type = 1 
                    AND date >= date('now', '-30 days')", connection);
                var totalSales = Convert.ToDouble(salesCmd.ExecuteScalar());
                TotalSales = $"R {totalSales:N2}";

                // 3. Load Most Recent Sale - ACCURATE DATA
                var recentCmd = new SqliteCommand(@"
                    SELECT totalAmount, date 
                    FROM INVOICEQUOTE 
                    WHERE type = 1 
                    ORDER BY date DESC LIMIT 1", connection);
                using (var reader = recentCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var amount = Convert.ToDouble(reader["totalAmount"]);
                        var date = DateTime.Parse(reader["date"].ToString());
                        RecentSale = $"R {amount:N2}\n{date:MMM dd}";
                    }
                    else
                    {
                        RecentSale = "No sales";
                    }
                }

                // 4. Load Inventory Alerts (items below minimum stock) - ACCURATE DATA
                var alertCmd = new SqliteCommand(@"
                    SELECT COUNT(*) 
                    FROM ITEM 
                    WHERE stockQuantity <= minimumStock 
                    AND minimumStock > 0", connection);
                var alertCount = Convert.ToInt32(alertCmd.ExecuteScalar());

                if (alertCount > 0)
                {
                    var lowStockCmd = new SqliteCommand(@"
                        SELECT description, stockQuantity, minimumStock 
                        FROM ITEM 
                        WHERE stockQuantity <= minimumStock 
                        AND minimumStock > 0 
                        LIMIT 3", connection);

                    var alertDetails = new List<string>();
                    using (var reader = lowStockCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            alertDetails.Add($"{reader["description"]}: {reader["stockQuantity"]}/{reader["minimumStock"]}");
                        }
                    }
                    // Format with proper line breaks
                    InventoryAlert = $"{alertCount} items low\n{string.Join("\n", alertDetails.Take(2))}";
                }
                else
                {
                    InventoryAlert = "All items OK";
                }

                // 5. Load Total Expenses (last 30 days) - ACCURATE DATA
                var expensesCmd = new SqliteCommand(@"
                    SELECT COALESCE(SUM(amount), 0) 
                    FROM EXPENSES 
                    WHERE date >= date('now', '-30 days')", connection);
                var totalExpenses = Convert.ToDouble(expensesCmd.ExecuteScalar());
                TotalExpenses = $"R {totalExpenses:N2}";

                // 6. Load Biggest Expense Category - ACCURATE DATA
                var biggestExpenseCmd = new SqliteCommand(@"
                    SELECT expenseType, SUM(amount) as Total
                    FROM EXPENSES
                    WHERE date >= date('now', '-30 days')
                    GROUP BY expenseType
                    ORDER BY Total DESC
                    LIMIT 1", connection);

                using (var reader = biggestExpenseCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var category = reader["expenseType"].ToString();
                        var amount = Convert.ToDouble(reader["Total"]);
                        BiggestExpenseCategory = $"{category}";
                    }
                    else
                    {
                        BiggestExpenseCategory = "None";
                    }
                }

                // 7. Calculate Sales Trend (current month vs previous month) - ACCURATE DATA
                SalesTrend = CalculateSalesTrend(connection);

                // 8. Load Total Profit (last 30 days) - ACCURATE DATA
                var profitCmd = new SqliteCommand(@"
                    SELECT 
                        COALESCE(SUM(ii.quantity * (ii.priceAtSale - i.costPrice)), 0) as Profit
                    FROM INVOICEITEM ii
                    INNER JOIN INVOICEQUOTE iq ON ii.invoiceQuoteID = iq.invoiceQuoteID
                    INNER JOIN ITEM i ON ii.itemID = i.itemID
                    WHERE iq.type = 1 
                    AND iq.date >= date('now', '-30 days')
                    AND ii.quantity > 0", connection);

                var totalProfit = Convert.ToDouble(profitCmd.ExecuteScalar());
                TotalProfit = $"R {totalProfit:N2}";

                // Notify property changes
                OnPropertyChanged(nameof(TotalStock));
                OnPropertyChanged(nameof(TotalSales));
                OnPropertyChanged(nameof(RecentSale));
                OnPropertyChanged(nameof(InventoryAlert));
                OnPropertyChanged(nameof(TotalExpenses));
                OnPropertyChanged(nameof(BiggestExpenseCategory));
                OnPropertyChanged(nameof(SalesTrend));
                OnPropertyChanged(nameof(TotalProfit));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading dashboard data: {ex.Message}");
                SetDefaultData();
            }
        }

        private string CalculateSalesTrend(SqliteConnection connection)
        {
            try
            {
                // Get current month sales
                var currentMonthCmd = new SqliteCommand(@"
                    SELECT COALESCE(SUM(totalAmount), 0) 
                    FROM INVOICEQUOTE 
                    WHERE type = 1 
                    AND strftime('%Y-%m', date) = strftime('%Y-%m', 'now')", connection);
                var currentMonthSales = Convert.ToDouble(currentMonthCmd.ExecuteScalar());

                // Get previous month sales
                var prevMonthCmd = new SqliteCommand(@"
                    SELECT COALESCE(SUM(totalAmount), 0) 
                    FROM INVOICEQUOTE 
                    WHERE type = 1 
                    AND strftime('%Y-%m', date) = strftime('%Y-%m', 'now', '-1 month')", connection);
                var prevMonthSales = Convert.ToDouble(prevMonthCmd.ExecuteScalar());

                if (prevMonthSales > 0)
                {
                    var trendPercent = ((currentMonthSales - prevMonthSales) / prevMonthSales) * 100;
                    var trendIcon = trendPercent >= 0 ? "↗" : "↘";
                    return $"{trendPercent:0}% {trendIcon}\nR {currentMonthSales:N0}";
                }
                else if (currentMonthSales > 0)
                {
                    return $"NEW DATA\nR {currentMonthSales:N0}";
                }

                return "NO DATA";
            }
            catch (Exception ex)
            {
                return $"ERROR";
            }
        }

        private void LoadChartData()
        {
            try
            {
                using var connection = new SqliteConnection(Database.SqliteConnectionString);
                connection.Open();

                // Get actual expense data from database - SAME AS FINANCE PAGE
                var expensesCmd = new SqliteCommand(@"
                    SELECT expenseType, SUM(amount) as Total
                    FROM EXPENSES
                    WHERE date >= date('now', '-30 days')
                    GROUP BY expenseType
                    ORDER BY Total DESC", connection);

                var expenses = new Dictionary<string, double>();
                using (var reader = expensesCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        expenses[reader["expenseType"].ToString()] = Convert.ToDouble(reader["Total"]);
                    }
                }

                // If no expense data, use database metrics
                if (expenses.Count == 0)
                {
                    // Try to get financial metrics as fallback
                    try
                    {
                        var metrics = Database.GetAccurateFinancialMetrics();
                        if (metrics.expenses > 0)
                        {
                            expenses["Operating Costs"] = metrics.expenses;
                        }
                    }
                    catch
                    {
                        // Final fallback only if database is completely unavailable
                        expenses = new Dictionary<string, double>
                        {
                            ["No Data"] = 1
                        };
                    }
                }

                UpdateChartData(expenses);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading chart data: {ex.Message}");
                // Fallback to database metrics
                try
                {
                    var metrics = Database.GetAccurateFinancialMetrics();
                    var fallbackExpenses = new Dictionary<string, double>();

                    if (metrics.expenses > 0)
                        fallbackExpenses["Operating Costs"] = metrics.expenses;

                    if (fallbackExpenses.Count > 0)
                    {
                        UpdateChartData(fallbackExpenses);
                    }
                    else
                    {
                        // Final fallback
                        var minimalFallback = new Dictionary<string, double>
                        {
                            ["Database Unavailable"] = 1
                        };
                        UpdateChartData(minimalFallback);
                    }
                }
                catch
                {
                    // Absolute final fallback
                    var minimalFallback = new Dictionary<string, double>
                    {
                        ["No Data Available"] = 1
                    };
                    UpdateChartData(minimalFallback);
                }
            }
        }

        private void UpdateChartData(Dictionary<string, double> expenseCategories)
        {
            ExpenseSeries = new SeriesCollection();

            // Use the EXACT SAME colors as Finance page
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
                    StrokeThickness = 2,
                    FontSize = 10
                });
                colorIndex++;
            }

            OnPropertyChanged(nameof(ExpenseSeries));
        }

        private void SetDefaultData()
        {
            TotalStock = "R 0";
            TotalSales = "R 0";
            RecentSale = "No sales";
            InventoryAlert = "All items OK";
            TotalExpenses = "R 0";
            BiggestExpenseCategory = "None";
            SalesTrend = "NO DATA";
            TotalProfit = "R 0";
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}