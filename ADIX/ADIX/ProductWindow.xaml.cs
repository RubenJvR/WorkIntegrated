using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using WorkIntegrated;

namespace ADIX
{
    /// <summary>
    /// Interaction logic for ProductWindow.xaml
    /// </summary>
    public partial class ProductWindow : Window
    {
        public ProductWindow()
        {
            InitializeComponent();
            Database.Initialize();
            InsertSampleItem();
            LoadItem();

        }

        private void LoadItem()
        {
            var headers = new List<string>
        {
            "Item ID", "Description", "Retail Price", "Cost Price",
            "Stock Quantity", "Stock Sold", "Supplier ID", "Seller ID"
        };
            Console.WriteLine("Hello World");
            var rows = new List<TableDataRow>();

            using var conn = new SqliteConnection("Data Source=ADIX.db");
            conn.Open();

            using var cmd = new SqliteCommand(
                "SELECT itemID, description, retailPrice, costPrice, stockQuantity, stockSold, supplierID, sellerID FROM ITEM",
                conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var cells = new List<string>
            {
                reader["itemID"]?.ToString() ?? "",
                reader["description"]?.ToString() ?? "",
                reader["retailPrice"]?.ToString() ?? "",
                reader["costPrice"]?.ToString() ?? "",
                reader["stockQuantity"]?.ToString() ?? "",
                reader["stockSold"]?.ToString() ?? "",
                reader["supplierID"]?.ToString() ?? "",
                reader["sellerID"]?.ToString() ?? ""
            };

                rows.Add(new TableDataRow(cells));
            }

            ItemGrid.SetValue(DataGridHelper.TableDataProperty, new TableData(headers, rows));
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MainWindow window = new MainWindow();
            window.Show();
            this.Close();
        }

        private void InsertSampleItem()
        {
            using var conn = new SqliteConnection("Data Source=ADIX.db");
            conn.Open();

            using var transaction = conn.BeginTransaction();

            // Insert supplier
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                INSERT OR IGNORE INTO SUPPLIER (supplierID, name, contactInfo, address)
                VALUES (1, 'ABC Supplies', '123-456-7890', '123 Main St');";
                cmd.ExecuteNonQuery();
            }

            // Insert seller
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                INSERT OR IGNORE INTO SELLER (sellerID, name, contactInfo, bankDetails, commissionRate)
                VALUES (1, 'John Doe', 'john@example.com', 'Bank XYZ 12345', 0.1);";
                cmd.ExecuteNonQuery();
            }

            // Insert item
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                INSERT OR IGNORE INTO ITEM (itemID, description, retailPrice, costPrice, stockQuantity, stockSold, supplierID, sellerID)
                VALUES (1, 'Bow', 3000, 15.00, 50, 0, 1, 1);";
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
            conn.Close();
        }

    }
}
