using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
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
using ADIX.Components;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using static iTextSharp.text.pdf.AcroFields;


namespace ADIX
{
    /// <summary>
    /// Interaction logic for Inventory.xaml
    /// </summary>
    public partial class Inventory : Page
    {
        private const string ConnStr = "Data Source=ADIX.db";
        private System.Windows.Threading.DispatcherTimer _inventoryRefreshTimer;
        public Inventory()
        {
            InitializeComponent();

            InventoryGrid.LoadingRow += InventoryGrid_LoadingRow;
            LoadInventoryAsync();

            ItemGroupFilter.SelectionChanged += Filter_Changed;
            LowStockToggle.Checked += (s, e) => ApplyFilters();
            LowStockToggle.Unchecked += (s, e) => ApplyFilters();

            LoadRefundsPerItem();



            // Setup auto-refresh timer for inventory
            _inventoryRefreshTimer = new System.Windows.Threading.DispatcherTimer();
            _inventoryRefreshTimer.Interval = TimeSpan.FromSeconds(30);
            _inventoryRefreshTimer.Tick += async (s, e) =>
            {
                if (Database.IsInternetAvailable())
                {
                    try
                    {
                        await Database.CheckAndSyncAsync();
                        LoadInventoryAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Inventory auto-refresh failed: {ex.Message}");
                    }
                }
            };

            this.Loaded += Inventory_Loaded;
            this.Unloaded += (s, e) => _inventoryRefreshTimer.Stop();
        }
        private async void SyncAllData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "Syncing All Data...";
                }

                MessageBox.Show("Starting comprehensive data sync. This may take a while...",
                              "Sync All Data",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);

                if (Database.IsInternetAvailable())
                {
                    bool success = await Database.SyncAllMissingDataAsync();
                    if (success)
                    {
                        LoadInventoryAsync(); // Refresh the grid
                        MessageBox.Show("All data synchronized successfully!",
                                      "Success",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Sync completed with some issues. Check console for details.",
                                      "Partial Success",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("No internet connection",
                                  "Error",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Comprehensive sync failed: {ex.Message}",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
            finally
            {
                var button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "Sync All Data";
                }
            }
        }
        private void ShowAllData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StringBuilder allData = new StringBuilder();
                allData.AppendLine("=== COMPLETE DATABASE COMPARISON ===\n");

                // LOCAL DATABASE
                allData.AppendLine("📍 LOCAL DATABASE (SQLite):");
                allData.AppendLine("─────────────────────────────");

                using (var localConn = new SqliteConnection("Data Source=ADIX.db"))
                {
                    localConn.Open();

                    // Get all items from local
                    var localCmd = new SqliteCommand(
                        "SELECT itemID, description, stockQuantity, stockSold, stockRecieved, lastModified FROM ITEM ORDER BY itemID",
                        localConn);

                    using (var localReader = localCmd.ExecuteReader())
                    {
                        bool hasLocalItems = false;
                        while (localReader.Read())
                        {
                            hasLocalItems = true;
                            allData.AppendLine($"ID: {localReader["itemID"]}");
                            allData.AppendLine($"  Name: {localReader["description"]}");
                            allData.AppendLine($"  Stock: {localReader["stockQuantity"]}");
                            allData.AppendLine($"  Sold: {localReader["stockSold"]}");
                            allData.AppendLine($"  Received: {localReader["stockRecieved"]}");
                            allData.AppendLine($"  Modified: {localReader["lastModified"]}");
                            allData.AppendLine();
                        }

                        if (!hasLocalItems)
                        {
                            allData.AppendLine("No items found in local database");
                        }
                    }

                    // Count totals
                    var countCmd = new SqliteCommand("SELECT COUNT(*) FROM ITEM", localConn);
                    int localCount = Convert.ToInt32(countCmd.ExecuteScalar());
                    allData.AppendLine($"TOTAL LOCAL ITEMS: {localCount}");
                }

                allData.AppendLine("\n" + new string('=', 50) + "\n");

                // AZURE DATABASE
                allData.AppendLine("☁️ AZURE DATABASE:");
                allData.AppendLine("──────────────────");

                if (!string.IsNullOrEmpty(Database.AzureSqlConnectionString))
                {
                    try
                    {
                        using (var azureConn = new SqlConnection(Database.AzureSqlConnectionString))
                        {
                            azureConn.Open();

                            // Get all items from Azure
                            var azureCmd = new SqlCommand(
                                "SELECT itemID, description, stockQuantity, stockSold, stockRecieved, lastModified FROM ITEM ORDER BY itemID",
                                azureConn);

                            using (var azureReader = azureCmd.ExecuteReader())
                            {
                                bool hasAzureItems = false;
                                while (azureReader.Read())
                                {
                                    hasAzureItems = true;
                                    allData.AppendLine($"ID: {azureReader["itemID"]}");
                                    allData.AppendLine($"  Name: {azureReader["description"]}");
                                    allData.AppendLine($"  Stock: {azureReader["stockQuantity"]}");
                                    allData.AppendLine($"  Sold: {azureReader["stockSold"]}");
                                    allData.AppendLine($"  Received: {azureReader["stockRecieved"]}");
                                    allData.AppendLine($"  Modified: {azureReader["lastModified"]}");
                                    allData.AppendLine();
                                }

                                if (!hasAzureItems)
                                {
                                    allData.AppendLine("No items found in Azure database");
                                }
                            }

                            // Count totals
                            var countCmd = new SqlCommand("SELECT COUNT(*) FROM ITEM", azureConn);
                            int azureCount = Convert.ToInt32(countCmd.ExecuteScalar());
                            allData.AppendLine($"TOTAL AZURE ITEMS: {azureCount}");
                        }
                    }
                    catch (Exception azureEx)
                    {
                        allData.AppendLine($"❌ ERROR CONNECTING TO AZURE:");
                        allData.AppendLine($"   {azureEx.Message}");
                    }
                }
                else
                {
                    allData.AppendLine("Azure connection string not configured");
                }

                allData.AppendLine("\n=== END COMPARISON ===");

                // Show in scrollable message box if too long
                string data = allData.ToString();

                // Create a scrollable text window
                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 600,
                    MaxWidth = 800
                };

                var textBlock = new TextBlock
                {
                    Text = data,
                    FontFamily = new FontFamily("Consolas, Courier New"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.NoWrap
                };

                scrollViewer.Content = textBlock;

                var window = new Window
                {
                    Title = "Complete Database Comparison",
                    Content = scrollViewer,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    MaxWidth = 1000,
                    MaxHeight = 800
                };

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void ForceSync_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Starting manual sync...", "Sync", MessageBoxButton.OK, MessageBoxImage.Information);

                if (Database.IsInternetAvailable())
                {
                    await Database.CheckAndSyncAsync();
                    RefreshAfterSync(); // Refresh the grid after sync
                    MessageBox.Show("Sync completed!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("No internet connection", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sync failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadInventoryAsync()
        {
            try
            {
                using var conn = new SqliteConnection(ConnStr);
                await conn.OpenAsync();

                string query = @"
            SELECT 
                sku,
                itemID,
                itemGroup,
                description,
                costPrice,
                retailPrice,
                stockQuantity,
                stockSold,
                stockRecieved,
                minimumStock
            FROM ITEM;
        ";

                using var cmd = new SqliteCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                var inventoryList = new List<InventoryItem>();

                while (await reader.ReadAsync())
                {
                    double ParseDouble(object o) =>
                        o == DBNull.Value ? 0 : Convert.ToDouble(o);

                    int ParseInt(object o) =>
                        o == DBNull.Value ? 0 : Convert.ToInt32(o);

                    var item = new InventoryItem
                    {
                        ItemID = Convert.ToInt32(reader["itemID"]),

                        ItemGroup = reader["itemGroup"]?.ToString() ?? "N/A",

                        SKU = reader["sku"] == DBNull.Value
                            ? "SKU-UNKNOWN"
                            : $"SKU-{reader["sku"]}",

                        ItemName = reader["description"]?.ToString() ?? "Unknown",

                        OpeningStockQuantity = Convert.ToInt32(reader["stockQuantity"]),
                        StockSold = Convert.ToInt32(reader["stockSold"]),
                        StockReceived = Convert.ToInt32(reader["stockRecieved"]),

                        CostPrice = ParseDouble(reader["costPrice"]),
                        RetailPrice = ParseDouble(reader["retailPrice"]),

                        StockReturned = 0,
                        StockRefunded = 0,

                        CostOfBusinessWorkings = Convert.ToDouble(reader["costPrice"]),
                        ReturnedStockUnusable = 0,

                        MinimumStock = GetInt(reader, "minimumStock"),

                    };

                    //Calculations
                    item.BalanceStock = item.OpeningStockQuantity - item.StockSold;
                    item.Loss = item.CostOfBusinessWorkings * item.ReturnedStockUnusable;

                    inventoryList.Add(item);
                }

                // Store full list
                // Clear and refill observable collection
                FullInventoryList.Clear();
                foreach (var item in inventoryList)
                    FullInventoryList.Add(item);

                // Create the view once
                if (InventoryView == null)
                {
                    InventoryView = CollectionViewSource.GetDefaultView(FullInventoryList);
                    InventoryView.Filter = InventoryFilter;
                    InventoryGrid.ItemsSource = InventoryView;
                }

                InventoryView.Refresh();


                InventoryGrid.LoadingRow += (s, e) =>
                {
                    var rowItem = e.Row.DataContext as InventoryItem;
                    if (rowItem != null && rowItem.BalanceStock < rowItem.MinimumStock)
                    {
                        e.Row.Background = new SolidColorBrush(Colors.IndianRed);
                    }
                    else
                    {
                        e.Row.Background = new SolidColorBrush(Colors.Transparent);
                    }
                };

                PopulateFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading inventory: {ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async void IncreaseMinStock_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is InventoryItem item)
            {
                item.MinimumStock++;
                await UpdateMinimumStockInDB(item);
                InventoryGrid.Items.Refresh();
            }
        }

        private async void DecreaseMinStock_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is InventoryItem item)
            {
                if (item.MinimumStock > 0)
                    item.MinimumStock--;

                await UpdateMinimumStockInDB(item);
                InventoryGrid.Items.Refresh();
            }
        }
        private async void RefreshAfterSync()
        {
            // Small delay to ensure sync completes
            await Task.Delay(1000);
            LoadInventoryAsync();
        }
        private async Task UpdateMinimumStockInDB(InventoryItem item)
        {
            try
            {
                using var conn = new SqliteConnection(ConnStr);
                await conn.OpenAsync();

                string updateSql = "UPDATE ITEM SET minimumStock = @min, lastModified = CURRENT_TIMESTAMP WHERE itemID = @id";
                using var cmd = new SqliteCommand(updateSql, conn);
                cmd.Parameters.AddWithValue("@min", item.MinimumStock);
                cmd.Parameters.AddWithValue("@id", item.ItemID);

                await cmd.ExecuteNonQueryAsync();

                // MARK SYNC AS REQUIRED
                Database.MarkSyncRequired();
                Console.WriteLine($"Updated minimum stock for item {item.ItemID} to {item.MinimumStock}");

                // Force immediate sync if online
                if (Database.IsInternetAvailable())
                {
                    try
                    {
                        await Database.CheckAndSyncAsync();

                        // CRITICAL: Reload the inventory AFTER sync to get any changes from Azure
                        LoadInventoryAsync();
                    }
                    catch (Exception syncEx)
                    {
                        Console.WriteLine($"Immediate sync after min stock update failed: {syncEx.Message}");
                        // Don't show error to user - it will sync eventually
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"DB update failed: {ex.Message}");
            }
        }

        private int GetInt(SqliteDataReader reader, string column)
        {
            return reader[column] == DBNull.Value ? 0 : Convert.ToInt32(reader[column]);
        }

        private void MinStockTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (InventoryGrid.CurrentCell != null)
            {
                InventoryGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            }
        }

        private ObservableCollection<InventoryItem> FullInventoryList = new ObservableCollection<InventoryItem>();
        private ICollectionView InventoryView;

        private void PopulateFilters()
        {
            var groups = FullInventoryList
                           .Select(i => i.ItemGroup)
                           .Distinct()
                           .OrderBy(x => x)
                           .ToList();
            groups.Insert(0, "All");

            ItemGroupFilter.ItemsSource = groups;
            ItemGroupFilter.SelectedIndex = 0;

        }

        private void LoadRefundsPerItem()
        {
            try
            {
                using (var conn = new SqliteConnection(ConnStr))
                {
                    conn.Open();

                    string query = @"
                SELECT 
                    ii.itemID,
                    COUNT(*) AS RefundCount
                FROM INVOICEQUOTE iq
                JOIN INVOICEITEM ii ON iq.invoiceQuoteID = ii.invoiceQuoteID
                WHERE iq.paymentMethod = 'Return'
                GROUP BY ii.itemID;
            ";

                    using (var cmd = new SqliteCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        // Loop through refunds and update your FullInventoryList items
                        while (reader.Read())
                        {
                            int itemID = reader.GetInt32(0);
                            int refundCount = reader.GetInt32(1);

                            // Find matching item in your loaded list
                            var item = FullInventoryList.FirstOrDefault(x => x.ItemID == itemID);
                            if (item != null)
                            {
                                item.StockRefunded = refundCount;
                            }
                        }
                    }
                }

                InventoryGrid.Items.Refresh(); // Refresh to display refund counts
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading refunds per item: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 🔍 Live search typing handler
        private async void ProductSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = ProductSearchTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(query))
            {
                AutoCompletePopup.IsOpen = false;
                InventoryGrid.ItemsSource = FullInventoryList;
                return;
            }

            var results = await SearchItemsAsync(query);
            AutoCompleteListBox.ItemsSource = results;
            AutoCompletePopup.IsOpen = results.Any();
        }

        // 🔍 DB search method
        private async Task<List<InventoryItem>> SearchItemsAsync(string searchTerm)
        {
            var list = new List<InventoryItem>();
            try
            {
                using var conn = new SqliteConnection(ConnStr);
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        itemID,
                        itemGroup,
                        description,
                        costPrice,
                        retailPrice,
                        stockQuantity,
                        stockSold,
                        stockRecieved,
                        minimumStock
                    FROM ITEM
                    WHERE description LIKE @term;
                ";

                using var cmd = new SqliteCommand(query, conn);
                cmd.Parameters.AddWithValue("@term", $"%{searchTerm}%");

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var item = new InventoryItem
                    {
                        ItemID = Convert.ToInt32(reader["itemID"]),
                        ItemGroup = reader["itemGroup"]?.ToString() ?? "N/A",
                        ItemName = reader["description"]?.ToString() ?? "Unknown",
                        CostPrice = Convert.ToDouble(reader["costPrice"]),
                        RetailPrice = Convert.ToDouble(reader["retailPrice"]),
                        OpeningStockQuantity = Convert.ToInt32(reader["stockQuantity"]),
                        StockSold = Convert.ToInt32(reader["stockSold"]),
                        StockReceived = Convert.ToInt32(reader["stockRecieved"]),
                        MinimumStock = Convert.ToInt32(reader["minimumStock"])
                    };
                    item.BalanceStock = item.OpeningStockQuantity - item.StockSold;
                    list.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Search error: {ex.Message}");
            }

            return list;
        }

        private void AutoCompleteListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AutoCompleteListBox.SelectedItem is InventoryItem selected)
            {
                ProductSearchTextBox.Text = selected.ItemName;
                AutoCompletePopup.IsOpen = false;
                InventoryGrid.ItemsSource = new List<InventoryItem> { selected };
            }
        }

        private void ProductSearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                AutoCompletePopup.IsOpen = false;
                ProductSearchTextBox.Clear();
            }
        }
        private void InventoryGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.DataContext is InventoryItem item && item.BalanceStock < item.MinimumStock)
                e.Row.Background = new SolidColorBrush(Colors.IndianRed);
            else
                e.Row.Background = Brushes.Transparent;
        }
        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            ItemGroupFilter.SelectedIndex = 0;
            LowStockToggle.IsChecked = false;
            ProductSearchTextBox.Text = string.Empty;
            ApplyFilters();
        }

        private bool InventoryFilter(object obj)
        {
            if (obj is not InventoryItem item) return true;

            // Item Group filter
            if (ItemGroupFilter.SelectedItem is string group && group != "All")
            {
                if (!string.Equals(item.ItemGroup, group, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Low stock toggle
            if (LowStockToggle.IsChecked == true)
            {
                if (!(item.BalanceStock < item.MinimumStock))
                    return false;
            }

            return true;
        }
        private void ApplyFilters()
        {
            InventoryView?.Refresh();
            InventoryGrid.Items.Refresh();
        }


        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }



        public class Product
        {
            public int ItemID { get; set; }
            public string SKU { get; set; }
            public string ItemGroup { get; set; }
            public string Description { get; set; }
            public decimal RetailPrice { get; set; }
            public decimal CostPrice { get; set; }
            public int StockQuantity { get; set; }
            public string StockRecieved { get; set; }
            public int StockSold { get; set; }
            public int SupplierID { get; set; }
            public int SellerID { get; set; }
            public int MinimumStock { get; set; }
        }


        private async void ImportCSV_Click(object sender, EventArgs e)
        {
            var button = sender as Button;
            string originalContent = button?.Content?.ToString() ?? "Import CSV";

            try
            {
                // Disable the button during import
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "Importing...";
                }

                // Call the importer
                bool imported = CsvImporter.ImportFromCsv();

                if (imported)
                {
                    // Mark sync as required
                    Database.MarkSyncRequired();

                    // Refresh the grid immediately to show imported items
                    Console.WriteLine("[INVENTORY] Refreshing grid after CSV import...");
                    LoadInventoryAsync();

                    // Small delay to let the UI update
                    await Task.Delay(500);

                    // If online, sync in background
                    if (Database.IsInternetAvailable())
                    {
                        if (button != null)
                        {
                            button.Content = "Syncing to cloud...";
                        }

                        // Sync asynchronously
                        await Task.Run(async () =>
                        {
                            try
                            {
                                Console.WriteLine("[INVENTORY] Starting background sync...");
                                await Database.CheckAndSyncAsync();
                                Console.WriteLine("[INVENTORY] Background sync completed");

                                // Refresh again after sync to show any changes from Azure
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    Console.WriteLine("[INVENTORY] Refreshing grid after sync...");
                                    LoadInventoryAsync();
                                });

                                await Dispatcher.InvokeAsync(() =>
                                {
                                    MessageBox.Show("Import and sync completed successfully!",
                                        "Success",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                                });
                            }
                            catch (Exception syncEx)
                            {
                                Console.WriteLine($"[INVENTORY] Background sync failed: {syncEx.Message}");
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    MessageBox.Show($"Import successful but sync failed: {syncEx.Message}\n\nData saved locally and will sync later.",
                                        "Sync Warning",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Warning);
                                });
                            }
                        });
                    }
                    else
                    {
                        MessageBox.Show("Import complete! Data saved locally.\n\nWill sync to cloud when internet is available.",
                            "Success (Offline)",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                else
                {
                    Console.WriteLine("[INVENTORY] Import was cancelled or failed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[INVENTORY] Import error: {ex.Message}");
                MessageBox.Show($"Import error: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable the button
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = originalContent;
                }
            }
        }
        private void DebugLocalItems()
        {
            try
            {
                using var conn = new SqliteConnection("Data Source=ADIX.db");
                conn.Open();

                string query = "SELECT itemID, description, stockQuantity FROM ITEM ORDER BY itemID";
                using var cmd = new SqliteCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                Console.WriteLine("=== LOCAL ITEMS ===");
                while (reader.Read())
                {
                    Console.WriteLine($"ID: {reader["itemID"]}, Name: {reader["description"]}, Stock: {reader["stockQuantity"]}");
                }
                Console.WriteLine($"Total local items: {reader.HasRows}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug error: {ex.Message}");
            }
        }
        private void CheckAzureItems()
        {
            try
            {
                using var conn = new SqlConnection(Database.AzureSqlConnectionString);
                conn.Open();

                string query = "SELECT itemID, description, stockQuantity FROM ITEM ORDER BY itemID";
                using var cmd = new SqlCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                Console.WriteLine("=== AZURE ITEMS ===");
                while (reader.Read())
                {
                    Console.WriteLine($"ID: {reader["itemID"]}, Name: {reader["description"]}, Stock: {reader["stockQuantity"]}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Azure check error: {ex.Message}");
            }
        }

        // Call this in your constructor after LoadInventoryAsync()

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (InventoryGrid.SelectedItem == null)
            {
                MessageBox.Show("Please select an item to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedItem = InventoryGrid.SelectedItem as InventoryItem;
            if (selectedItem == null)
            {
                MessageBox.Show("Invalid item selected.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Debug: Check if ItemID is properly set
            if (selectedItem.ItemID <= 0)
            {
                MessageBox.Show($"Invalid ItemID: {selectedItem.ItemID}. Cannot delete.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete '{selectedItem.ItemName}'?\n\n" +
                $"Item ID: {selectedItem.ItemID}\n" +
                $"SKU: {selectedItem.SKU}\n" +
                $"Current Stock: {selectedItem.BalanceStock}\n\n" +
                "This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                DeleteItemFromDatabase(selectedItem.ItemID, selectedItem.ItemName);
            }
        }
        private async void Inventory_Loaded(object sender, RoutedEventArgs e)
        {
            // Sync when inventory page loads
            if (Database.IsInternetAvailable() && Database.IsSyncRequired())
            {
                try
                {
                    await Database.CheckAndSyncAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Inventory sync failed: {ex.Message}");
                }
            }

            LoadInventoryAsync();
        }
        private void DeleteItemFromDatabase(int itemId, string itemName)
        {
            try
            {
                using var conn = new SqliteConnection("Data Source=ADIX.db");
                conn.Open();

                // Check if item exists
                string checkItemSql = "SELECT COUNT(*) FROM ITEM WHERE itemID = @itemID";
                using var checkItemCmd = new SqliteCommand(checkItemSql, conn);
                checkItemCmd.Parameters.AddWithValue("@itemID", itemId);
                int itemExists = Convert.ToInt32(checkItemCmd.ExecuteScalar());

                if (itemExists == 0)
                {
                    MessageBox.Show("Item not found or already deleted.", "Delete Failed",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Check if item has any invoice records
                string checkInvoicesSql = "SELECT COUNT(*) FROM INVOICEITEM WHERE itemID = @itemID";
                using var checkCmd = new SqliteCommand(checkInvoicesSql, conn);
                checkCmd.Parameters.AddWithValue("@itemID", itemId);
                int invoiceCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (invoiceCount > 0)
                {
                    var confirmResult = MessageBox.Show(
                        $"This item has {invoiceCount} invoice record(s).\n\n" +
                        "Deleting it may affect historical sales data.\n\n" +
                        "Do you still want to delete?",
                        "Warning: Item Has Invoices",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (confirmResult == MessageBoxResult.No)
                        return;
                }

                using var transaction = conn.BeginTransaction();

                try
                {
                    // Delete from INVOICEITEM first (foreign key constraint)
                    if (invoiceCount > 0)
                    {
                        string deleteInvoiceItemsSql = "DELETE FROM INVOICEITEM WHERE itemID = @itemID";
                        using var deleteInvoiceItemsCmd = new SqliteCommand(deleteInvoiceItemsSql, conn, transaction);
                        deleteInvoiceItemsCmd.Parameters.AddWithValue("@itemID", itemId);
                        deleteInvoiceItemsCmd.ExecuteNonQuery();
                    }

                    // Delete the item
                    string deleteItemSql = "DELETE FROM ITEM WHERE itemID = @itemID";
                    using var deleteCmd = new SqliteCommand(deleteItemSql, conn, transaction);
                    deleteCmd.Parameters.AddWithValue("@itemID", itemId);
                    int rowsAffected = deleteCmd.ExecuteNonQuery();

                    // LOG THE DELETION FOR SYNC
                    string logDeletionSql = "INSERT INTO DELETION_LOG (tableName, recordID) VALUES ('ITEM', @itemID)";
                    using var logCmd = new SqliteCommand(logDeletionSql, conn, transaction);
                    logCmd.Parameters.AddWithValue("@itemID", itemId);
                    logCmd.ExecuteNonQuery();

                    transaction.Commit();

                    if (rowsAffected > 0)
                    {
                        MessageBox.Show(
                            $"Item '{itemName}' deleted successfully! Sync will propagate to other devices.",
                            "Delete Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        // Mark sync required
                        Database.MarkSyncRequired();

                        // Refresh the grid
                        LoadInventoryAsync();

                        // Trigger sync if online
                        if (Database.IsInternetAvailable())
                        {
                            Task.Run(async () =>
                            {
                                try
                                {
                                    await Database.CheckAndSyncAsync();
                                }
                                catch (Exception syncEx)
                                {
                                    Console.WriteLine($"Sync after delete failed: {syncEx.Message}");
                                }
                            });
                        }
                    }
                    else
                    {
                        MessageBox.Show("Failed to delete item.", "Delete Failed",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqliteException sqlEx)
            {
                MessageBox.Show($"Database error: {sqlEx.Message}\n\nError Code: {sqlEx.SqliteErrorCode}", "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting item: {ex.Message}", "Delete Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}