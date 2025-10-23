using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADIX.Models
{
    internal class ItemGroupModel
    {
        public int GroupID { get; set; }
        public int Quantity { get; set; }
        public string ItemGroup { get; set; }
        public string SKU { get; set; }
        public string ItemName { get; set; }
        public int OpeningStock { get; set; }
        public int StockReceived { get; set; }
        public int StockSold { get; set; }
        public int BalanceStock { get; set; }
        public int StockTake { get; set; }
    }
}
