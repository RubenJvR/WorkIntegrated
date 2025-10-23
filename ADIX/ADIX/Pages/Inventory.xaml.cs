using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ADIX
{
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

                // Get item data
                string query = @"
                    SELECT 
                        itemID,
                        description,
                        costPrice,
                        retailPrice,
                        stockQuantity,
                        stockSold
                    FROM ITEM;
                ";

                using var cmd = new SqliteCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                var inventoryList = new List<InventoryItem>();

                while (await reader.ReadAsync())
                {
                    var item = new InventoryItem
                    {
                        ItemGroup = "New", 
                        ItemName = reader["description"]?.ToString() ?? "Unknown",
                        SKU = $"SKU-{reader["itemID"]}",
                        OpeningStockQuantity = Convert.ToInt32(reader["stockQuantity"]),
                        StockSold = Convert.ToInt32(reader["stockSold"]),
                        StockReceived = 0, 
                        StockReturned = 0,
                        StockRefunded = 0,
                        CostOfBusinessWorkings = Convert.ToDouble(reader["costPrice"]),
                        ReturnedStockUnusable = 0
                    };

                    item.BalanceStock = item.OpeningStockQuantity - item.StockSold;
                    item.Loss = item.CostOfBusinessWorkings * item.ReturnedStockUnusable;

                    inventoryList.Add(item);
                }

                InventoryGrid.ItemsSource = inventoryList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading inventory: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

   
}
