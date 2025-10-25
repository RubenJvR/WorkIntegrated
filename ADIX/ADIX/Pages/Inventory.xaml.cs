using ADIX.Components;
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

        public Inventory()
        {
            InitializeComponent();
            LoadInventoryAsync();
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
                stockRecieved
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
                        ReturnedStockUnusable = 0
                    };

                    //Calculations
                    item.BalanceStock = item.OpeningStockQuantity - item.StockSold;
                    item.Loss = item.CostOfBusinessWorkings * item.ReturnedStockUnusable;

                    inventoryList.Add(item);
                }

                InventoryGrid.ItemsSource = inventoryList;
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
