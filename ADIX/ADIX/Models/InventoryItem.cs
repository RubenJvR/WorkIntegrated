using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADIX
{
    public class InventoryItem
    {
        public string ItemGroup { get; set; }
        public string ItemName { get; set; }
        public string SKU { get; set; }
        public int OpeningStockQuantity { get; set; }
        public int StockReceived { get; set; }
        public int StockSold { get; set; }
        public int BalanceStock { get; set; }
        public int StockReturned { get; set; }
        public int StockRefunded { get; set; }
        public double CostOfBusinessWorkings { get; set; }
        public double ReturnedStockUnusable { get; set; }
        public double Loss { get; set; }
    }
}
