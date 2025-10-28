using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WorkIntegrated;
using System.Globalization;
namespace ADIX
{
    public partial class Products : Page
    {
        public Products()
        {
            InitializeComponent();
            LoadItem();
        }

        public class Product
        {
            public int ItemID { get; set; }
            public string Description { get; set; }
            public decimal RetailPrice { get; set; }
            public decimal CostPrice { get; set; }
            public int StockQuantity { get; set; }
            public int StockSold { get; set; }
            public int SupplierID { get; set; }
            public int SellerID { get; set; }
        }

        private void LoadItem()
        {
            try
            {
                using var connection = new SqliteConnection("Data Source=ADIX.db");
                connection.Open();

                string query = @"SELECT itemID, description, retailPrice, costPrice, stockQuantity, 
                        supplierID, sellerID, minimumStock FROM ITEM";

                using var cmd = new SqliteCommand(query, connection);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    try
                    {
                        // Your original code here - wrap each field access if needed
                        int itemId = Convert.ToInt32(reader["itemID"]);
                        // ... rest of your field accesses
                    }
                    catch (InvalidCastException nullEx)
                    {
                        // Skip this row if there are null values
                        Console.WriteLine($"Skipping row due to null value: {nullEx.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading items: {ex.Message}");
            }
        }

        private void ImportCSV_Click(object sender, EventArgs e)
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
                    var products = ParseCSV(filePath);

                    if (products.Count == 0)
                    {
                        MessageBox.Show("No valid products found in CSV file.", "Import Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var result = MessageBox.Show($"Found {products.Count} products to import.\n\nDo you want to proceed?", "Confirm Import", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        int imported = ImportProductsToDatabase(products);
                        MessageBox.Show($"Successfully imported {imported} products!\n\n" + (Database.IsInternetAvailable() ? "Syncing to cloud..." : "Offline - will sync when online"), "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadItem();
                        if (Database.IsInternetAvailable())
                        {
                            System.Threading.Tasks.Task.Run(async () => { await Database.CheckAndSyncAsync(); });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing CSV: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<Product> ParseCSV(string filePath)
        {
            var parsedList = new List<Product>();
            var lines = File.ReadAllLines(filePath);
            if (lines.Length == 0) return parsedList;

            bool hasHeader = lines[0].ToLower().Contains("description") || lines[0].ToLower().Contains("Products");
            int startLine = hasHeader ? 1 : 0;

            for (int i = startLine; i < lines.Length; i++)
            {
                try
                {
                    var values = lines[i].Split(',').Select(v => v.Trim('"', ' ')).ToArray();
                    if (values.Length < 6)
                    {
                        Console.WriteLine($"Skipping line {i + 1}: Not Enough columns");
                        continue;
                    }

                    var product = new Product
                    {
                        Description = values[0].Trim().Trim('"'),
                        RetailPrice = ParseDecimal(values[1]),
                        CostPrice = ParseDecimal(values[2]),
                        StockQuantity = ParseInt(values[3]),
                        StockSold = 0,
                        SupplierID = values.Length > 4 ? ParseInt(values[4]) : 1,
                        SellerID = values.Length > 5 ? ParseInt(values[5]) : 1
                    };

                    if (!string.IsNullOrWhiteSpace(product.Description) && product.RetailPrice > 0)
                    {
                        parsedList.Add(product);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing line {i + 1}: {ex.Message}");
                }
            }
            return parsedList;
        }

        private decimal ParseDecimal(string value)
        {
            value = value.Trim().Trim('"');

            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                return result;

            return 0;
        }

        private int ParseInt(string value)
        {
            value = value.Trim().Trim('"');
            if (int.TryParse(value, out int result)) return result;
            return 0;
        }

        private int ImportProductsToDatabase(List<Product> products)
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

                    var insertCmd = new SqliteCommand(@"INSERT INTO ITEM (description, retailPrice, costPrice, stockQuantity, stockSold, supplierID, sellerID, lastModified)
                        VALUES (@desc, @retail, @cost, @qty, @sold, @supplier, @seller, CURRENT_TIMESTAMP)", conn, transaction);

                    insertCmd.Parameters.AddWithValue("@desc", product.Description);
                    insertCmd.Parameters.AddWithValue("@retail", (double)product.RetailPrice);
                    insertCmd.Parameters.AddWithValue("@cost", (double)product.CostPrice);
                    insertCmd.Parameters.AddWithValue("@qty", product.StockQuantity);
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