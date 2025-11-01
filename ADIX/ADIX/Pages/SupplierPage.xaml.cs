using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

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
            // Reconcile stock to ensure accurate numbers
            Database.ReconcileStockQuantities();
            LoadSuppliers();
            LoadSupplierData();
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            Supplier.SelectionChanged += Supplier_SelectionChanged;
            SearchTextBox.TextChanged += SearchTextBox_TextChanged;
            SearchTextBox.KeyDown += SearchTextBox_KeyDown;
            AutoCompleteListBox.SelectionChanged += AutoCompleteListBox_SelectionChanged;
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

                // Add search filter
                if (!string.IsNullOrWhiteSpace(SearchTextBox.Text))
                {
                    cmd.Parameters.AddWithValue("@searchTerm", $"%{SearchTextBox.Text}%");
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
            -- FIXED: Calculate accurate payment amounts based on payment model
            CASE 
                WHEN i.stockRecieved > 0 AND i.stockSold = 0 THEN (i.costPrice * i.stockRecieved)  -- Immediate payment for all received stock
                WHEN i.stockSold > 0 THEN (i.costPrice * i.stockSold)  -- Consignment payment for sold items only
                ELSE 0
            END as AmountOwed,
            COALESCE((SELECT SUM(sp.amount) FROM SUPPLIER_PAYMENT sp WHERE sp.supplierID = s.supplierID), 0) as AmountPaid,
            -- FIXED: Calculate remaining amount to pay
            (CASE 
                WHEN i.stockRecieved > 0 AND i.stockSold = 0 THEN (i.costPrice * i.stockRecieved)
                WHEN i.stockSold > 0 THEN (i.costPrice * i.stockSold)
                ELSE 0
            END - COALESCE((SELECT SUM(sp.amount) FROM SUPPLIER_PAYMENT sp WHERE sp.supplierID = s.supplierID), 0)) as TotalToPay,
            'INV-' || s.supplierID || '-' || i.itemID as InvoiceRef
        FROM ITEM i
        INNER JOIN SUPPLIER s ON i.supplierID = s.supplierID
        WHERE 1=1";

            // Add supplier filter
            if (Supplier.SelectedItem is ComboBoxItem supplierItem && supplierItem.Tag?.ToString() != "ALL")
            {
                baseQuery += " AND s.supplierID = @supplierID";
            }

            // Add search filter
            if (!string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                baseQuery += " AND (s.name LIKE @searchTerm OR i.description LIKE @searchTerm)";
            }

            // Add low stock filter
            if (LowStockToggle.IsChecked == true)
            {
                baseQuery += " AND i.stockQuantity < 10";
            }

            // Add payment status filter
            if (PaymentStatus.SelectedItem is ComboBoxItem paymentItem)
            {
                string paymentStatus = paymentItem.Content.ToString();
                if (paymentStatus == "Paid")
                {
                    baseQuery += " AND (CASE WHEN i.stockRecieved > 0 AND i.stockSold = 0 THEN (i.costPrice * i.stockRecieved) WHEN i.stockSold > 0 THEN (i.costPrice * i.stockSold) ELSE 0 END) <= COALESCE((SELECT SUM(sp.amount) FROM SUPPLIER_PAYMENT sp WHERE sp.supplierID = s.supplierID), 0)";
                }
                else if (paymentStatus == "Pending")
                {
                    baseQuery += " AND (CASE WHEN i.stockRecieved > 0 AND i.stockSold = 0 THEN (i.costPrice * i.stockRecieved) WHEN i.stockSold > 0 THEN (i.costPrice * i.stockSold) ELSE 0 END) > COALESCE((SELECT SUM(sp.amount) FROM SUPPLIER_PAYMENT sp WHERE sp.supplierID = s.supplierID), 0)";
                }
            }

            baseQuery += " ORDER BY s.name, i.description";

            return baseQuery;
        }

        // Search functionality
        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(query))
            {
                AutoCompletePopup.IsOpen = false;
                LoadSupplierData();
                return;
            }

            var results = await SearchSuppliersAndItemsAsync(query);
            AutoCompleteListBox.ItemsSource = results;
            AutoCompletePopup.IsOpen = results.Any();
        }

        private async Task<List<SearchResult>> SearchSuppliersAndItemsAsync(string searchTerm)
        {
            var list = new List<SearchResult>();
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        s.name as SupplierName,
                        i.description as ItemDescription,
                        i.retailPrice as Price,
                        i.stockQuantity as Stock
                    FROM ITEM i
                    INNER JOIN SUPPLIER s ON i.supplierID = s.supplierID
                    WHERE s.name LIKE @term OR i.description LIKE @term
                    ORDER BY s.name, i.description
                    LIMIT 10";

                using var cmd = new SqliteCommand(query, conn);
                cmd.Parameters.AddWithValue("@term", $"%{searchTerm}%");

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var result = new SearchResult
                    {
                        SupplierName = reader["SupplierName"]?.ToString() ?? "Unknown",
                        ItemDescription = reader["ItemDescription"]?.ToString() ?? "Unknown",
                        Price = Convert.ToDouble(reader["Price"]),
                        Stock = Convert.ToInt32(reader["Stock"])
                    };
                    list.Add(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search error: {ex.Message}");
            }

            return list;
        }

        private void SearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                AutoCompletePopup.IsOpen = false;
                SearchTextBox.Clear();
            }
            else if (e.Key == System.Windows.Input.Key.Enter)
            {
                AutoCompletePopup.IsOpen = false;
                LoadSupplierData();
            }
        }

        private void AutoCompleteListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AutoCompleteListBox.SelectedItem is SearchResult selected)
            {
                SearchTextBox.Text = $"{selected.SupplierName} - {selected.ItemDescription}";
                AutoCompletePopup.IsOpen = false;
                LoadSupplierData();
            }
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

        // Event handlers for filters
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

        // Search result class
        public class SearchResult
        {
            public string SupplierName { get; set; }
            public string ItemDescription { get; set; }
            public double Price { get; set; }
            public int Stock { get; set; }
        }

        private void AddSupplierButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get supplier ID from user
                string supplierIdInput = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter Supplier ID (numeric):", "Add Supplier - ID", "");

                if (string.IsNullOrWhiteSpace(supplierIdInput) || !int.TryParse(supplierIdInput, out int supplierId))
                {
                    MessageBox.Show("Please enter a valid numeric Supplier ID.");
                    return;
                }

                // Check if supplier ID already exists
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                string checkIdSql = "SELECT COUNT(*) FROM SUPPLIER WHERE supplierID = @id";
                using var checkCmd = new SqliteCommand(checkIdSql, connection);
                checkCmd.Parameters.AddWithValue("@id", supplierId);
                int existingCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (existingCount > 0)
                {
                    MessageBox.Show($"Supplier ID {supplierId} already exists. Please use a different ID.");
                    return;
                }

                // Get supplier name
                string supplierName = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter supplier name:", "Add Supplier", "");

                if (string.IsNullOrWhiteSpace(supplierName))
                    return;

                // Get contact info
                string contactInfo = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter contact information (phone/email):", "Add Supplier - Contact Info", "");

                // Get address
                string address = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter supplier address:", "Add Supplier - Address", "");

                // Insert supplier with user-specified ID
                string insertSql = @"
            INSERT INTO SUPPLIER (supplierID, name, contactInfo, address, lastModified) 
            VALUES (@id, @name, @contact, @address, CURRENT_TIMESTAMP)";

                using var cmd = new SqliteCommand(insertSql, connection);
                cmd.Parameters.AddWithValue("@id", supplierId);
                cmd.Parameters.AddWithValue("@name", supplierName.Trim());
                cmd.Parameters.AddWithValue("@contact", contactInfo?.Trim() ?? "");
                cmd.Parameters.AddWithValue("@address", address?.Trim() ?? "");

                int rowsAffected = cmd.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    MessageBox.Show($"Supplier '{supplierName}' added successfully with ID: {supplierId}");

                    // Mark sync required
                    Database.MarkSyncRequired();

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

                try
                {
                    using var connection = new SqliteConnection(ConnectionString);
                    connection.Open();

                    // Get current supplier details
                    string selectSql = "SELECT name, contactInfo, address FROM SUPPLIER WHERE name = @name";
                    string currentContactInfo = "";
                    string currentAddress = "";

                    using (var selectCmd = new SqliteCommand(selectSql, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@name", supplierName);
                        using var reader = selectCmd.ExecuteReader();
                        if (reader.Read())
                        {
                            currentContactInfo = reader["contactInfo"].ToString();
                            currentAddress = reader["address"].ToString();
                        }
                    }

                    // Edit name
                    string newName = Microsoft.VisualBasic.Interaction.InputBox(
                        "Edit supplier name:", "Edit Supplier", supplierName);

                    if (string.IsNullOrWhiteSpace(newName))
                        return;

                    // Edit contact info
                    string newContactInfo = Microsoft.VisualBasic.Interaction.InputBox(
                        "Edit contact information:", "Edit Supplier - Contact Info", currentContactInfo);

                    // Edit address
                    string newAddress = Microsoft.VisualBasic.Interaction.InputBox(
                        "Edit supplier address:", "Edit Supplier - Address", currentAddress);

                    // Update the supplier
                    string updateSql = @"
                        UPDATE SUPPLIER 
                        SET name = @newName, 
                            contactInfo = @contactInfo, 
                            address = @address,
                            lastModified = CURRENT_TIMESTAMP 
                        WHERE name = @oldName";

                    using var updateCmd = new SqliteCommand(updateSql, connection);
                    updateCmd.Parameters.AddWithValue("@newName", newName.Trim());
                    updateCmd.Parameters.AddWithValue("@contactInfo", newContactInfo?.Trim() ?? "");
                    updateCmd.Parameters.AddWithValue("@address", newAddress?.Trim() ?? "");
                    updateCmd.Parameters.AddWithValue("@oldName", supplierName);

                    int rowsAffected = updateCmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        MessageBox.Show("Supplier updated successfully!");

                        // Mark sync required
                        Database.MarkSyncRequired();

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
                                    Console.WriteLine($"Sync after edit supplier failed: {syncEx.Message}");
                                }
                            });
                        }
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
                                        Console.WriteLine($"Sync after delete supplier failed: {syncEx.Message}");
                                    }
                                });
                            }
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

        private void ViewBalanceButton_Click(object sender, RoutedEventArgs e)
        {
            if (Supplier.SelectedItem is ComboBoxItem supplierItem && supplierItem.Tag?.ToString() != "ALL")
            {
                int supplierID = Convert.ToInt32(supplierItem.Tag);
                ShowSupplierBalance(supplierID);
            }
            else
            {
                MessageBox.Show("Please select a specific supplier to view balance.");
            }
        }

        private void ShowSupplierBalance(int supplierID)
        {
            try
            {
                var balance = SupplierPaymentCalculator.CalculateSupplierBalance(supplierID);

                // Create a properly styled balance window with scrolling
                var balanceWindow = new Window
                {
                    Title = $"Balance Summary - {balance.SupplierName}",
                    Width = 920, // Slightly wider to accommodate scrollbar
                    Height = 700,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    Background = new SolidColorBrush(Color.FromRgb(135, 135, 135)), // #878787
                    ResizeMode = ResizeMode.CanResize, // Allow resizing
                    MinWidth = 900,
                    MinHeight = 600
                };

                // Main scroll viewer
                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Padding = new Thickness(0)
                };

                var mainGrid = new Grid { Margin = new Thickness(15, 15, 15, 15) };

                // Define rows - using Auto for better flexibility
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40, GridUnitType.Pixel) }); // Header
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(15, GridUnitType.Pixel) });  // Spacing
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(80, GridUnitType.Pixel) });  // Summary
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10, GridUnitType.Pixel) });  // Spacing
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25, GridUnitType.Pixel) });  // Items header
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(200, GridUnitType.Pixel) }); // Items grid
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10, GridUnitType.Pixel) });  // Spacing
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25, GridUnitType.Pixel) });  // Payments header
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(150, GridUnitType.Pixel) }); // Payments grid
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20, GridUnitType.Pixel) });  // Spacing
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40, GridUnitType.Pixel) });  // Buttons

                // HEADER
                var headerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(74, 169, 2)), // #4AA902
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    CornerRadius = new CornerRadius(10),
                    Height = 40
                };

                var headerText = new TextBlock
                {
                    Text = $"Balance Summary - {balance.SupplierName}",
                    FontSize = 18,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.Bold
                };
                headerBorder.Child = headerText;
                Grid.SetRow(headerBorder, 0);
                mainGrid.Children.Add(headerBorder);

                // SUMMARY SECTION - Quick overview at the top
                var summaryBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(79, 79, 79)), // #4F4F4F
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    CornerRadius = new CornerRadius(6)
                };

                var summaryGrid = new Grid();
                summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Total Owed
                AddSummaryItem(summaryGrid, 0, "Total Owed:", $"R {balance.TotalOwed:N2}", Brushes.White);
                // Total Paid
                AddSummaryItem(summaryGrid, 1, "Total Paid:", $"R {balance.TotalPaid:N2}", Brushes.White);
                // Balance Due (colored red if owed, green if credit)
                var balanceColor = balance.BalanceDue > 0 ? Brushes.Red : Brushes.LightGreen;
                AddSummaryItem(summaryGrid, 2, "Balance Due:", $"R {balance.BalanceDue:N2}", balanceColor);

                // Quick Payment Button - RIGHT IN THE SUMMARY SECTION!
                var quickPaymentStack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var quickPaymentButton = new Button
                {
                    Content = "MAKE PAYMENT",
                    Background = new SolidColorBrush(Color.FromRgb(74, 169, 2)), // Green
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Padding = new Thickness(15, 8, 15, 8),
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    BorderBrush = Brushes.Black,
                    Width = 140,
                    Height = 35
                };
                quickPaymentButton.Template = CreateRoundedButtonTemplate();
                quickPaymentButton.Click += (s, e) =>
                {
                    balanceWindow.Close(); // Close balance window first
                    MakePayment(supplierID, balance.SupplierName);
                };

                quickPaymentStack.Children.Add(quickPaymentButton);
                Grid.SetColumn(quickPaymentStack, 3);
                summaryGrid.Children.Add(quickPaymentStack);

                summaryBorder.Child = summaryGrid;
                Grid.SetRow(summaryBorder, 2);
                mainGrid.Children.Add(summaryBorder);

                // OWED ITEMS SECTION
                var itemsHeader = new TextBlock
                {
                    Text = "Items Owed For:",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(itemsHeader, 4);
                mainGrid.Children.Add(itemsHeader);

                var itemsDataGrid = CreateStyledDataGrid();
                itemsDataGrid.MaxHeight = 200;
                itemsDataGrid.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

                itemsDataGrid.Columns.Add(new DataGridTextColumn { Header = "Item Description", Binding = new Binding("Description"), Width = 200 });
                itemsDataGrid.Columns.Add(new DataGridTextColumn { Header = "Payment Model", Binding = new Binding("PaymentModel"), Width = 100 });
                itemsDataGrid.Columns.Add(new DataGridTextColumn { Header = "Stock Rec'd", Binding = new Binding("StockReceived"), Width = 80 });
                itemsDataGrid.Columns.Add(new DataGridTextColumn { Header = "Stock Sold", Binding = new Binding("StockSold"), Width = 80 });
                itemsDataGrid.Columns.Add(new DataGridTextColumn { Header = "Cost Price", Binding = new Binding("CostPrice") { StringFormat = "R {0:N2}" }, Width = 100 });
                itemsDataGrid.Columns.Add(new DataGridTextColumn { Header = "Amount Owed", Binding = new Binding("AmountOwed") { StringFormat = "R {0:N2}" }, Width = 120 });

                itemsDataGrid.ItemsSource = balance.OwedItems;
                Grid.SetRow(itemsDataGrid, 5);
                mainGrid.Children.Add(itemsDataGrid);

                // PAYMENT HISTORY SECTION
                var paymentsHeader = new TextBlock
                {
                    Text = "Payment History:",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(paymentsHeader, 7);
                mainGrid.Children.Add(paymentsHeader);

                var paymentHistoryGrid = CreateStyledDataGrid();
                paymentHistoryGrid.MaxHeight = 150;
                paymentHistoryGrid.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

                paymentHistoryGrid.Columns.Add(new DataGridTextColumn { Header = "Date", Binding = new Binding("Date"), Width = 100 });
                paymentHistoryGrid.Columns.Add(new DataGridTextColumn { Header = "Amount", Binding = new Binding("Amount") { StringFormat = "R {0:N2}" }, Width = 100 });
                paymentHistoryGrid.Columns.Add(new DataGridTextColumn { Header = "Method", Binding = new Binding("Method"), Width = 80 });
                paymentHistoryGrid.Columns.Add(new DataGridTextColumn { Header = "Reference", Binding = new Binding("Reference"), Width = 120 });
                paymentHistoryGrid.Columns.Add(new DataGridTextColumn { Header = "Notes", Binding = new Binding("Notes"), Width = 200 });

                var paymentHistory = SupplierPaymentCalculator.GetPaymentHistory(supplierID);
                paymentHistoryGrid.ItemsSource = paymentHistory.DefaultView;
                Grid.SetRow(paymentHistoryGrid, 8);
                mainGrid.Children.Add(paymentHistoryGrid);

                // ACTION BUTTONS AT BOTTOM
                var buttonStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var makePaymentButton = new Button
                {
                    Content = "Make Payment",
                    Background = new SolidColorBrush(Color.FromRgb(74, 169, 2)), // Green
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Padding = new Thickness(20, 8, 20, 8),
                    Margin = new Thickness(0, 0, 10, 0),
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    BorderBrush = Brushes.Black,
                    Width = 150,
                    Height = 40
                };
                makePaymentButton.Template = CreateRoundedButtonTemplate();
                makePaymentButton.Click += (s, e) =>
                {
                    balanceWindow.Close(); // Close balance window first
                    MakePayment(supplierID, balance.SupplierName);
                };

                var closeButton = new Button
                {
                    Content = "Close",
                    Background = new SolidColorBrush(Color.FromRgb(79, 79, 79)), // #4F4F4F
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Padding = new Thickness(20, 8, 20, 8),
                    Margin = new Thickness(10, 0, 0, 0),
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    BorderBrush = Brushes.Black,
                    Width = 150,
                    Height = 40
                };
                closeButton.Template = CreateRoundedButtonTemplate();
                closeButton.Click += (s, e) => balanceWindow.Close();

                buttonStack.Children.Add(makePaymentButton);
                buttonStack.Children.Add(closeButton);
                Grid.SetRow(buttonStack, 10);
                mainGrid.Children.Add(buttonStack);

                // Add the main grid to scroll viewer and set as window content
                scrollViewer.Content = mainGrid;
                balanceWindow.Content = scrollViewer;

                balanceWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading balance: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private DataGrid CreateStyledDataGrid()
        {
            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Background = new SolidColorBrush(Color.FromRgb(79, 79, 79)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1, 1, 1, 1),
                HeadersVisibility = DataGridHeadersVisibility.Column,
                RowHeight = 30,
                ColumnHeaderHeight = 35,
                GridLinesVisibility = DataGridGridLinesVisibility.All
            };

            // Style the DataGrid to match your app
            dataGrid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader))
            {
                Setters = {
                    new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(Color.FromRgb(45, 45, 45))),
                    new Setter(DataGridColumnHeader.ForegroundProperty, Brushes.White),
                    new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.Bold),
                    new Setter(DataGridColumnHeader.BorderBrushProperty, Brushes.Black),
                    new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0.5, 0.5, 0.5, 0.5)),
                    new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty, HorizontalAlignment.Center),
                    new Setter(DataGridColumnHeader.VerticalContentAlignmentProperty, VerticalAlignment.Center),
                    new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(5, 5, 5, 5))
                }
            };

            // Cell style
            dataGrid.CellStyle = new Style(typeof(DataGridCell))
            {
                Setters = {
                    new Setter(DataGridCell.BorderBrushProperty, Brushes.Black),
                    new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0.5, 0.5, 0.5, 0.5)),
                    new Setter(DataGridCell.PaddingProperty, new Thickness(5, 2, 5, 2)),
                    new Setter(DataGridCell.HorizontalContentAlignmentProperty, HorizontalAlignment.Center),
                    new Setter(DataGridCell.VerticalContentAlignmentProperty, VerticalAlignment.Center),
                    new Setter(DataGridCell.ForegroundProperty, Brushes.White),
                    new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent)
                },
                Triggers = {
                    new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true,
                        Setters = {
                            new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush(Color.FromRgb(106, 106, 106))),
                            new Setter(DataGridCell.ForegroundProperty, Brushes.White),
                            new Setter(DataGridCell.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(74, 169, 2)))
                        }
                    }
                }
            };

            // Row style
            dataGrid.RowStyle = new Style(typeof(DataGridRow))
            {
                Setters = {
                    new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromRgb(79, 79, 79))),
                    new Setter(DataGridRow.ForegroundProperty, Brushes.White)
                },
                Triggers = {
                    new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true,
                        Setters = {
                            new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromRgb(106, 106, 106)))
                        }
                    },
                    new Trigger { Property = DataGridRow.IsMouseOverProperty, Value = true,
                        Setters = {
                            new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromRgb(90, 90, 90)))
                        }
                    }
                }
            };

            return dataGrid;
        }

        private void AddSummaryItem(Grid grid, int column, string label, string value, Brush valueColor)
        {
            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            stackPanel.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = value,
                Foreground = valueColor,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            Grid.SetColumn(stackPanel, column);
            grid.Children.Add(stackPanel);
        }

        private void MakePayment(int supplierID, string supplierName)
        {
            try
            {
                // Create popup window
                var paymentWindow = new Window
                {
                    Title = $"Make Payment - {supplierName}",
                    Width = 470, // Slightly wider to accommodate scrollbar
                    Height = 550,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    Background = new SolidColorBrush(Color.FromRgb(135, 135, 135)), // #878787
                    ResizeMode = ResizeMode.CanResize, // Allow resizing for better UX
                    WindowStyle = WindowStyle.SingleBorderWindow,
                    MinWidth = 450,
                    MinHeight = 500
                };

                // Main scroll viewer to make everything scrollable
                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Padding = new Thickness(0)
                };

                var mainGrid = new Grid { Margin = new Thickness(20, 20, 20, 20) };

                // Define rows - FIXED: Added proper spacing for buttons
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40, GridUnitType.Pixel) }); // Header
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(15, GridUnitType.Pixel) });  // Spacing
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Amount label
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8, GridUnitType.Pixel) });   // Spacing
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30, GridUnitType.Pixel) });  // Amount input
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8, GridUnitType.Pixel) });   // Spacing
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Date label
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8, GridUnitType.Pixel) });   // Spacing
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30, GridUnitType.Pixel) });  // Date input
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8, GridUnitType.Pixel) });   // Spacing
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Method label
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8, GridUnitType.Pixel) });   // Spacing
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30, GridUnitType.Pixel) });  // Method input
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8, GridUnitType.Pixel) });   // Spacing
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Reference label
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8, GridUnitType.Pixel) });   // Spacing
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30, GridUnitType.Pixel) });  // Reference input
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8, GridUnitType.Pixel) });   // Spacing
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Notes label
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8, GridUnitType.Pixel) });   // Spacing
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(80, GridUnitType.Pixel) });  // Notes input
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20, GridUnitType.Pixel) });  // Spacing
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50, GridUnitType.Pixel) });  // Buttons (increased height)

                // Header (matches your app style)
                var headerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(74, 169, 2)), // #4AA902
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    CornerRadius = new CornerRadius(10),
                    Height = 40,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                var headerText = new TextBlock
                {
                    Text = $"Make Payment - {supplierName}",
                    FontSize = 16,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.Bold
                };

                headerBorder.Child = headerText;
                Grid.SetRow(headerBorder, 0);
                mainGrid.Children.Add(headerBorder);

                // Amount Label
                var amountLabel = new TextBlock
                {
                    Text = "Amount (R):",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(amountLabel, 2);
                mainGrid.Children.Add(amountLabel);

                // Amount Input
                var amountBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(79, 79, 79)), // #4F4F4F
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    CornerRadius = new CornerRadius(6),
                    Height = 30
                };

                var amountTextBox = new TextBox
                {
                    Background = Brushes.Transparent,
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0, 0, 0, 0),
                    Padding = new Thickness(8, 5, 8, 5),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Text = "0.00"
                };

                amountBorder.Child = amountTextBox;
                Grid.SetRow(amountBorder, 4);
                mainGrid.Children.Add(amountBorder);

                // Date Label
                var dateLabel = new TextBlock
                {
                    Text = "Payment Date:",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(dateLabel, 6);
                mainGrid.Children.Add(dateLabel);

                // Date Input - Fixed
                var datePicker = new DatePicker
                {
                    SelectedDate = DateTime.Now,
                    Background = new SolidColorBrush(Color.FromRgb(79, 79, 79)),
                    Foreground = Brushes.White,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    Height = 30
                };

                // Fix DatePicker text visibility
                datePicker.Loaded += (s, e) =>
                {
                    var textBox = FindVisualChild<DatePickerTextBox>(datePicker);
                    if (textBox != null)
                    {
                        textBox.Foreground = Brushes.White;
                        textBox.Background = new SolidColorBrush(Color.FromRgb(79, 79, 79));
                        textBox.BorderBrush = Brushes.Transparent;
                    }
                };

                Grid.SetRow(datePicker, 8);
                mainGrid.Children.Add(datePicker);

                // Method Label
                var methodLabel = new TextBlock
                {
                    Text = "Payment Method:",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(methodLabel, 10);
                mainGrid.Children.Add(methodLabel);

                // Method Input - Fixed
                var methodComboBox = new ComboBox
                {
                    Background = Brushes.White, // Light background
                    Foreground = Brushes.Black, // Dark text
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    Height = 30,
                    SelectedIndex = 0
                };

                methodComboBox.Items.Add("Cash");
                methodComboBox.Items.Add("EFT");
                methodComboBox.Items.Add("Cheque");
                methodComboBox.Items.Add("Credit Card");

                // Ensure dropdown items are visible
                methodComboBox.ItemContainerStyle = new Style(typeof(ComboBoxItem))
                {
                    Setters = {
        new Setter(Control.BackgroundProperty, Brushes.White),
        new Setter(Control.ForegroundProperty, Brushes.Black)
    }
                };

                Grid.SetRow(methodComboBox, 12);
                mainGrid.Children.Add(methodComboBox);
                // Reference Label
                var referenceLabel = new TextBlock
                {
                    Text = "Reference Number (Optional):",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(referenceLabel, 14);
                mainGrid.Children.Add(referenceLabel);

                // Reference Input
                var referenceBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(79, 79, 79)),
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    CornerRadius = new CornerRadius(6),
                    Height = 30
                };

                var referenceTextBox = new TextBox
                {
                    Background = Brushes.Transparent,
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0, 0, 0, 0),
                    Padding = new Thickness(8, 5, 8, 5),
                    VerticalContentAlignment = VerticalAlignment.Center
                };

                referenceBorder.Child = referenceTextBox;
                Grid.SetRow(referenceBorder, 16);
                mainGrid.Children.Add(referenceBorder);

                // Notes Label
                var notesLabel = new TextBlock
                {
                    Text = "Notes (Optional):",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(notesLabel, 18);
                mainGrid.Children.Add(notesLabel);

                // Notes Input
                var notesBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(79, 79, 79)),
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    CornerRadius = new CornerRadius(6),
                    Height = 80
                };

                var notesTextBox = new TextBox
                {
                    Background = Brushes.Transparent,
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0, 0, 0, 0),
                    Padding = new Thickness(8, 5, 8, 5),
                    VerticalContentAlignment = VerticalAlignment.Top,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true
                };

                notesBorder.Child = notesTextBox;
                Grid.SetRow(notesBorder, 20);
                mainGrid.Children.Add(notesBorder);

                // BUTTONS SECTION - Made more prominent
                var buttonsBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(79, 79, 79)),
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    CornerRadius = new CornerRadius(6),
                    Height = 50,
                    Padding = new Thickness(10, 5, 10, 5)
                };

                var buttonStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var processButton = new Button
                {
                    Content = "💳 PROCESS PAYMENT", // Added emoji to make it more visible
                    Background = new SolidColorBrush(Color.FromRgb(74, 169, 2)), // Green
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    Padding = new Thickness(25, 10, 25, 10),
                    Margin = new Thickness(0, 0, 15, 0),
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    BorderBrush = Brushes.Black,
                    Width = 180,
                    Height = 38
                };

                // Style the button with rounded corners
                processButton.Template = CreateRoundedButtonTemplate();

                var cancelButton = new Button
                {
                    Content = "❌ Cancel",
                    Background = new SolidColorBrush(Color.FromRgb(79, 79, 79)), // #4F4F4F
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    Padding = new Thickness(25, 10, 25, 10),
                    Margin = new Thickness(15, 0, 0, 0),
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    BorderBrush = Brushes.Black,
                    Width = 150,
                    Height = 38
                };

                // Style the button with rounded corners
                cancelButton.Template = CreateRoundedButtonTemplate();

                buttonStack.Children.Add(processButton);
                buttonStack.Children.Add(cancelButton);
                buttonsBorder.Child = buttonStack;
                Grid.SetRow(buttonsBorder, 22);
                mainGrid.Children.Add(buttonsBorder);

                // ADD ENTER KEY SUPPORT - Pressing Enter will process payment
                amountTextBox.KeyDown += (s, e) =>
                {
                    if (e.Key == System.Windows.Input.Key.Enter)
                    {
                        ProcessThePayment();
                    }
                };

                referenceTextBox.KeyDown += (s, e) =>
                {
                    if (e.Key == System.Windows.Input.Key.Enter)
                    {
                        ProcessThePayment();
                    }
                };

                notesTextBox.KeyDown += (s, e) =>
                {
                    if (e.Key == System.Windows.Input.Key.Enter)
                    {
                        // For notes, don't process on Enter (allows multiline)
                        if (!notesTextBox.AcceptsReturn)
                        {
                            ProcessThePayment();
                        }
                    }
                };

                // Payment processing method
                void ProcessThePayment()
                {
                    if (decimal.TryParse(amountTextBox.Text, out decimal amount) && amount > 0)
                    {
                        bool success = SupplierPaymentCalculator.ProcessPayment(
                            supplierID,
                            amount,
                            datePicker.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                            methodComboBox.SelectedItem?.ToString() ?? "Cash",
                            referenceTextBox.Text,
                            notesTextBox.Text
                        );

                        if (success)
                        {
                            MessageBox.Show($"Payment of R {amount:N2} processed successfully!", "Success",
                                          MessageBoxButton.OK, MessageBoxImage.Information);
                            paymentWindow.DialogResult = true;
                            paymentWindow.Close();
                        }
                        else
                        {
                            MessageBox.Show("Failed to process payment. Please try again.", "Error",
                                          MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Please enter a valid payment amount greater than R 0.00.", "Invalid Amount",
                                      MessageBoxButton.OK, MessageBoxImage.Warning);
                        amountTextBox.Focus();
                        amountTextBox.SelectAll();
                    }
                }

                // Event handlers
                processButton.Click += (s, e) => ProcessThePayment();

                cancelButton.Click += (s, e) =>
                {
                    paymentWindow.DialogResult = false;
                    paymentWindow.Close();
                };

                // Set focus to amount field when window loads
                paymentWindow.Loaded += (s, e) =>
                {
                    amountTextBox.Focus();
                    amountTextBox.SelectAll();
                };

                // Add the main grid to scroll viewer and set as window content
                scrollViewer.Content = mainGrid;
                paymentWindow.Content = scrollViewer;

                var result = paymentWindow.ShowDialog();

                if (result == true)
                {
                    // Refresh the balance view if open
                    ShowSupplierBalance(supplierID);

                    // Also refresh the main supplier data
                    LoadSupplierData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing payment: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Helper method to create rounded button template
        private ControlTemplate CreateRoundedButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));

            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            border.AppendChild(contentPresenter);
            template.VisualTree = border;

            // Trigger for mouse over
            var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            trigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(255, 100, 100, 100))));
            template.Triggers.Add(trigger);

            return template;
        }

        private void ViewAllBalancesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var balances = SupplierPaymentCalculator.GetAllSuppliersWithBalances();

                var balancesWindow = new Window
                {
                    Title = "All Supplier Balances",
                    Width = 900,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    Background = new SolidColorBrush(Color.FromRgb(135, 135, 135)), // #878787
                    ResizeMode = ResizeMode.NoResize
                };

                var mainGrid = new Grid { Margin = new Thickness(20, 20, 20, 20) };

                // Header
                var headerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(74, 169, 2)), // #4AA902
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    CornerRadius = new CornerRadius(10),
                    Height = 40,
                    Margin = new Thickness(0, 0, 0, 20)
                };

                var headerText = new TextBlock
                {
                    Text = "Supplier Balances Summary",
                    FontSize = 18,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.Bold
                };
                headerBorder.Child = headerText;
                mainGrid.Children.Add(headerBorder);

                var dataGrid = CreateStyledDataGrid();
                dataGrid.Margin = new Thickness(0, 60, 0, 0);
                dataGrid.Height = 450;

                // FIXED CURRENCY FORMATTING - Use "R {0:N2}" for South African Rand
                dataGrid.Columns.Add(new DataGridTextColumn { Header = "Supplier", Binding = new Binding("Supplier"), Width = 200 });
                dataGrid.Columns.Add(new DataGridTextColumn { Header = "Contact", Binding = new Binding("Contact"), Width = 150 });
                dataGrid.Columns.Add(new DataGridTextColumn { Header = "Total Owed", Binding = new Binding("TotalOwed") { StringFormat = "R {0:N2}" }, Width = 120 });
                dataGrid.Columns.Add(new DataGridTextColumn { Header = "Total Paid", Binding = new Binding("TotalPaid") { StringFormat = "R {0:N2}" }, Width = 120 });
                dataGrid.Columns.Add(new DataGridTextColumn { Header = "Balance Due", Binding = new Binding("BalanceDue") { StringFormat = "R {0:N2}" }, Width = 120 });

                dataGrid.ItemsSource = balances.DefaultView;
                mainGrid.Children.Add(dataGrid);

                balancesWindow.Content = mainGrid;
                balancesWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading balances: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Helper method to find child controls
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;
                else
                {
                    var descendant = FindVisualChild<T>(child);
                    if (descendant != null)
                        return descendant;
                }
            }
            return null;
        }
    }
}