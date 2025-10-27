using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace ADIX
{
    public partial class SupplierPage : Page
    {
        private const string ConnectionString = "Data Source=ADIX.db";

        public SupplierPage()
        {
            InitializeComponent();
            Loaded += SupplierPage_Loaded;
        }

        private void SupplierPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSuppliers();
            LoadSupplierData();
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            Supplier.SelectionChanged += Supplier_SelectionChanged;
            StockType.SelectionChanged += StockType_SelectionChanged;
            PaymentStatus.SelectionChanged += PaymentStatus_SelectionChanged;
            LowStockToggle.Checked += LowStockToggle_Checked;
            LowStockToggle.Unchecked += LowStockToggle_Unchecked;
        }

        private void LoadSuppliers()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                Supplier.Items.Clear();
                Supplier.Items.Add(new ComboBoxItem { Content = "All Suppliers", Tag = "ALL" });

                string sql = "SELECT supplierID, name FROM SUPPLIER ORDER BY name";
                using var cmd = new SqliteCommand(sql, connection);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    Supplier.Items.Add(new ComboBoxItem
                    {
                        Content = reader["name"].ToString(),
                        Tag = reader["supplierID"].ToString()
                    });
                }

                // Select "All Suppliers" by default
                Supplier.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading suppliers: {ex.Message}");
            }
        }

        private void LoadSupplierData()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                var supplierData = new DataTable();

                // Define columns
                supplierData.Columns.Add("Supplier", typeof(string));
                supplierData.Columns.Add("SKU", typeof(string));
                supplierData.Columns.Add("Description", typeof(string));
                supplierData.Columns.Add("StockReceived", typeof(int));
                supplierData.Columns.Add("StockBalance", typeof(int));
                supplierData.Columns.Add("AmountSold", typeof(int));
                supplierData.Columns.Add("CostPrice", typeof(decimal));
                supplierData.Columns.Add("SellingPrice", typeof(decimal));
                supplierData.Columns.Add("TotalSales", typeof(decimal));
                supplierData.Columns.Add("Profit", typeof(decimal));
                supplierData.Columns.Add("Status", typeof(string));
                supplierData.Columns.Add("AmountPaid", typeof(decimal));
                supplierData.Columns.Add("TotalToPay", typeof(decimal));
                supplierData.Columns.Add("InvoiceRef", typeof(string));

                // Build the SQL query with filters
                string sql = BuildFilteredQuery();

                using var cmd = new SqliteCommand(sql, connection);

                // Add parameters for filters
                if (Supplier.SelectedItem is ComboBoxItem supplierItem && supplierItem.Tag?.ToString() != "ALL")
                {
                    cmd.Parameters.AddWithValue("@supplierID", supplierItem.Tag.ToString());
                }

                if (StockType.SelectedItem is ComboBoxItem stockTypeItem)
                {
                    string stockType = stockTypeItem.Content.ToString();
                    if (stockType == "Adix")
                    {
                        // Filter for Adix items (you might need to adjust this logic)
                        // This is just an example - adjust based on your business logic
                    }
                    else if (stockType == "Consignment")
                    {
                        // Filter for Consignment items
                    }
                }

                if (LowStockToggle.IsChecked == true)
                {
                    // Low stock filter - items with less than 10 in stock
                }

                if (PaymentStatus.SelectedItem is ComboBoxItem paymentItem)
                {
                    string paymentStatus = paymentItem.Content.ToString();
                    // Add payment status filtering logic here
                }

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    supplierData.Rows.Add(
                        SafeGetString(reader, "Supplier"),
                        SafeGetString(reader, "SKU"),
                        SafeGetString(reader, "Description"),
                        SafeGetInt(reader, "StockReceived"),
                        SafeGetInt(reader, "StockBalance"),
                        SafeGetInt(reader, "AmountSold"),
                        SafeGetDecimal(reader, "CostPrice"),
                        SafeGetDecimal(reader, "SellingPrice"),
                        SafeGetDecimal(reader, "TotalSales"),
                        SafeGetDecimal(reader, "Profit"),
                        SafeGetString(reader, "Status"),
                        SafeGetDecimal(reader, "AmountPaid"),
                        SafeGetDecimal(reader, "TotalToPay"),
                        SafeGetString(reader, "InvoiceRef")
                    );
                }

                // If no data found, show empty table
                if (supplierData.Rows.Count == 0)
                {
                    supplierData.Rows.Add(
                        "No data found", "", "", 0, 0, 0, 0, 0, 0, 0, "No data", 0, 0, ""
                    );
                }

                SupplierGrid.ItemsSource = supplierData.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading supplier data: {ex.Message}");
            }
        }

        private string BuildFilteredQuery()
        {
            string baseQuery = @"
                SELECT 
                    s.name as Supplier,
                    i.sku as SKU,
                    i.description as Description,
                    i.stockRecieved as StockReceived,
                    i.stockQuantity as StockBalance,
                    i.stockSold as AmountSold,
                    i.costPrice as CostPrice,
                    i.retailPrice as SellingPrice,
                    (i.retailPrice * i.stockSold) as TotalSales,
                    ((i.retailPrice - i.costPrice) * i.stockSold) as Profit,
                    CASE 
                        WHEN i.stockQuantity < 10 THEN 'Low Stock'
                        ELSE 'In Stock'
                    END as Status,
                    (i.costPrice * i.stockRecieved * 0.7) as AmountPaid,
                    (i.costPrice * i.stockRecieved * 0.3) as TotalToPay,
                    'INV-' || s.supplierID || '-' || i.itemID as InvoiceRef
                FROM ITEM i
                INNER JOIN SUPPLIER s ON i.supplierID = s.supplierID
                WHERE 1=1";

            // Add supplier filter
            if (Supplier.SelectedItem is ComboBoxItem supplierItem && supplierItem.Tag?.ToString() != "ALL")
            {
                baseQuery += " AND s.supplierID = @supplierID";
            }

            // Add low stock filter
            if (LowStockToggle.IsChecked == true)
            {
                baseQuery += " AND i.stockQuantity < 10";
            }

            // Add payment status filter (you'll need to adjust this based on your payment tracking)
            if (PaymentStatus.SelectedItem is ComboBoxItem paymentItem)
            {
                string paymentStatus = paymentItem.Content.ToString();
                // Example - adjust based on your actual payment tracking
                if (paymentStatus == "Paid")
                {
                    baseQuery += " AND (i.costPrice * i.stockRecieved * 0.3) <= 0";
                }
                else if (paymentStatus == "Pending")
                {
                    baseQuery += " AND (i.costPrice * i.stockRecieved * 0.3) > 0";
                }
                // Add other payment status conditions as needed
            }

            baseQuery += " ORDER BY s.name, i.description";

            return baseQuery;
        }

        // Helper methods for safe data reading
        private string SafeGetString(SqliteDataReader reader, string column)
        {
            try
            {
                if (reader[column] == DBNull.Value)
                    return "N/A";

                string value = reader[column].ToString();
                return string.IsNullOrEmpty(value) ? "N/A" : value;
            }
            catch
            {
                return "N/A";
            }
        }

        private int SafeGetInt(SqliteDataReader reader, string column)
        {
            try
            {
                return reader[column] != DBNull.Value ? Convert.ToInt32(reader[column]) : 0;
            }
            catch
            {
                return 0;
            }
        }

        private decimal SafeGetDecimal(SqliteDataReader reader, string column)
        {
            try
            {
                return reader[column] != DBNull.Value ? Convert.ToDecimal(reader[column]) : 0m;
            }
            catch
            {
                return 0m;
            }
        }

        private void DateRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateRange.SelectedItem is ComboBoxItem selected)
            {
                string choice = selected.Content.ToString();
                if (choice == "Custom")
                {
                    CustomDatePanel.Visibility = Visibility.Visible;
                }
                else
                {
                    CustomDatePanel.Visibility = Visibility.Collapsed;
                    ApplyFilters();
                }
            }
        }

        private void Supplier_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void StockType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void LowStockToggle_Checked(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void LowStockToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void PaymentStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            LoadSupplierData();
        }

        private void AddSupplierButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string supplierName = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter supplier name:", "Add Supplier", "");

                if (!string.IsNullOrWhiteSpace(supplierName))
                {
                    using var connection = new SqliteConnection(ConnectionString);
                    connection.Open();

                    // Get next supplier ID
                    string maxIdSql = "SELECT COALESCE(MAX(supplierID), 0) + 1 FROM SUPPLIER";
                    int newSupplierId = 1;
                    using (var cmd = new SqliteCommand(maxIdSql, connection))
                    {
                        newSupplierId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    string insertSql = @"
                        INSERT INTO SUPPLIER (supplierID, name, contactInfo, address) 
                        VALUES (@id, @name, @contact, @address)";

                    using var cmd2 = new SqliteCommand(insertSql, connection);
                    cmd2.Parameters.AddWithValue("@id", newSupplierId);
                    cmd2.Parameters.AddWithValue("@name", supplierName.Trim());
                    cmd2.Parameters.AddWithValue("@contact", "");
                    cmd2.Parameters.AddWithValue("@address", "");

                    int rowsAffected = cmd2.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        MessageBox.Show("Supplier added successfully!");

                        // Mark sync required
                        Database.MarkSyncRequired();

                        // Reload data
                        LoadSuppliers();
                        LoadSupplierData();

                        // Sync if online
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
                                    Console.WriteLine($"Sync after add supplier failed: {syncEx.Message}");
                                }
                            });
                        }
                    }
                    else
                    {
                        MessageBox.Show("Failed to add supplier.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding supplier: {ex.Message}");
            }
        }

        private void EditSupplierButton_Click(object sender, RoutedEventArgs e)
        {
            if (SupplierGrid.SelectedItem != null)
            {
                var selectedRow = (DataRowView)SupplierGrid.SelectedItem;
                string supplierName = selectedRow["Supplier"].ToString();

                // Don't allow editing of placeholder rows
                if (supplierName == "No data found" || supplierName == "No suppliers found")
                {
                    MessageBox.Show("Please select a valid supplier to edit.");
                    return;
                }

                string newName = Microsoft.VisualBasic.Interaction.InputBox(
                    "Edit supplier name:", "Edit Supplier", supplierName);

                if (!string.IsNullOrWhiteSpace(newName) && newName != supplierName)
                {
                    try
                    {
                        using var connection = new SqliteConnection(ConnectionString);
                        connection.Open();

                        string updateSql = "UPDATE SUPPLIER SET name = @newName WHERE name = @oldName";
                        using var cmd = new SqliteCommand(updateSql, connection);
                        cmd.Parameters.AddWithValue("@newName", newName.Trim());
                        cmd.Parameters.AddWithValue("@oldName", supplierName);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Supplier updated successfully!");

                            // Mark sync required
                            Database.MarkSyncRequired();

                            LoadSuppliers();
                            LoadSupplierData();
                        }
                        else
                        {
                            MessageBox.Show("Supplier not found or no changes made.");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error updating supplier: {ex.Message}");
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a supplier to edit.");
            }
        }

        private void DeleteSupplierButton_Click(object sender, RoutedEventArgs e)
        {
            if (SupplierGrid.SelectedItem != null)
            {
                var selectedRow = (DataRowView)SupplierGrid.SelectedItem;
                string supplierName = selectedRow["Supplier"].ToString();

                // Don't allow deletion of placeholder rows
                if (supplierName == "No data found" || supplierName == "No suppliers found")
                {
                    MessageBox.Show("Please select a valid supplier to delete.");
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to delete supplier '{supplierName}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var connection = new SqliteConnection(ConnectionString);
                        connection.Open();

                        // First check if supplier has items
                        string checkItemsSql = @"
                            SELECT COUNT(*) FROM ITEM 
                            WHERE supplierID IN (
                                SELECT supplierID FROM SUPPLIER WHERE name = @name
                            )";
                        using (var checkCmd = new SqliteCommand(checkItemsSql, connection))
                        {
                            checkCmd.Parameters.AddWithValue("@name", supplierName);
                            int itemCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                            if (itemCount > 0)
                            {
                                MessageBox.Show(
                                    "Cannot delete supplier with existing items. " +
                                    "Please reassign or delete items first.");
                                return;
                            }
                        }

                        string deleteSql = "DELETE FROM SUPPLIER WHERE name = @name";
                        using var cmd = new SqliteCommand(deleteSql, connection);
                        cmd.Parameters.AddWithValue("@name", supplierName);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Supplier deleted successfully!");

                            // Mark sync required
                            Database.MarkSyncRequired();

                            LoadSuppliers();
                            LoadSupplierData();
                        }
                        else
                        {
                            MessageBox.Show("Supplier not found.");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting supplier: {ex.Message}");
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a supplier to delete.");
            }
        }
    }
}