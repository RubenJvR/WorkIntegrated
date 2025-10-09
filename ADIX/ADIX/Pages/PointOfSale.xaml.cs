using Microsoft.Data.Sqlite;
using System;
using System.Windows.Controls;
using WorkIntegrated;

namespace ADIX
{
    public partial class PointOfSale : Page
    {
        public PointOfSale()
        {
            InitializeComponent();

            // Load data dynamically from SQLite
            var tableData = LoadItemTableData();
            DataGridHelper.SetTableData(ItemsGrid, tableData);
        }

        private TableData LoadItemTableData()
        {
            List<string> headers = new() { "Item ID", "Name", "Price", "Stock" };
            List<TableDataRow> rows = new();

            using var conn = new SqliteConnection("Data Source=ADIX.db");
            conn.Open();
            string query = "SELECT itemID, description, retailPrice, stockQuantity FROM ITEM";
            using var cmd = new SqliteCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                List<string> cells = new()
        {
            reader.GetInt32(0).ToString(),       // itemID
            reader.GetString(1),                  // description
            reader.GetDouble(2).ToString("F2"),  // retailPrice
            reader.GetInt32(3).ToString()        // stockQuantity
        };
                rows.Add(new TableDataRow(cells));
            }

            return new TableData(headers, rows);
        }


    }
}
