using ADIX.Components;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
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

namespace ADIX
{
    /// <summary>
    /// Interaction logic for Inventory.xaml
    /// </summary>
    public partial class Inventory : Page
    {
        private const string ConnStr = "Data Source=ADIX.db";
        private System.Windows.Threading.DispatcherTimer _inventoryRefreshTimer;
        private List<InventoryItem> _allInventoryItems;
        private string _selectedItemGroup = "";
        private string _selectedItemName = "";
        private bool _showLowStockOnly = false;

        public Inventory()
        {
            InitializeComponent();

            // Attach event handlers
            ItemGroup.SelectionChanged += ItemGroup_SelectionChanged;
            ItemName.SelectionChanged += ItemName_SelectionChanged;
            LowStockToggle.Checked += LowStockToggle_Checked;
            LowStockToggle.Unchecked += LowStockToggle_Unchecked;

            LoadInventoryAsync();

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

        private void ItemGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ItemGroup.SelectedItem == null) return;

            var selected = ItemGroup.SelectedItem.ToString();
            _selectedItemGroup = (selected == "Show all Item Group") ? "" : selected;
            ApplyFilters();
        }

        private void ItemName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ItemName.SelectedItem == null) return;

            var selected = ItemName.SelectedItem.ToString();
            _selectedItemName = (selected == "Show all Item Name") ? "" : selected;
            ApplyFilters();
        }

        private void LowStockToggle_Checked(object sender, RoutedEventArgs e)
        {
            _showLowStockOnly = true;
            ApplyFilters();
        }

        private void LowStockToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _showLowStockOnly = false;
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_allInventoryItems == null) return;

            var filteredItems = _allInventoryItems.AsEnumerable();

            // Apply Item Group filter (only if not "Show all")
            if (!string.IsNullOrEmpty(_selectedItemGroup))
            {
                filteredItems = filteredItems.Where(item =>
                    item.ItemGroup == _selectedItemGroup);
            }

            // Apply Item Name filter (only if not "Show all")
            if (!string.IsNullOrEmpty(_selectedItemName))
            {
                filteredItems = filteredItems.Where(item =>
                    item.ItemName == _selectedItemName);
            }

            // Apply Low Stock filter
            if (_showLowStockOnly)
            {
                filteredItems = filteredItems.Where(item =>
                    item.BalanceStock <= item.MinimumStock);
            }

            InventoryGrid.ItemsSource = filteredItems.ToList();

            // Update filter status
            UpdateFilterStatus();
        }

        private void PopulateFilterDropdowns()
        {
            if (_allInventoryItems == null) return;

            // Store current selections to restore after refresh
            string currentGroup = _selectedItemGroup;
            string currentName = _selectedItemName;

            // Populate Item Group filter
            var itemGroups = _allInventoryItems
                .Select(i => i.ItemGroup)
                .Where(g => !string.IsNullOrEmpty(g))
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            ItemGroup.Items.Clear();
            ItemGroup.Items.Add("Show all Item Group"); // Default "all" option
            foreach (var group in itemGroups)
            {
                ItemGroup.Items.Add(group);
            }

            // Restore or set default selection for Item Group
            if (!string.IsNullOrEmpty(currentGroup) && ItemGroup.Items.Contains(currentGroup))
            {
                ItemGroup.SelectedItem = currentGroup;
            }
            else
            {
                ItemGroup.SelectedIndex = 0; // Select "Show all Item Group"
                _selectedItemGroup = "";
            }

            // Populate Item Name filter
            var itemNames = _allInventoryItems
                .Select(i => i.ItemName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            ItemName.Items.Clear();
            ItemName.Items.Add("Show all Item Name"); // Default "all" option
            foreach (var name in itemNames)
            {
                ItemName.Items.Add(name);
            }

            // Restore or set default selection for Item Name
            if (!string.IsNullOrEmpty(currentName) && ItemName.Items.Contains(currentName))
            {
                ItemName.SelectedItem = currentName;
            }
            else
            {
                ItemName.SelectedIndex = 0; // Select "Show all Item Name"
                _selectedItemName = "";
            }
        }

        private void UpdateFilterStatus()
        {
            int totalItems = _allInventoryItems?.Count ?? 0;
            int filteredItems = (InventoryGrid.ItemsSource as IEnumerable<object>)?.Count() ?? 0;

            // You can add this to a status text block if you want
            // FilterStatusText.Text = $"Showing {filteredItems} of {totalItems} items";

            Console.WriteLine($"Filtered: {filteredItems} of {totalItems} items");
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            // Reset filters
            _selectedItemGroup = "";
            _selectedItemName = "";
            _showLowStockOnly = false;

            // Reset UI controls to initial state
            ItemGroup.SelectedIndex = 0; // "Show all Item Group"
            ItemName.SelectedIndex = 0;  // "Show all Item Name"
            LowStockToggle.IsChecked = false;

            // Apply filters (which will show all items)
            ApplyFilters();
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
                    LoadInventoryAsync(); // Refresh the grid
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

                    // Calculations
                    item.BalanceStock = item.OpeningStockQuantity - item.StockSold;
                    item.Loss = item.CostOfBusinessWorkings * item.ReturnedStockUnusable;

                    inventoryList.Add(item);
                }

                _allInventoryItems = inventoryList;

                // Populate filter dropdowns first
                PopulateFilterDropdowns();

                // Then apply filters
                ApplyFilters();
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

        private async Task UpdateMinimumStockInDB(InventoryItem item)
        {
            try
            {
                using var conn = new SqliteConnection(ConnStr);
                await conn.OpenAsync();

                string updateSql =
                    "UPDATE ITEM SET minimumStock = @min WHERE itemID = @id";

                using var cmd = new SqliteCommand(updateSql, conn);
                cmd.Parameters.AddWithValue("@min", item.MinimumStock);
                cmd.Parameters.AddWithValue("@id", item.ItemID);

                await cmd.ExecuteNonQueryAsync();
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
        }

        private void ImportCSV_Click(object sender, EventArgs e)
        {
            CsvImporter.ImportFromCsv();
            Database.MarkSyncRequired();
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

                    transaction.Commit();

                    if (rowsAffected > 0)
                    {
                        MessageBox.Show(
                            $"Item '{itemName}' deleted successfully!",
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