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

            // Backward compatible properties
            public int SupplierID { get; set; } = 1;
            public int SellerID { get; set; } = 1;

            // New properties for supplier names
            public string SupplierName { get; set; }
            public string SellerName { get; set; }

            // Helper property to determine which to use
            public bool HasSupplierName => !string.IsNullOrWhiteSpace(SupplierName);
            public bool HasSellerName => !string.IsNullOrWhiteSpace(SellerName);
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
                            Task.Run(async () => { await Database.CheckAndSyncAsync(); });
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
                    // SKU, ItemGroup, Description, RetailPrice, CostPrice, StockQuantity, StockReceived, Supplier, Seller
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
                        StockSold = 0
                    };

                    // OPTION 1: SMART SUPPLIER DETECTION - Check if column 7 is a number or text
                    if (values.Length > 7)
                    {
                        string supplierValue = values[7];

                        // Try to parse as number first (backward compatibility)
                        if (int.TryParse(supplierValue, out int supplierId))
                        {
                            product.SupplierID = supplierId;
                            Console.WriteLine($"Line {i + 1}: Using numeric SupplierID: {supplierId}");
                        }
                        else
                        {
                            // It's a supplier name
                            product.SupplierName = supplierValue;
                            Console.WriteLine($"Line {i + 1}: Using supplier name: {supplierValue}");
                        }
                    }
                    else
                    {
                        product.SupplierID = 1; // Default supplier
                    }

                    // OPTION 1: SMART SELLER DETECTION - Check if column 8 is a number or text
                    if (values.Length > 8)
                    {
                        string sellerValue = values[8];

                        // Try to parse as number first (backward compatibility)
                        if (int.TryParse(sellerValue, out int sellerId))
                        {
                            product.SellerID = sellerId;
                            Console.WriteLine($"Line {i + 1}: Using numeric SellerID: {sellerId}");
                        }
                        else
                        {
                            // It's a seller name
                            product.SellerName = sellerValue;
                            Console.WriteLine($"Line {i + 1}: Using seller name: {sellerValue}");
                        }
                    }
                    else
                    {
                        product.SellerID = 1; // Default seller
                    }

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

        // OPTION 3: UPDATED IMPORT LOGIC
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
                    // OPTION 3: DETERMINE SUPPLIER ID
                    int finalSupplierId;
                    if (product.HasSupplierName)
                    {
                        // Use supplier name to get/create supplier
                        finalSupplierId = GetOrCreateSupplier(conn, transaction, product.SupplierName);
                        Console.WriteLine($"Using supplier '{product.SupplierName}' with ID: {finalSupplierId}");
                    }
                    else
                    {
                        // Use existing SupplierID (backward compatible)
                        finalSupplierId = product.SupplierID;
                        Console.WriteLine($"Using numeric SupplierID: {finalSupplierId}");
                    }

                    // OPTION 3: DETERMINE SELLER ID
                    int finalSellerId;
                    if (product.HasSellerName)
                    {
                        // Use seller name to get/create seller
                        finalSellerId = GetOrCreateSeller(conn, transaction, product.SellerName);
                        Console.WriteLine($"Using seller '{product.SellerName}' with ID: {finalSellerId}");
                    }
                    else
                    {
                        // Use existing SellerID (backward compatible)
                        finalSellerId = product.SellerID;
                        Console.WriteLine($"Using numeric SellerID: {finalSellerId}");
                    }

                    // Check by description instead of exact match
                    var checkCmd = new SqliteCommand(
                        "SELECT itemID FROM ITEM WHERE LOWER(description) = LOWER(@desc)",
                        conn, transaction);
                    checkCmd.Parameters.AddWithValue("@desc", product.Description);
                    var existingId = checkCmd.ExecuteScalar();

                    if (existingId != null)
                    {
                        Console.WriteLine($"Product '{product.Description}' already exists with ID {existingId}, skipping...");
                        continue;
                    }

                    // Generate unique timestamp-based ID
                    long newItemId = Database.GetNextItemID();
                    System.Threading.Thread.Sleep(1); // Ensure uniqueness

                    var insertCmd = new SqliteCommand(@"
                INSERT INTO ITEM 
                (itemID, SKU, itemGroup, description, retailPrice, costPrice, stockQuantity, stockRecieved, stockSold, supplierID, sellerID, lastModified)
                VALUES 
                (@itemID, @sku, @group, @desc, @retail, @cost, @qty, @received, @sold, @supplierID, @sellerID, CURRENT_TIMESTAMP)",
                        conn, transaction);

                    insertCmd.Parameters.AddWithValue("@itemID", newItemId);
                    insertCmd.Parameters.AddWithValue("@sku", product.SKU ?? "");
                    insertCmd.Parameters.AddWithValue("@group", product.ItemGroup ?? "");
                    insertCmd.Parameters.AddWithValue("@desc", product.Description);
                    insertCmd.Parameters.AddWithValue("@retail", (double)product.RetailPrice);
                    insertCmd.Parameters.AddWithValue("@cost", (double)product.CostPrice);
                    insertCmd.Parameters.AddWithValue("@qty", product.StockQuantity);
                    insertCmd.Parameters.AddWithValue("@received", product.StockRecieved ?? DateTime.Now.ToString("yyyy-MM-dd"));
                    insertCmd.Parameters.AddWithValue("@sold", product.StockSold);
                    insertCmd.Parameters.AddWithValue("@supplierID", finalSupplierId);
                    insertCmd.Parameters.AddWithValue("@sellerID", finalSellerId);
                    insertCmd.ExecuteNonQuery();

                    Console.WriteLine($"Imported item '{product.Description}' with ID {newItemId}, SupplierID: {finalSupplierId}, SellerID: {finalSellerId}");
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

        private static int GetOrCreateSupplier(SqliteConnection conn, SqliteTransaction transaction, string supplierName)
        {
            // Check if supplier exists
            var checkCmd = new SqliteCommand(
                "SELECT supplierID FROM SUPPLIER WHERE LOWER(name) = LOWER(@name)",
                conn, transaction);
            checkCmd.Parameters.AddWithValue("@name", supplierName);
            var existingId = checkCmd.ExecuteScalar();

            if (existingId != null)
                return Convert.ToInt32(existingId);

            // Create new supplier
            var maxIdCmd = new SqliteCommand("SELECT COALESCE(MAX(supplierID), 0) + 1 FROM SUPPLIER", conn, transaction);
            int newSupplierId = Convert.ToInt32(maxIdCmd.ExecuteScalar());

            var insertCmd = new SqliteCommand(
                "INSERT INTO SUPPLIER (supplierID, name, lastModified) VALUES (@id, @name, CURRENT_TIMESTAMP)",
                conn, transaction);
            insertCmd.Parameters.AddWithValue("@id", newSupplierId);
            insertCmd.Parameters.AddWithValue("@name", supplierName);
            insertCmd.ExecuteNonQuery();

            Console.WriteLine($"Created new supplier: '{supplierName}' with ID: {newSupplierId}");
            return newSupplierId;
        }

        private static int GetOrCreateSeller(SqliteConnection conn, SqliteTransaction transaction, string sellerName)
        {
            // Check if seller exists
            var checkCmd = new SqliteCommand(
                "SELECT sellerID FROM SELLER WHERE LOWER(name) = LOWER(@name)",
                conn, transaction);
            checkCmd.Parameters.AddWithValue("@name", sellerName);
            var existingId = checkCmd.ExecuteScalar();

            if (existingId != null)
                return Convert.ToInt32(existingId);

            // Create new seller
            var maxIdCmd = new SqliteCommand("SELECT COALESCE(MAX(sellerID), 0) + 1 FROM SELLER", conn, transaction);
            int newSellerId = Convert.ToInt32(maxIdCmd.ExecuteScalar());

            var insertCmd = new SqliteCommand(
                "INSERT INTO SELLER (sellerID, name, lastModified) VALUES (@id, @name, CURRENT_TIMESTAMP)",
                conn, transaction);
            insertCmd.Parameters.AddWithValue("@id", newSellerId);
            insertCmd.Parameters.AddWithValue("@name", sellerName);
            insertCmd.ExecuteNonQuery();

            Console.WriteLine($"Created new seller: '{sellerName}' with ID: {newSellerId}");
            return newSellerId;
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
    }
}