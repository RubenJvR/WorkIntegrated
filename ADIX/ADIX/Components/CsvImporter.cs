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
            public int StockRecieved { get; set; }
            public int StockSold { get; set; }
            public int SupplierID { get; set; }
            public int SellerID { get; set; }
        }

        public static bool ImportFromCsv()
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
                        return false;
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

                        return true; // Import successful
                    }
                }

                return false; // User cancelled
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing CSV: {ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
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
                        StockRecieved = ParseInt(values.Length > 6 ? values[6] : "0"),
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
            int updatedCount = 0;
            using var conn = new SqliteConnection("Data Source=ADIX.db");
            conn.Open();
            using var transaction = conn.BeginTransaction();

            try
            {
                foreach (var product in products)
                {
                    bool itemExists = false;
                    int existingId = 0;
                    int currentQuantity = 0;
                    int currentStockReceived = 0;

                    // Check by SKU first
                    if (!string.IsNullOrWhiteSpace(product.SKU))
                    {
                        var checkBySkuCmd = new SqliteCommand(
                            @"SELECT itemID, stockQuantity, stockRecieved FROM ITEM 
                              WHERE sku = @sku",
                            conn, transaction);
                        checkBySkuCmd.Parameters.AddWithValue("@sku", product.SKU);

                        using var skuReader = checkBySkuCmd.ExecuteReader();
                        if (skuReader.Read())
                        {
                            itemExists = true;
                            existingId = skuReader.GetInt32(0);
                            currentQuantity = skuReader.GetInt32(1);
                            currentStockReceived = skuReader.GetInt32(2);
                            skuReader.Close();
                        }
                        else
                        {
                            skuReader.Close();
                        }
                    }

                    // Check by supplier AND description if no SKU match
                    if (!itemExists)
                    {
                        var checkCmd = new SqliteCommand(
                            @"SELECT itemID, stockQuantity, stockRecieved FROM ITEM 
                              WHERE supplierID = @supplierID 
                              AND LOWER(description) = LOWER(@desc)",
                            conn, transaction);
                        checkCmd.Parameters.AddWithValue("@supplierID", product.SupplierID);
                        checkCmd.Parameters.AddWithValue("@desc", product.Description);

                        using var reader = checkCmd.ExecuteReader();
                        if (reader.Read())
                        {
                            itemExists = true;
                            existingId = reader.GetInt32(0);
                            currentQuantity = reader.GetInt32(1);
                            currentStockReceived = reader.GetInt32(2);
                        }
                        reader.Close();
                    }

                    if (itemExists)
                    {
                        // FIXED: Update stockRecieved to track actual received stock
                        // The quantity field in CSV represents NEW stock being added
                        var updateCmd = new SqliteCommand(
                            @"UPDATE ITEM 
                              SET stockQuantity = stockQuantity + @newQuantity,
                                  stockRecieved = stockRecieved + @newQuantity,
                                  retailPrice = @retailPrice,
                                  costPrice = @costPrice,
                                  itemGroup = COALESCE(@itemGroup, itemGroup),
                                  lastModified = CURRENT_TIMESTAMP
                              WHERE itemID = @itemID",
                            conn, transaction);

                        // Use StockQuantity from CSV as the amount being added
                        int quantityToAdd = product.StockQuantity;

                        updateCmd.Parameters.AddWithValue("@newQuantity", quantityToAdd);
                        updateCmd.Parameters.AddWithValue("@retailPrice", (double)product.RetailPrice);
                        updateCmd.Parameters.AddWithValue("@costPrice", (double)product.CostPrice);
                        updateCmd.Parameters.AddWithValue("@itemGroup",
                            string.IsNullOrWhiteSpace(product.ItemGroup) ? (object)DBNull.Value : product.ItemGroup);
                        updateCmd.Parameters.AddWithValue("@itemID", existingId);

                        updateCmd.ExecuteNonQuery();

                        Console.WriteLine($"Updated existing item '{product.Description}' (ID: {existingId})");
                        Console.WriteLine($"  Added {quantityToAdd} units");
                        Console.WriteLine($"  New stockQuantity: {currentQuantity + quantityToAdd}");
                        Console.WriteLine($"  New stockRecieved: {currentStockReceived + quantityToAdd}");

                        updatedCount++;
                    }
                    else
                    {
                        // New item - create with timestamp-based ID
                        long newItemId = Database.GetNextItemID();
                        System.Threading.Thread.Sleep(1);

                        // For new items, stockRecieved should equal initial stockQuantity
                        var insertCmd = new SqliteCommand(@"
                            INSERT INTO ITEM 
                            (itemID, SKU, itemGroup, description, retailPrice, costPrice, 
                             stockQuantity, stockRecieved, stockSold, supplierID, sellerID, 
                             lastModified, minimumStock)
                            VALUES 
                            (@itemID, @sku, @group, @desc, @retail, @cost, 
                             @qty, @qty, @sold, @supplier, @seller, 
                             CURRENT_TIMESTAMP, @minStock)",
                            conn, transaction);

                        insertCmd.Parameters.AddWithValue("@itemID", newItemId);
                        insertCmd.Parameters.AddWithValue("@sku", product.SKU ?? "");
                        insertCmd.Parameters.AddWithValue("@group", product.ItemGroup ?? "");
                        insertCmd.Parameters.AddWithValue("@desc", product.Description);
                        insertCmd.Parameters.AddWithValue("@retail", (double)product.RetailPrice);
                        insertCmd.Parameters.AddWithValue("@cost", (double)product.CostPrice);
                        insertCmd.Parameters.AddWithValue("@qty", product.StockQuantity);
                        insertCmd.Parameters.AddWithValue("@sold", 0);
                        insertCmd.Parameters.AddWithValue("@supplier", product.SupplierID);
                        insertCmd.Parameters.AddWithValue("@seller", product.SellerID);
                        insertCmd.Parameters.AddWithValue("@minStock", 0);

                        insertCmd.ExecuteNonQuery();

                        Console.WriteLine($"Imported new item '{product.Description}' with ID {newItemId}");
                        Console.WriteLine($"  Initial stock: {product.StockQuantity}");
                        Console.WriteLine($"  stockRecieved: {product.StockQuantity}");

                        importedCount++;
                    }
                }

                transaction.Commit();
                Database.MarkSyncRequired();

                Console.WriteLine($"\nImport Summary:");
                Console.WriteLine($"  New items: {importedCount}");
                Console.WriteLine($"  Updated items: {updatedCount}");
                Console.WriteLine($"  Total: {importedCount + updatedCount}");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new Exception($"Database error during import: {ex.Message}", ex);
            }

            return importedCount + updatedCount;
        }
    }
}