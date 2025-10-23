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

        }

    }
}
