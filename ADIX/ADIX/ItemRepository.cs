using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADIX
{

    public class Item
    {
        public int ItemID { get; set; }
        public string ItemName { get; set; } = "";
        public double ItemPrice { get; set; }
        public int QuantityInStock { get; set; }
    }

    internal class ItemRepository
    {
        private const string DATABASE_NAME = "ADIX.db";

        public static List<Item> GetAllItems()
        {
            List<Item> items = new();
            using var conn = new SqliteConnection($"Data Source={DATABASE_NAME}");
            conn.Open();

            string query = "SELECT itemID, itemName, itemPrice, quantityInStock FROM ITEM";
            using var cmd = new SqliteCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                items.Add(new Item
                {
                    ItemID = reader.GetInt32(0),
                    ItemName = reader.GetString(1),
                    ItemPrice = reader.GetDouble(2),
                    QuantityInStock = reader.GetInt32(3)
                });
            }
            return items;
        }
    }
}

