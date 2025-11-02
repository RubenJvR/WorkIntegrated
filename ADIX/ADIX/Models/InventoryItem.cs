using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADIX
{
    class InventoryItem
    {
        //models reference
        //https://learn.microsoft.com/en-us/aspnet/mvc/overview/older-versions/getting-started-with-aspnet-mvc3/cs/adding-a-model
        public int ItemID { get; set; }
        public string ItemGroup { get; set; }
        public string ItemName { get; set; }
        public string SKU { get; set; }
        public int OpeningStockQuantity { get; set; }
        public int StockReceived { get; set; }
        public int StockSold { get; set; }
        public int BalanceStock { get; set; }
        public double CostPrice { get; set; }
        public double RetailPrice { get; set; }
        public int StockReturned { get; set; }
        public int StockRefunded { get; set; }
        public double CostOfBusinessWorkings { get; set; }
        public int ReturnedStockUnusable { get; set; }
        public double Loss { get; set; }
        public int MinimumStock { get; set; }
        public string PaymentMethod { get; set; }
    }
}