using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using ADIX.Models;

namespace ADIX.Repositories
{
    public class POSRepository
    {
        private readonly string _connectionString;

        public POSRepository(string connectionString = "Data Source=ADIX.db")
        {
            _connectionString = connectionString;
        }

        public List<POSItem> GetAllItems()
        {
            var items = new List<POSItem>();

            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                string query = @"
                    SELECT itemID, description, retailPrice, stockQuantity 
                    FROM ITEM 
                    WHERE stockQuantity > 0
                    ORDER BY description";

                using var cmd = new SqliteCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    items.Add(new POSItem
                    {
                        ItemID = reader.GetInt32(0),
                        ItemName = reader.GetString(1),
                        Price = (decimal)reader.GetDouble(2),
                        InStock = reader.GetInt32(3),
                        StockControl = reader.GetInt32(3),
                        Quantity = 0,
                        ItemDiscount = 0
                    });
                }
            }
            catch (SqliteException ex)
            {
                throw new Exception($"Database error loading items: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading items: {ex.Message}", ex);
            }

            return items;
        }
        public void RefreshItemsFromSync()
        {
            try
            {
                // This will force a sync and reload items
                if (Database.IsInternetAvailable())
                {
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            await Database.CheckAndSyncAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Background sync failed: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing items from sync: {ex.Message}");
            }
        }
        public POSItem GetItemById(int itemId)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                string query = @"
                    SELECT itemID, description, retailPrice, stockQuantity 
                    FROM ITEM 
                    WHERE itemID = @itemID";

                using var cmd = new SqliteCommand(query, conn);
                cmd.Parameters.AddWithValue("@itemID", itemId);

                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    return new POSItem
                    {
                        ItemID = reader.GetInt32(0),
                        ItemName = reader.GetString(1),
                        Price = (decimal)reader.GetDouble(2),
                        InStock = reader.GetInt32(3),
                        StockControl = reader.GetInt32(3),
                        Quantity = 0,
                        ItemDiscount = 0
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading item: {ex.Message}", ex);
            }
        }

        public List<StaffMember> GetAllStaff()
        {
            var staff = new List<StaffMember>();

            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                string query = "SELECT staffID, name FROM STAFF ORDER BY name";

                using var cmd = new SqliteCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    staff.Add(new StaffMember
                    {
                        StaffID = reader.GetInt32(0),
                        Name = reader.GetString(1)
                    });
                }
            }
            catch (SqliteException ex)
            {
                throw new Exception($"Database error loading staff: {ex.Message}\nMake sure the database is initialized.", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading staff: {ex.Message}", ex);
            }

            return staff;
        }

        public int CreateInvoice(string customerName, int staffId, string paymentMethod,
            bool paymentReceived, decimal vatAmount, string address, int type, decimal totalAmount)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                // Create or get customer
                int customerId = GetOrCreateCustomer(conn, customerName, address);

                // Create invoice with synced = 0 (not synced yet)
                string query = @"
                    INSERT INTO INVOICEQUOTE (date, type, totalAmount, customerID, staffID, synced)
                    VALUES (@date, @type, @totalAmount, @customerID, @staffID, 0);
                    SELECT last_insert_rowid();";

                using var cmd = new SqliteCommand(query, conn);
                cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@totalAmount", (double)totalAmount);
                cmd.Parameters.AddWithValue("@customerID", customerId);
                cmd.Parameters.AddWithValue("@staffID", staffId);

                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating invoice: {ex.Message}", ex);
            }
        }

        public void AddInvoiceItems(int invoiceId, List<POSItem> items)
        {
            SqliteConnection conn = null;
            SqliteTransaction transaction = null;

            try
            {
                conn = new SqliteConnection(_connectionString);
                conn.Open();

                transaction = conn.BeginTransaction();

                foreach (var item in items)
                {
                    if (item.Quantity > 0)
                    {
                        // Add invoice item with synced = 0
                        string itemQuery = @"
                            INSERT INTO INVOICEITEM (quantity, priceAtSale, itemID, invoiceQuoteID, synced)
                            VALUES (@quantity, @priceAtSale, @itemID, @invoiceQuoteID, 0)";

                        using var itemCmd = new SqliteCommand(itemQuery, conn, transaction);
                        itemCmd.Parameters.AddWithValue("@quantity", item.Quantity);
                        itemCmd.Parameters.AddWithValue("@priceAtSale", (double)item.Price);
                        itemCmd.Parameters.AddWithValue("@itemID", item.ItemID);
                        itemCmd.Parameters.AddWithValue("@invoiceQuoteID", invoiceId);
                        itemCmd.ExecuteNonQuery();

                        // Update stock locally (immediate feedback for user)
                        string stockQuery = @"
                            UPDATE ITEM 
                            SET stockQuantity = stockQuantity - @quantity,
                                stockSold = stockSold + @quantity,
                                lastModified = CURRENT_TIMESTAMP
                            WHERE itemID = @itemID";

                        using var stockCmd = new SqliteCommand(stockQuery, conn, transaction);
                        stockCmd.Parameters.AddWithValue("@quantity", item.Quantity);
                        stockCmd.Parameters.AddWithValue("@itemID", item.ItemID);
                        stockCmd.ExecuteNonQuery();
                    }
                }

                transaction.Commit();

                // Mark that sync is required
                Database.MarkSyncRequired();

                // Try to sync immediately if online
                if (Database.IsInternetAvailable())
                {
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            await Database.CheckAndSyncAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Background sync failed: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                transaction?.Rollback();
                throw new Exception($"Error adding invoice items: {ex.Message}", ex);
            }
            finally
            {
                transaction?.Dispose();
                conn?.Dispose();
            }
        }

        private int GetOrCreateCustomer(SqliteConnection conn, string customerName, string address)
        {
            if (string.IsNullOrWhiteSpace(customerName))
            {
                customerName = "Walk-in Customer";
            }

            // Check if customer exists
            string checkQuery = "SELECT customerID FROM CUSTOMER WHERE name = @name LIMIT 1";
            using var checkCmd = new SqliteCommand(checkQuery, conn);
            checkCmd.Parameters.AddWithValue("@name", customerName);

            var result = checkCmd.ExecuteScalar();
            if (result != null)
            {
                return Convert.ToInt32(result);
            }

            // Create new customer with lastModified
            string insertQuery = @"
                INSERT INTO CUSTOMER (name, phone, email, credit, lastModified)
                VALUES (@name, '', '', 0, CURRENT_TIMESTAMP);
                SELECT last_insert_rowid();";

            using var insertCmd = new SqliteCommand(insertQuery, conn);
            insertCmd.Parameters.AddWithValue("@name", customerName);

            return Convert.ToInt32(insertCmd.ExecuteScalar());
        }

        public int GetNextInvoiceNumber()
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                string query = "SELECT COALESCE(MAX(invoiceQuoteID), 0) + 1 FROM INVOICEQUOTE";
                using var cmd = new SqliteCommand(query, conn);

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting invoice number: {ex.Message}", ex);
            }
        }


        /// <summary>
        /// Create a refund invoice
        /// </summary>
        public int CreateRefund(string customerName, int staffId, decimal vatAmount, string address, decimal totalAmount)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                // Create or get customer
                int customerId = GetOrCreateCustomer(conn, customerName, address);

                // Create refund invoice with type = 1 (same as sale, but we'll handle negative quantities in sync)
                // Total amount is stored as positive, but quantities will be negative
                string query = @"
                INSERT INTO INVOICEQUOTE (date, type, totalAmount, customerID, staffID, synced)
                VALUES (@date, @type, @totalAmount, @customerID, @staffID, 0);
                SELECT last_insert_rowid();";

                using var cmd = new SqliteCommand(query, conn);
                cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@type", 1); // Type 1 = Sale/Refund (distinguished by negative quantities)
                cmd.Parameters.AddWithValue("@totalAmount", (double)Math.Abs(totalAmount)); // Store as positive
                cmd.Parameters.AddWithValue("@customerID", customerId);
                cmd.Parameters.AddWithValue("@staffID", staffId);

                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating refund: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Add refund items to invoice (with negative quantities)
        /// </summary>
        public void AddRefundItems(int refundId, List<POSItem> items)
        {
            SqliteConnection conn = null;
            SqliteTransaction transaction = null;

            try
            {
                conn = new SqliteConnection(_connectionString);
                conn.Open();

                transaction = conn.BeginTransaction();

                foreach (var item in items)
                {
                    if (item.Quantity > 0)
                    {
                        // Use negative quantity for refunds (this is the proper way)
                        string itemQuery = @"
                    INSERT INTO INVOICEITEM (quantity, priceAtSale, itemID, invoiceQuoteID, synced)
                    VALUES (@quantity, @priceAtSale, @itemID, @invoiceQuoteID, 0)";

                        using var itemCmd = new SqliteCommand(itemQuery, conn, transaction);
                        itemCmd.Parameters.AddWithValue("@quantity", -item.Quantity); // Negative for refund
                        itemCmd.Parameters.AddWithValue("@priceAtSale", (double)item.Price);
                        itemCmd.Parameters.AddWithValue("@itemID", item.ItemID);
                        itemCmd.Parameters.AddWithValue("@invoiceQuoteID", refundId);
                        itemCmd.ExecuteNonQuery();

                        // Update stock locally (add back to stock for refund)
                        string stockQuery = @"
                    UPDATE ITEM 
                    SET stockQuantity = stockQuantity + @quantity,
                        stockSold = stockSold - @quantity,
                        lastModified = CURRENT_TIMESTAMP
                    WHERE itemID = @itemID";

                        using var stockCmd = new SqliteCommand(stockQuery, conn, transaction);
                        stockCmd.Parameters.AddWithValue("@quantity", item.Quantity); // Positive quantity for stock adjustment
                        stockCmd.Parameters.AddWithValue("@itemID", item.ItemID);
                        stockCmd.ExecuteNonQuery();

                        Console.WriteLine($"[REFUND] Processed refund for item {item.ItemID}: {item.Quantity} units");
                    }
                }

                transaction.Commit();

                // Mark that sync is required
                Database.MarkSyncRequired();

                // Try to sync immediately if online
                if (Database.IsInternetAvailable())
                {
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            await Database.CheckAndSyncAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Background sync failed: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                transaction?.Rollback();
                throw new Exception($"Error adding refund items: {ex.Message}", ex);
            }
            finally
            {
                transaction?.Dispose();
                conn?.Dispose();
            }
        }




        // Test connection method
        public bool TestConnection()
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get sync status for display
        /// </summary>
        public SyncStatus GetSyncStatus()
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                // Count unsynced invoices
                string unsyncedQuery = "SELECT COUNT(*) FROM INVOICEQUOTE WHERE synced = 0";
                using var cmd = new SqliteCommand(unsyncedQuery, conn);
                int unsyncedCount = Convert.ToInt32(cmd.ExecuteScalar());

                return new SyncStatus
                {
                    HasUnsyncedData = unsyncedCount > 0,
                    UnsyncedInvoiceCount = unsyncedCount,
                    IsOnline = Database.IsInternetAvailable(),
                    LastSyncTime = Database.GetLastSyncTime()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting sync status: {ex.Message}");
                return new SyncStatus
                {
                    HasUnsyncedData = false,
                    UnsyncedInvoiceCount = 0,
                    IsOnline = false,
                    LastSyncTime = DateTime.MinValue
                };
            }
        }

        /// <summary>
        /// Force a manual sync
        /// </summary>
        public async System.Threading.Tasks.Task<bool> ForceSyncAsync()
        {
            if (!Database.IsInternetAvailable())
            {
                throw new Exception("No internet connection available.");
            }

            return await Database.CheckAndSyncAsync();
        }

    }

    public class StaffMember
    {
        public int StaffID { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class SyncStatus
    {
        public bool HasUnsyncedData { get; set; }
        public int UnsyncedInvoiceCount { get; set; }
        public bool IsOnline { get; set; }
        public DateTime LastSyncTime { get; set; }

        public string StatusMessage
        {
            get
            {
                if (!IsOnline)
                    return $"Offline - {UnsyncedInvoiceCount} invoices waiting to sync";
                if (HasUnsyncedData)
                    return $"Online - Syncing {UnsyncedInvoiceCount} invoices...";
                if (LastSyncTime != DateTime.MinValue)
                    return $"Synced - Last sync: {LastSyncTime:HH:mm:ss}";
                return "Ready";
            }
        }
    }
}