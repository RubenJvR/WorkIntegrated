using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;

namespace WorkIntegrated;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent(); 
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

        using var conn = new SQLiteConnection("Data Source=ADIX.db;Version=3");
        conn.Open();

        using var cmd = new SQLiteCommand(
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
}
