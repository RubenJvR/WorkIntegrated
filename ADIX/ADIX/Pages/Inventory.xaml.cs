using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// <summary>
    /// Interaction logic for Inventory.xaml
    /// </summary>
    /// 

    public partial class Inventory : Page
    {
        public ObservableCollection<InventoryItem> InventoryItems { get; set; }

        public Inventory()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            // Dummy data for now
            InventoryItems = new ObservableCollection<InventoryItem>
            {
                new InventoryItem
                {
                    ItemGroup = "Electronics",
                    ItemName = "Bluetooth Speaker",
                    SKU = "ELEC001",
                    OpeningStockQuantity = 50,
                    StockReceived = 20,
                    StockSold = 10,
                    BalanceStock = 60,
                    StockReturned = 2,
                    StockRefunded = 1,
                    CostOfBusinessWorkings = "Moderate",
                    ReturnedStockUnusable = "Yes",
                    Loss = "Low"
                },
                new InventoryItem
                {
                    ItemGroup = "Stationery",
                    ItemName = "Notebook",
                    SKU = "STN002",
                    OpeningStockQuantity = 200,
                    StockReceived = 50,
                    StockSold = 70,
                    BalanceStock = 180,
                    StockReturned = 3,
                    StockRefunded = 0,
                    CostOfBusinessWorkings = "Low",
                    ReturnedStockUnusable = "No",
                    Loss = "None"
                }
            };

            // Bind to DataGrid
            InventoryGrid.ItemsSource = InventoryItems;
        }
    }
}
