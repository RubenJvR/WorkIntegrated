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

                // Create invoice
                string query = @"
                    INSERT INTO INVOICEQUOTE (date, type, totalAmount, customerID, staffID)
                    VALUES (@date, @type, @totalAmount, @customerID, @staffID);
                    SELECT last_insert_rowid();";

                using var cmd = new SqliteCommand(query, conn);
                cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@type", type);

                // For refunds, store as negative amount
                double finalAmount = (double)(type == 3 ? -Math.Abs(totalAmount) : totalAmount);
                cmd.Parameters.AddWithValue("@totalAmount", finalAmount);

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

                // Get the invoice type to determine if it's a refund
                string typeQuery = "SELECT type FROM INVOICEQUOTE WHERE invoiceQuoteID = @invoiceId";
                using var typeCmd = new SqliteCommand(typeQuery, conn, transaction);
                typeCmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                int invoiceType = Convert.ToInt32(typeCmd.ExecuteScalar());

                bool isRefund = (invoiceType == 3);

                foreach (var item in items)
                {
                    if (item.Quantity > 0)
                    {
                        // Add invoice item - negative quantity for refunds
                        int quantity = isRefund ? -item.Quantity : item.Quantity;

                        string itemQuery = @"
                            INSERT INTO INVOICEITEM (quantity, priceAtSale, itemID, invoiceQuoteID)
                            VALUES (@quantity, @priceAtSale, @itemID, @invoiceQuoteID)";

                        using var itemCmd = new SqliteCommand(itemQuery, conn, transaction);
                        itemCmd.Parameters.AddWithValue("@quantity", quantity);
                        itemCmd.Parameters.AddWithValue("@priceAtSale", (double)item.Price);
                        itemCmd.Parameters.AddWithValue("@itemID", item.ItemID);
                        itemCmd.Parameters.AddWithValue("@invoiceQuoteID", invoiceId);
                        itemCmd.ExecuteNonQuery();

                        // Update stock - different logic for refunds vs sales
                        string stockQuery = isRefund ? @"
                            UPDATE ITEM 
                            SET stockQuantity = stockQuantity + @quantity,
                                stockSold = stockSold - @quantity
                            WHERE itemID = @itemID" : @"
                            UPDATE ITEM 
                            SET stockQuantity = stockQuantity - @quantity,
                                stockSold = stockSold + @quantity
                            WHERE itemID = @itemID";

                        using var stockCmd = new SqliteCommand(stockQuery, conn, transaction);
                        stockCmd.Parameters.AddWithValue("@quantity", item.Quantity);
                        stockCmd.Parameters.AddWithValue("@itemID", item.ItemID);
                        stockCmd.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
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

        // Method to process refund specifically
        public void ProcessRefund(int invoiceId, List<POSItem> refundItems)
        {
            SqliteConnection conn = null;
            SqliteTransaction transaction = null;

            try
            {
                conn = new SqliteConnection(_connectionString);
                conn.Open();

                transaction = conn.BeginTransaction();

                foreach (var item in refundItems)
                {
                    if (item.Quantity > 0)
                    {
                        // Add refund item (negative quantity)
                        string itemQuery = @"
                            INSERT INTO INVOICEITEM (quantity, priceAtSale, itemID, invoiceQuoteID)
                            VALUES (@quantity, @priceAtSale, @itemID, @invoiceQuoteID)";

                        using var itemCmd = new SqliteCommand(itemQuery, conn, transaction);
                        itemCmd.Parameters.AddWithValue("@quantity", -item.Quantity); // Negative quantity for refund
                        itemCmd.Parameters.AddWithValue("@priceAtSale", (double)item.Price);
                        itemCmd.Parameters.AddWithValue("@itemID", item.ItemID);
                        itemCmd.Parameters.AddWithValue("@invoiceQuoteID", invoiceId);
                        itemCmd.ExecuteNonQuery();

                        // Update stock (increase stock for refund)
                        string stockQuery = @"
                            UPDATE ITEM 
                            SET stockQuantity = stockQuantity + @quantity,
                                stockSold = stockSold - @quantity
                            WHERE itemID = @itemID";

                        using var stockCmd = new SqliteCommand(stockQuery, conn, transaction);
                        stockCmd.Parameters.AddWithValue("@quantity", item.Quantity);
                        stockCmd.Parameters.AddWithValue("@itemID", item.ItemID);
                        stockCmd.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction?.Rollback();
                throw new Exception($"Error processing refund: {ex.Message}", ex);
            }
            finally
            {
                transaction?.Dispose();
                conn?.Dispose();
            }
        }

        // Method to update item stock (for refunds)
        public void UpdateItemStock(int itemId, int quantity)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                string query = @"
                    UPDATE ITEM 
                    SET stockQuantity = stockQuantity + @quantity,
                        stockSold = stockSold - @quantity
                    WHERE itemID = @itemID";

                using var cmd = new SqliteCommand(query, conn);
                cmd.Parameters.AddWithValue("@quantity", quantity);
                cmd.Parameters.AddWithValue("@itemID", itemId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating item stock: {ex.Message}", ex);
            }
        }

        // Method to find original sale for refund
        public int? FindOriginalSale(string customerName, DateTime saleDate)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                string query = @"
                    SELECT iq.invoiceQuoteID 
                    FROM INVOICEQUOTE iq
                    JOIN CUSTOMER c ON iq.customerID = c.customerID
                    WHERE c.name = @customerName 
                    AND iq.date >= @startDate 
                    AND iq.date <= @endDate
                    AND iq.type = 1  -- Sale type
                    ORDER BY iq.date DESC 
                    LIMIT 1";

                using var cmd = new SqliteCommand(query, conn);
                cmd.Parameters.AddWithValue("@customerName", customerName);
                cmd.Parameters.AddWithValue("@startDate", saleDate.AddDays(-7).ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@endDate", saleDate.ToString("yyyy-MM-dd HH:mm:ss"));

                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error finding original sale: {ex.Message}", ex);
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

            // Create new customer
            string insertQuery = @"
                INSERT INTO CUSTOMER (name, phone, email, credit)
                VALUES (@name, '', '', 0);
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
}