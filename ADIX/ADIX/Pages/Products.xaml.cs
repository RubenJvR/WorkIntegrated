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
using System.Windows.Navigation;
using System.Windows.Shapes;
using WorkIntegrated;

namespace ADIX
{
    /// <summary>
    /// Interaction logic for Products.xaml
    /// </summary>
    public partial class Products : Page
    {
        public Products()
        {
            InitializeComponent();
            Database.Initialize();
            InsertSampleItem();
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
            var productList = new List<Product>();

            using var conn = new SqliteConnection("Data Source=ADIX.db");
            conn.Open();

            using var cmd = new SqliteCommand(
                "SELECT itemID, description, retailPrice, costPrice, stockQuantity, stockSold, supplierID, sellerID FROM ITEM",
                conn);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                productList.Add(new Product
                {
                    ItemID = Convert.ToInt32(reader["itemID"]),
                    Description = reader["description"]?.ToString(),
                    RetailPrice = Convert.ToDecimal(reader["retailPrice"]),
                    CostPrice = Convert.ToDecimal(reader["costPrice"]),
                    StockQuantity = Convert.ToInt32(reader["stockQuantity"]),
                    StockSold = Convert.ToInt32(reader["stockSold"]),
                    SupplierID = Convert.ToInt32(reader["supplierID"]),
                    SellerID = Convert.ToInt32(reader["sellerID"])
                });
            }

           ProductsGrid.ItemsSource = productList;
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
