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

namespace ADIX.Components
{
    public static class CsvImporter
    {
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

        public static void ImportFromCsv()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    Title = "Select CSV File to Import"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var filePath = openFileDialog.FileName;
                    var products = ParseCsv(filePath);

                    if (products.Count == 0)
                    {
                        MessageBox.Show("No valid products found in CSV file.", "Import Warning",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var result = MessageBox.Show(
                        $"Found {products.Count} products to import.\n\nDo you want to proceed?",
                        "Confirm Import",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        int imported = ImportProductsToDatabase(products);
                        MessageBox.Show($"Successfully imported {imported} products!\n\n" +
                            (Database.IsInternetAvailable() ? "Syncing to cloud..." : "Offline - will sync when online"),
                            "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                        if (Database.IsInternetAvailable())
                        {
                            System.Threading.Tasks.Task.Run(async () => { await Database.CheckAndSyncAsync(); });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing CSV: {ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static List<Product> ParseCsv(string filePath)
        {
            var parsedList = new List<Product>();
            var lines = File.ReadAllLines(filePath);
            if (lines.Length == 0) return parsedList;

            bool hasHeader = lines[0].ToLower().Contains("description") || lines[0].ToLower().Contains("sku");
            int startLine = hasHeader ? 1 : 0;

            for (int i = startLine; i < lines.Length; i++)
            {
                try
                {
                    var values = lines[i].Split(',').Select(v => v.Trim('"', ' ')).ToArray();

                    // Expected order:
                    // SKU, ItemGroup, Description, RetailPrice, CostPrice, StockQuantity, StockReceived, SupplierID, SellerID
                    if (values.Length < 6)
                    {
                        Console.WriteLine($"Skipping line {i + 1}: Not enough columns");
                        continue;
                    }

                    var product = new Product
                    {
                        SKU = values.Length > 0 ? values[0] : string.Empty,
                        ItemGroup = values.Length > 1 ? values[1] : string.Empty,
                        Description = values.Length > 2 ? values[2] : string.Empty,
                        RetailPrice = ParseDecimal(values.Length > 3 ? values[3] : "0"),
                        CostPrice = ParseDecimal(values.Length > 4 ? values[4] : "0"),
                        StockQuantity = ParseInt(values.Length > 5 ? values[5] : "0"),
                        StockRecieved = values.Length > 6 ? values[6] : DateTime.Now.ToString("yyyy-MM-dd"),
                        StockSold = 0,
                        SupplierID = values.Length > 7 ? ParseInt(values[7]) : 1,
                        SellerID = values.Length > 8 ? ParseInt(values[8]) : 1
                    };

                    if (!string.IsNullOrWhiteSpace(product.Description) && product.RetailPrice > 0)
                        parsedList.Add(product);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing line {i + 1}: {ex.Message}");
                }
            }
            return parsedList;
        }

        private static decimal ParseDecimal(string value)
        {
            value = value.Trim().Trim('"');
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
        }

        private static int ParseInt(string value)
        {
            value = value.Trim().Trim('"');
            return int.TryParse(value, out var result) ? result : 0;
        }

        private static int ImportProductsToDatabase(List<Product> products)
        {
            int importedCount = 0;
            using var conn = new SqliteConnection("Data Source=ADIX.db");
            conn.Open();
            using var transaction = conn.BeginTransaction();

            try
            {
                foreach (var product in products)
                {
                    var checkCmd = new SqliteCommand("SELECT COUNT(*) FROM ITEM WHERE description = @desc", conn, transaction);
                    checkCmd.Parameters.AddWithValue("@desc", product.Description);
                    var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

                    if (exists)
                    {
                        Console.WriteLine($"Product '{product.Description}' already exists, skipping...");
                        continue;
                    }

                    var insertCmd = new SqliteCommand(@"
                        INSERT INTO ITEM 
                        (SKU, itemGroup, description, retailPrice, costPrice, stockQuantity, stockRecieved, stockSold, supplierID, sellerID, lastModified)
                        VALUES 
                        (@sku, @group, @desc, @retail, @cost, @qty, @received, @sold, @supplier, @seller, CURRENT_TIMESTAMP)", conn, transaction);

                    insertCmd.Parameters.AddWithValue("@sku", product.SKU ?? "");
                    insertCmd.Parameters.AddWithValue("@group", product.ItemGroup ?? "");
                    insertCmd.Parameters.AddWithValue("@desc", product.Description);
                    insertCmd.Parameters.AddWithValue("@retail", (double)product.RetailPrice);
                    insertCmd.Parameters.AddWithValue("@cost", (double)product.CostPrice);
                    insertCmd.Parameters.AddWithValue("@qty", product.StockQuantity);
                    insertCmd.Parameters.AddWithValue("@received", product.StockRecieved ?? DateTime.Now.ToString("yyyy-MM-dd"));
                    insertCmd.Parameters.AddWithValue("@sold", product.StockSold);
                    insertCmd.Parameters.AddWithValue("@supplier", product.SupplierID);
                    insertCmd.Parameters.AddWithValue("@seller", product.SellerID);
                    insertCmd.ExecuteNonQuery();
                    importedCount++;
                }

                transaction.Commit();
                Database.MarkSyncRequired();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new Exception($"Database error during import: {ex.Message}", ex);
            }

            return importedCount;
        }
    }
}
    

