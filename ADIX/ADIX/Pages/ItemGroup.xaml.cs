using ADIX.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
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

namespace ADIX
{
    public partial class ItemGroup : Page
    {
        private ObservableCollection<ItemGroupModel> _itemGroups = new ObservableCollection<ItemGroupModel>();
        private const string ConnectionString = "Data Source=ADIX.db";

        public ItemGroup()
        {
            InitializeComponent();
            LoadItemGroupData();
            SalesGrid.ItemsSource = _itemGroups;
        }

        private void LoadItemGroupData()
        {
            _itemGroups.Clear();

            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            string query = @"SELECT 
    I.itemID AS SKU,
    G.groupName AS ItemGroup,
    I.description AS ItemName,
    I.stockQuantity AS BalanceStock,
    I.stockSold AS StockSold,
    (I.stockQuantity + I.stockSold) AS OpeningStock,
    0 AS StockReceived,
    0 AS StockTake
FROM ITEM I
LEFT JOIN ITEMGROUP G ON I.groupID = G.groupID;
";

            using var cmd = new SqliteCommand(query, conn);
            using var reader = cmd.ExecuteReader();
            var list = new List<ItemGroupModel>();

            while (reader.Read())
            {
                _itemGroups.Add(new ItemGroupModel
                {
                    Quantity = reader.GetInt32(reader.GetOrdinal("BalanceStock")),
                    ItemGroup = reader["ItemGroup"]?.ToString() ?? "",
                    SKU = reader["SKU"]?.ToString() ?? "",
                    ItemName = reader["ItemName"]?.ToString() ?? "",
                    OpeningStock = reader.GetInt32(reader.GetOrdinal("OpeningStock")),
                    StockReceived = reader.GetInt32(reader.GetOrdinal("StockReceived")),
                    StockSold = reader.GetInt32(reader.GetOrdinal("StockSold")),
                    BalanceStock = reader.GetInt32(reader.GetOrdinal("BalanceStock")),
                    StockTake = reader.GetInt32(reader.GetOrdinal("StockTake"))
                });
            }
        }

        // Handle search for all three boxes
        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            string groupSearch = ItemGroupSearch.Text?.ToLower() ?? "";
            string skuSearch = SKUSearch.Text?.ToLower() ?? "";
            string nameSearch = ItemNameSearch.Text?.ToLower() ?? "";

            var filtered = _itemGroups.Where(x =>
                (string.IsNullOrEmpty(groupSearch) || x.ItemGroup.ToLower().Contains(groupSearch)) &&
                (string.IsNullOrEmpty(skuSearch) || x.SKU.ToLower().Contains(skuSearch)) &&
                (string.IsNullOrEmpty(nameSearch) || x.ItemName.ToLower().Contains(nameSearch))
            ).ToList();

            SalesGrid.ItemsSource = filtered;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ItemGroupSearch.TextChanged += Search_TextChanged;
            SKUSearch.TextChanged += Search_TextChanged;
            ItemNameSearch.TextChanged += Search_TextChanged;
        }
    }
}