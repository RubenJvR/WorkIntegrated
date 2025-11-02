using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;

namespace ADIX
{
    public static class SupplierPaymentCalculator
    {
        private const string ConnectionString = "Data Source=ADIX.db";


        static SupplierPaymentCalculator()
        {
            // Ensure tables exist when the class is first used
            Database.EnsureSupplierPaymentTablesExist();
        }

        public class SupplierBalance
        {
            public int SupplierID { get; set; }
            public string SupplierName { get; set; }
            public decimal TotalOwed { get; set; }
            public decimal TotalPaid { get; set; }
            public decimal BalanceDue { get; set; }
            public List<OwedItem> OwedItems { get; set; } = new List<OwedItem>();
        }

        public class OwedItem
        {
            public int ItemID { get; set; }
            public string Description { get; set; }
            public int StockSold { get; set; }
            public decimal CostPrice { get; set; }
            public decimal AmountOwed { get; set; }
            public int StockReceived { get; set; }
            public string PaymentModel { get; set; } // "Immediate" or "Consignment"
        }

        // Calculate total amount owed to a supplier based on different payment models
        public static SupplierBalance CalculateSupplierBalance(int supplierID)
        {
            var balance = new SupplierBalance { SupplierID = supplierID };

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            // Get supplier name
            balance.SupplierName = GetSupplierName(connection, supplierID);

            // Calculate amounts based on different payment models
            CalculateImmediatePayments(connection, supplierID, balance);
            CalculateConsignmentPayments(connection, supplierID, balance);

            // Get total paid
            balance.TotalPaid = GetTotalPaid(connection, supplierID);
            balance.BalanceDue = balance.TotalOwed - balance.TotalPaid;

            return balance;
        }

        private static string GetSupplierName(SqliteConnection connection, int supplierID)
        {
            string query = "SELECT name FROM SUPPLIER WHERE supplierID = @supplierID";
            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@supplierID", supplierID);
            return cmd.ExecuteScalar()?.ToString() ?? "Unknown Supplier";
        }

        private static void CalculateImmediatePayments(SqliteConnection connection, int supplierID, SupplierBalance balance)
        {
            // For immediate payment model: pay for all received stock
            string query = @"
                SELECT 
                    i.itemID,
                    i.description,
                    i.stockRecieved as StockReceived,
                    i.costPrice,
                    (i.stockRecieved * i.costPrice) as AmountOwed
                FROM ITEM i
                WHERE i.supplierID = @supplierID 
                AND i.stockRecieved > 0";

            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@supplierID", supplierID);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var owedItem = new OwedItem
                {
                    ItemID = Convert.ToInt32(reader["itemID"]),
                    Description = reader["description"].ToString(),
                    StockReceived = Convert.ToInt32(reader["StockReceived"]),
                    CostPrice = Convert.ToDecimal(reader["costPrice"]),
                    AmountOwed = Convert.ToDecimal(reader["AmountOwed"]),
                    PaymentModel = "Immediate"
                };

                balance.OwedItems.Add(owedItem);
                balance.TotalOwed += owedItem.AmountOwed;
            }
        }

        private static void CalculateConsignmentPayments(SqliteConnection connection, int supplierID, SupplierBalance balance)
        {
            // For consignment model: pay only for sold items
            string query = @"
                SELECT 
                    i.itemID,
                    i.description,
                    i.stockSold,
                    i.costPrice,
                    (i.stockSold * i.costPrice) as AmountOwed,
                    i.stockRecieved as StockReceived
                FROM ITEM i
                WHERE i.supplierID = @supplierID 
                AND i.stockSold > 0";

            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@supplierID", supplierID);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var itemID = Convert.ToInt32(reader["itemID"]);

                // Check if this item already exists in owed items (shouldn't for consignment)
                var existingItem = balance.OwedItems.Find(item => item.ItemID == itemID);
                if (existingItem == null)
                {
                    var owedItem = new OwedItem
                    {
                        ItemID = itemID,
                        Description = reader["description"].ToString(),
                        StockSold = Convert.ToInt32(reader["stockSold"]),
                        CostPrice = Convert.ToDecimal(reader["costPrice"]),
                        AmountOwed = Convert.ToDecimal(reader["AmountOwed"]),
                        StockReceived = Convert.ToInt32(reader["StockReceived"]),
                        PaymentModel = "Consignment"
                    };

                    balance.OwedItems.Add(owedItem);
                    balance.TotalOwed += owedItem.AmountOwed;
                }
            }
        }

        private static decimal GetTotalPaid(SqliteConnection connection, int supplierID)
        {
            string query = "SELECT COALESCE(SUM(amount), 0) FROM SUPPLIER_PAYMENT WHERE supplierID = @supplierID";
            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@supplierID", supplierID);

            var result = cmd.ExecuteScalar();
            return result != DBNull.Value ? Convert.ToDecimal(result) : 0m;
        }

        /// <summary>
        /// Get payment history for a supplier
        /// </summary>
        public static DataTable GetPaymentHistory(int supplierID)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string query = @"
                SELECT 
                    paymentID as ID,
                    amount as Amount,
                    paymentDate as Date,
                    paymentMethod as Method,
                    referenceNumber as Reference,
                    notes as Notes
                FROM SUPPLIER_PAYMENT 
                WHERE supplierID = @supplierID
                ORDER BY paymentDate DESC";

            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@supplierID", supplierID);

            var dataTable = new DataTable();
            using var reader = cmd.ExecuteReader();
            dataTable.Load(reader);

            return dataTable;
        }

        /// <summary>
        /// Process a payment to a supplier
        /// </summary>
        public static bool ProcessPayment(int supplierID, decimal amount, string paymentDate,
                                        string paymentMethod, string referenceNumber = "", string notes = "")
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                string insertSql = @"
                    INSERT INTO SUPPLIER_PAYMENT 
                    (supplierID, amount, paymentDate, paymentMethod, referenceNumber, notes)
                    VALUES 
                    (@supplierID, @amount, @paymentDate, @paymentMethod, @referenceNumber, @notes)";

                using var cmd = new SqliteCommand(insertSql, connection);
                cmd.Parameters.AddWithValue("@supplierID", supplierID);
                cmd.Parameters.AddWithValue("@amount", amount);
                cmd.Parameters.AddWithValue("@paymentDate", paymentDate);
                cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
                cmd.Parameters.AddWithValue("@referenceNumber", referenceNumber ?? "");
                cmd.Parameters.AddWithValue("@notes", notes ?? "");

                int rowsAffected = cmd.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    // Mark sync required
                    Database.MarkSyncRequired();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing supplier payment: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get all suppliers with their current balances
        /// </summary>
        public static DataTable GetAllSuppliersWithBalances()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string query = @"
            SELECT 
                s.supplierID as ID,
                s.name as Supplier,
                s.contactInfo as Contact,
                s.address as Address,
                COALESCE((
                    SELECT SUM(i.stockRecieved * i.costPrice) 
                    FROM ITEM i 
                    WHERE i.supplierID = s.supplierID
                ), 0) as TotalOwed,
                COALESCE((
                    SELECT SUM(sp.amount) 
                    FROM SUPPLIER_PAYMENT sp 
                    WHERE sp.supplierID = s.supplierID
                ), 0) as TotalPaid,
                (COALESCE((
                    SELECT SUM(i.stockRecieved * i.costPrice) 
                    FROM ITEM i 
                    WHERE i.supplierID = s.supplierID
                ), 0) - COALESCE((
                    SELECT SUM(sp.amount) 
                    FROM SUPPLIER_PAYMENT sp 
                    WHERE sp.supplierID = s.supplierID
                ), 0)) as BalanceDue
            FROM SUPPLIER s
            ORDER BY BalanceDue DESC, s.name";

            using var cmd = new SqliteCommand(query, connection);

            var dataTable = new DataTable();
            using var reader = cmd.ExecuteReader();
            dataTable.Load(reader);

            return dataTable;
        }
    }
}