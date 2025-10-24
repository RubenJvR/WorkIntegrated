using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

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
        }

        private void LoadSuppliers()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                Supplier.Items.Clear();
                Supplier.Items.Add(new ComboBoxItem { Content = "All Suppliers", IsSelected = true });

                string sql = "SELECT supplierID, name FROM SUPPLIER ORDER BY name";
                using var cmd = new SqliteCommand(sql, connection);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    Supplier.Items.Add(new ComboBoxItem
                    {
                        Content = reader["name"].ToString(),
                        Tag = reader["supplierID"] // Store ID for filtering
                    });
                }
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

                // Get actual supplier data from database
                string sql = @"
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
                    ORDER BY s.name, i.description";

                using var cmd = new SqliteCommand(sql, connection);
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

                // If no data found, show empty table with message
                if (supplierData.Rows.Count == 0)
                {
                    supplierData.Rows.Add(
                        "No suppliers found", "N/A", "N/A", 0, 0, 0, 0, 0, 0, 0, "N/A", 0, 0, "N/A"
                    );
                }

                SupplierGrid.ItemsSource = supplierData.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading supplier data: {ex.Message}\n\nPlease check if the database has been properly initialized with suppliers and items.");
            }
        }

        // Helper methods for safe data reading
        private string SafeGetString(SqliteDataReader reader, string column)
        {
            try
            {
                return reader[column] != DBNull.Value ? reader[column].ToString() : "N/A";
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
            // For now, just reload data - you can implement more sophisticated filtering later
            LoadSupplierData();
        }

        private void AddSupplierButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Simple add supplier implementation
                string supplierName = Microsoft.VisualBasic.Interaction.InputBox("Enter supplier name:", "Add Supplier", "");

                if (!string.IsNullOrEmpty(supplierName))
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
                    cmd2.Parameters.AddWithValue("@name", supplierName);
                    cmd2.Parameters.AddWithValue("@contact", "");
                    cmd2.Parameters.AddWithValue("@address", "");
                    cmd2.ExecuteNonQuery();

                    MessageBox.Show("Supplier added successfully!");
                    LoadSuppliers();
                    LoadSupplierData();
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

                string newName = Microsoft.VisualBasic.Interaction.InputBox("Edit supplier name:", "Edit Supplier", supplierName);

                if (!string.IsNullOrEmpty(newName) && newName != supplierName)
                {
                    try
                    {
                        using var connection = new SqliteConnection(ConnectionString);
                        connection.Open();

                        string updateSql = "UPDATE SUPPLIER SET name = @newName WHERE name = @oldName";
                        using var cmd = new SqliteCommand(updateSql, connection);
                        cmd.Parameters.AddWithValue("@newName", newName);
                        cmd.Parameters.AddWithValue("@oldName", supplierName);
                        cmd.ExecuteNonQuery();

                        MessageBox.Show("Supplier updated successfully!");
                        LoadSuppliers();
                        LoadSupplierData();
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
                var result = MessageBox.Show(
                    "Are you sure you want to delete the selected supplier?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var selectedRow = (DataRowView)SupplierGrid.SelectedItem;
                        string supplierName = selectedRow["Supplier"].ToString();

                        using var connection = new SqliteConnection(ConnectionString);
                        connection.Open();

                        // First check if supplier has items
                        string checkItemsSql = "SELECT COUNT(*) FROM ITEM WHERE supplierID IN (SELECT supplierID FROM SUPPLIER WHERE name = @name)";
                        using (var checkCmd = new SqliteCommand(checkItemsSql, connection))
                        {
                            checkCmd.Parameters.AddWithValue("@name", supplierName);
                            int itemCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                            if (itemCount > 0)
                            {
                                MessageBox.Show("Cannot delete supplier with existing items. Please reassign or delete items first.");
                                return;
                            }
                        }

                        string deleteSql = "DELETE FROM SUPPLIER WHERE name = @name";
                        using var cmd = new SqliteCommand(deleteSql, connection);
                        cmd.Parameters.AddWithValue("@name", supplierName);
                        cmd.ExecuteNonQuery();

                        MessageBox.Show("Supplier deleted successfully!");
                        LoadSuppliers();
                        LoadSupplierData();
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