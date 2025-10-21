using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using System;
using System.IO;
using System.Data.Common;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ADIX
{
    public static class Database
    {
        private const string SqliteConnectionString = "Data Source=ADIX.db";
        public static string AzureSqlConnectionString { get; set; } = "";
        public static DatabaseType CurrentDatabaseType { get; set; } = DatabaseType.SQLite;

        private static bool _syncRequired = false;
        private static DateTime _lastSyncTime = DateTime.MinValue;

        public enum DatabaseType
        {
            SQLite,
            AzureSQL
        }

        /// <summary>
        /// Checks if the device has internet connectivity
        /// </summary>
        public static bool IsInternetAvailable()
        {
            try
            {
                using (var ping = new Ping())
                {
                    var result = ping.Send("8.8.8.8", 3000);
                    return result.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks internet and attempts to sync if available
        /// </summary>
        public static async Task<bool> CheckAndSyncAsync()
        {
            if (string.IsNullOrEmpty(AzureSqlConnectionString))
            {
                Console.WriteLine("Azure SQL connection string not configured. Skipping sync.");
                return false;
            }

            if (!IsInternetAvailable())
            {
                Console.WriteLine("No internet connection. Sync skipped.");
                _syncRequired = true;
                return false;
            }

            try
            {
                await SyncTransactionBasedAsync();
                _syncRequired = false;
                _lastSyncTime = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sync failed: {ex.Message}");
                _syncRequired = true;
                return false;
            }
        }

        /// <summary>
        /// Initialize database - creates tables and syncs if internet is available
        /// </summary>
        public static async Task InitializeAsync()
        {
            try
            {
                InitializeSQLite();

                if (!string.IsNullOrEmpty(AzureSqlConnectionString) && IsInternetAvailable())
                {
                    try
                    {
                        InitializeAzureSQL();
                        await SyncTransactionBasedAsync();
                        _lastSyncTime = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Azure SQL initialization failed: {ex.Message}");
                        Console.WriteLine("Continuing with local database only.");
                    }
                }
                else
                {
                    Console.WriteLine("Operating in offline mode. Data will sync when internet is available.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Database initialization failed: {ex.Message}", ex);
            }
        }

        public static void Initialize()
        {
            InitializeAsync().Wait();
        }

        private static void InitializeSQLite()
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            using var pragmaCmd = new SqliteCommand("PRAGMA foreign_keys = ON;", connection);
            pragmaCmd.ExecuteNonQuery();

            string checkQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name='SELLER'";
            using var checkCmd = new SqliteCommand(checkQuery, connection);
            var result = checkCmd.ExecuteScalar();

            if (result == null)
            {
                CreateSQLiteTables(connection);
                InsertTestDataSQLite(connection);
            }
        }

        private static void InitializeAzureSQL()
        {
            using var connection = new SqlConnection(AzureSqlConnectionString);
            connection.Open();

            string checkQuery = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SELLER'";
            using var checkCmd = new SqlCommand(checkQuery, connection);
            var result = checkCmd.ExecuteScalar();

            if (result == null)
            {
                CreateAzureSQLTables(connection);
                InsertTestDataAzureSQL(connection);
            }
        }

        private static void CreateSQLiteTables(SqliteConnection connection)
        {
            string createTablesSql = @"
                CREATE TABLE IF NOT EXISTS SELLER(
                    sellerID INTEGER NOT NULL PRIMARY KEY,
                    name TEXT NOT NULL,
                    contactInfo TEXT,
                    bankDetails TEXT,
                    commissionRate REAL CHECK(commissionRate >= 0 AND commissionRate <= 1),
                    lastModified TEXT DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS SUPPLIER(
                    supplierID INTEGER NOT NULL PRIMARY KEY,
                    name TEXT NOT NULL,
                    contactInfo TEXT,
                    address TEXT,
                    lastModified TEXT DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS ITEM(
                    itemID INTEGER NOT NULL PRIMARY KEY,
                    description TEXT NOT NULL,
                    retailPrice REAL NOT NULL CHECK(retailPrice >= 0),
                    costPrice REAL NOT NULL CHECK(costPrice >= 0),
                    stockQuantity INTEGER NOT NULL DEFAULT 0 CHECK(stockQuantity >= 0),
                    stockSold INTEGER NOT NULL DEFAULT 0 CHECK(stockSold >= 0),
                    supplierID INTEGER,
                    sellerID INTEGER,
                    lastModified TEXT DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(supplierID) REFERENCES SUPPLIER(supplierID),
                    FOREIGN KEY(sellerID) REFERENCES SELLER(sellerID)
                );

                CREATE TABLE IF NOT EXISTS CUSTOMER(
                    customerID INTEGER NOT NULL PRIMARY KEY,
                    name TEXT NOT NULL,
                    phone TEXT,
                    email TEXT,
                    credit REAL DEFAULT 0,
                    lastModified TEXT DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS STAFF(
                    staffID INTEGER NOT NULL PRIMARY KEY,
                    name TEXT NOT NULL,
                    Role TEXT,
                    userName TEXT UNIQUE,
                    passwordHash TEXT,
                    salary REAL,
                    lastModified TEXT DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS INVOICEQUOTE(
                    invoiceQuoteID INTEGER NOT NULL PRIMARY KEY,
                    date TEXT NOT NULL,
                    type INTEGER NOT NULL CHECK(type IN (1,2)),
                    totalAmount REAL NOT NULL,
                    customerID INTEGER,
                    staffID INTEGER NOT NULL,
                    synced INTEGER DEFAULT 0,
                    lastModified TEXT DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(customerID) REFERENCES CUSTOMER(customerID),
                    FOREIGN KEY(staffID) REFERENCES STAFF(staffID)
                );

                CREATE TABLE IF NOT EXISTS REPORT(
                    reportID INTEGER NOT NULL PRIMARY KEY,
                    reportType INTEGER,
                    date TEXT,
                    staffID INTEGER,
                    lastModified TEXT DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(staffID) REFERENCES STAFF(staffID)
                );

                CREATE TABLE IF NOT EXISTS INVOICEITEM(
                    invoiceItemID INTEGER NOT NULL PRIMARY KEY,
                    quantity INTEGER NOT NULL CHECK(quantity > 0),
                    priceAtSale REAL NOT NULL CHECK(priceAtSale >= 0),
                    itemID INTEGER NOT NULL,
                    invoiceQuoteID INTEGER NOT NULL,
                    synced INTEGER DEFAULT 0,
                    lastModified TEXT DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(invoiceQuoteID) REFERENCES INVOICEQUOTE(invoiceQuoteID),
                    FOREIGN KEY(itemID) REFERENCES ITEM(itemID)
                );

                CREATE TABLE IF NOT EXISTS SYNC_LOG(
                    syncLogID INTEGER PRIMARY KEY AUTOINCREMENT,
                    tableName TEXT NOT NULL,
                    recordID INTEGER NOT NULL,
                    operation TEXT NOT NULL,
                    syncedToAzure INTEGER DEFAULT 0,
                    timestamp TEXT DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS idx_item_supplier ON ITEM(supplierID);
                CREATE INDEX IF NOT EXISTS idx_item_seller ON ITEM(sellerID);
                CREATE INDEX IF NOT EXISTS idx_invoice_customer ON INVOICEQUOTE(customerID);
                CREATE INDEX IF NOT EXISTS idx_invoice_staff ON INVOICEQUOTE(staffID);
                CREATE INDEX IF NOT EXISTS idx_invoice_date ON INVOICEQUOTE(date);
                CREATE INDEX IF NOT EXISTS idx_invoiceitem_invoice ON INVOICEITEM(invoiceQuoteID);
                CREATE INDEX IF NOT EXISTS idx_invoiceitem_item ON INVOICEITEM(itemID);
                CREATE INDEX IF NOT EXISTS idx_sync_log_synced ON SYNC_LOG(syncedToAzure);
            ";

            using var cmd = new SqliteCommand(createTablesSql, connection);
            cmd.ExecuteNonQuery();
        }

        private static void CreateAzureSQLTables(SqlConnection connection)
        {
            string createTablesSql = @"
                CREATE TABLE SELLER(
                    sellerID INT NOT NULL PRIMARY KEY,
                    name NVARCHAR(255) NOT NULL,
                    contactInfo NVARCHAR(255),
                    bankDetails NVARCHAR(255),
                    commissionRate FLOAT CHECK(commissionRate >= 0 AND commissionRate <= 1),
                    lastModified DATETIME DEFAULT GETUTCDATE()
                );

                CREATE TABLE SUPPLIER(
                    supplierID INT NOT NULL PRIMARY KEY,
                    name NVARCHAR(255) NOT NULL,
                    contactInfo NVARCHAR(255),
                    address NVARCHAR(500),
                    lastModified DATETIME DEFAULT GETUTCDATE()
                );

                CREATE TABLE ITEM(
                    itemID INT NOT NULL PRIMARY KEY,
                    description NVARCHAR(500) NOT NULL,
                    retailPrice FLOAT NOT NULL CHECK(retailPrice >= 0),
                    costPrice FLOAT NOT NULL CHECK(costPrice >= 0),
                    stockQuantity INT NOT NULL DEFAULT 0 CHECK(stockQuantity >= 0),
                    stockSold INT NOT NULL DEFAULT 0 CHECK(stockSold >= 0),
                    supplierID INT,
                    sellerID INT,
                    lastModified DATETIME DEFAULT GETUTCDATE(),
                    FOREIGN KEY(supplierID) REFERENCES SUPPLIER(supplierID),
                    FOREIGN KEY(sellerID) REFERENCES SELLER(sellerID)
                );

                CREATE TABLE CUSTOMER(
                    customerID INT NOT NULL PRIMARY KEY,
                    name NVARCHAR(255) NOT NULL,
                    phone NVARCHAR(50),
                    email NVARCHAR(255),
                    credit FLOAT DEFAULT 0,
                    lastModified DATETIME DEFAULT GETUTCDATE()
                );

                CREATE TABLE STAFF(
                    staffID INT NOT NULL PRIMARY KEY,
                    name NVARCHAR(255) NOT NULL,
                    Role NVARCHAR(100),
                    userName NVARCHAR(100) UNIQUE,
                    passwordHash NVARCHAR(255),
                    salary FLOAT,
                    lastModified DATETIME DEFAULT GETUTCDATE()
                );

                CREATE TABLE INVOICEQUOTE(
                    invoiceQuoteID INT NOT NULL PRIMARY KEY,
                    date DATETIME NOT NULL,
                    type INT NOT NULL CHECK(type IN (1,2)),
                    totalAmount FLOAT NOT NULL,
                    customerID INT,
                    staffID INT NOT NULL,
                    lastModified DATETIME DEFAULT GETUTCDATE(),
                    FOREIGN KEY(customerID) REFERENCES CUSTOMER(customerID),
                    FOREIGN KEY(staffID) REFERENCES STAFF(staffID)
                );

                CREATE TABLE REPORT(
                    reportID INT NOT NULL PRIMARY KEY,
                    reportType INT,
                    date DATETIME,
                    staffID INT,
                    lastModified DATETIME DEFAULT GETUTCDATE(),
                    FOREIGN KEY(staffID) REFERENCES STAFF(staffID)
                );

                CREATE TABLE INVOICEITEM(
                    invoiceItemID INT NOT NULL PRIMARY KEY,
                    quantity INT NOT NULL CHECK(quantity > 0),
                    priceAtSale FLOAT NOT NULL CHECK(priceAtSale >= 0),
                    itemID INT NOT NULL,
                    invoiceQuoteID INT NOT NULL,
                    lastModified DATETIME DEFAULT GETUTCDATE(),
                    FOREIGN KEY(invoiceQuoteID) REFERENCES INVOICEQUOTE(invoiceQuoteID),
                    FOREIGN KEY(itemID) REFERENCES ITEM(itemID)
                );

                CREATE INDEX idx_item_supplier ON ITEM(supplierID);
                CREATE INDEX idx_item_seller ON ITEM(sellerID);
                CREATE INDEX idx_invoice_customer ON INVOICEQUOTE(customerID);
                CREATE INDEX idx_invoice_staff ON INVOICEQUOTE(staffID);
                CREATE INDEX idx_invoice_date ON INVOICEQUOTE(date);
                CREATE INDEX idx_invoiceitem_invoice ON INVOICEITEM(invoiceQuoteID);
                CREATE INDEX idx_invoiceitem_item ON INVOICEITEM(itemID);
            ";

            using var cmd = new SqlCommand(createTablesSql, connection);
            cmd.ExecuteNonQuery();
        }

        private static void InsertTestDataSQLite(SqliteConnection connection)
        {
            string insertDataSql = @"
                INSERT INTO SELLER (name, contactInfo, bankDetails, commissionRate) VALUES
                ('John Doe', 'john@example.com', '12345678', 0.05),
                ('Jane Smith', 'jane@example.com', '87654321', 0.07);

                INSERT INTO SUPPLIER (name, contactInfo, address) VALUES
                ('GreenFoods Ltd', 'greenfoods@example.com', '123 Green St'),
                ('BeverageCorp', 'info@beveragecorp.com', '456 Juice Ave'),
                ('SnackSupply Co', 'snacks@example.com', '789 Snack Road');

                INSERT INTO ITEM (description, retailPrice, costPrice, stockQuantity, stockSold, supplierID, sellerID) VALUES
                ('Apples (1kg)', 25.50, 15.00, 50, 0, 1, 1),
                ('Orange Juice (1L)', 35.00, 20.00, 30, 0, 2, 2),
                ('Chips (Large)', 15.00, 8.00, 80, 0, 3, 1),
                ('Bananas (1kg)', 20.00, 12.00, 40, 0, 1, 2),
                ('Cola (330ml)', 12.50, 7.00, 100, 0, 2, 1),
                ('Bread (Loaf)', 18.00, 10.00, 60, 0, 1, 1),
                ('Milk (1L)', 22.00, 14.00, 45, 0, 2, 2),
                ('Chocolate Bar', 8.50, 5.00, 120, 0, 3, 1),
                ('Water (500ml)', 9.00, 5.50, 150, 0, 2, 1),
                ('Coffee (250g)', 65.00, 40.00, 25, 0, 1, 2);

                INSERT INTO STAFF (name, Role, userName, passwordHash, salary) VALUES
                ('Ruben Janse', 'Admin', 'ruben', 'hashedpassword1', 15000),
                ('Sarah Ndlovu', 'Cashier', 'sarah', 'hashedpassword2', 9000),
                ('Michael Smith', 'Manager', 'michael', 'hashedpassword3', 12000),
                ('Emily Johnson', 'Cashier', 'emily', 'hashedpassword4', 9000);

                INSERT INTO CUSTOMER (name, phone, email, credit) VALUES
                ('Alice Brown', '0712345678', 'alice@example.com', 100),
                ('Bob White', '0723456789', 'bob@example.com', 50),
                ('Charlie Green', '0734567890', 'charlie@example.com', 75);
            ";

            using var cmd = new SqliteCommand(insertDataSql, connection);
            cmd.ExecuteNonQuery();
        }

        private static void InsertTestDataAzureSQL(SqlConnection connection)
        {
            string insertDataSql = @"
                INSERT INTO CUSTOMER (customerID, name, phone, email, credit) VALUES
                (1, 'Alice Brown', '0712345678', 'alice@example.com', 100),
                (2, 'Bob White', '0723456789', 'bob@example.com', 50),
                (3, 'Charlie Green', '0734567890', 'charlie@example.com', 75);

                INSERT INTO STAFF (staffID, name, Role, userName, passwordHash, salary) VALUES
                (1, 'Ruben Janse', 'Admin', 'ruben', 'hashedpassword1', 15000),
                (2, 'Sarah Ndlovu', 'Cashier', 'sarah', 'hashedpassword2', 9000),
                (3, 'Michael Smith', 'Manager', 'michael', 'hashedpassword3', 12000),
                (4, 'Emily Johnson', 'Cashier', 'emily', 'hashedpassword4', 9000);

                INSERT INTO SELLER (sellerID, name, contactInfo, bankDetails, commissionRate) VALUES
                (1, 'John Doe', 'john@example.com', '12345678', 0.05),
                (2, 'Jane Smith', 'jane@example.com', '87654321', 0.07);

                INSERT INTO SUPPLIER (supplierID, name, contactInfo, address) VALUES
                (1, 'GreenFoods Ltd', 'greenfoods@example.com', '123 Green St'),
                (2, 'BeverageCorp', 'info@beveragecorp.com', '456 Juice Ave'),
                (3, 'SnackSupply Co', 'snacks@example.com', '789 Snack Road');

                INSERT INTO ITEM (itemID, description, retailPrice, costPrice, stockQuantity, stockSold, supplierID, sellerID) VALUES
                (1, 'Apples (1kg)', 25.50, 15.00, 50, 0, 1, 1),
                (2, 'Orange Juice (1L)', 35.00, 20.00, 30, 0, 2, 2),
                (3, 'Chips (Large)', 15.00, 8.00, 80, 0, 3, 1),
                (4, 'Bananas (1kg)', 20.00, 12.00, 40, 0, 1, 2),
                (5, 'Cola (330ml)', 12.50, 7.00, 100, 0, 2, 1),
                (6, 'Bread (Loaf)', 18.00, 10.00, 60, 0, 1, 1),
                (7, 'Milk (1L)', 22.00, 14.00, 45, 0, 2, 2),
                (8, 'Chocolate Bar', 8.50, 5.00, 120, 0, 3, 1),
                (9, 'Water (500ml)', 9.00, 5.50, 150, 0, 2, 1),
                (10, 'Coffee (250g)', 65.00, 40.00, 25, 0, 1, 2);

                INSERT INTO INVOICEQUOTE (invoiceQuoteID, date, type, totalAmount, customerID, staffID) VALUES
                (1, '2025-10-17', 1, 200, 1, 1),
                (2, '2025-10-17', 2, 150, 2, 2);

                INSERT INTO REPORT (reportID, reportType, date, staffID) VALUES
                (1, 1, '2025-10-17', 1),
                (2, 2, '2025-10-17', 2);
            ";

            using var cmd = new SqlCommand(insertDataSql, connection);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Transaction-based sync: Syncs transactions and recalculates inventory
        /// </summary>
        public static async Task SyncTransactionBasedAsync()
        {
            if (string.IsNullOrEmpty(AzureSqlConnectionString))
            {
                throw new InvalidOperationException("Azure SQL connection string is not configured.");
            }

            await Task.Run(() => SyncTransactionBased());
        }

        private static void SyncTransactionBased()
        {
            using var sqliteConn = new SqliteConnection(SqliteConnectionString);
            using var azureConn = new SqlConnection(AzureSqlConnectionString);

            sqliteConn.Open();
            azureConn.Open();

            Console.WriteLine("Starting transaction-based sync...");

            // Step 1: Sync master data (bidirectional - newest wins)
            SyncMasterData(sqliteConn, azureConn, "SELLER", new[] { "sellerID", "name", "contactInfo", "bankDetails", "commissionRate", "lastModified" });
            SyncMasterData(sqliteConn, azureConn, "SUPPLIER", new[] { "supplierID", "name", "contactInfo", "address", "lastModified" });
            SyncMasterData(sqliteConn, azureConn, "CUSTOMER", new[] { "customerID", "name", "phone", "email", "credit", "lastModified" });
            SyncMasterData(sqliteConn, azureConn, "STAFF", new[] { "staffID", "name", "Role", "userName", "passwordHash", "salary", "lastModified" });

            // Step 2: Sync item master data (prices, descriptions) but NOT quantities
            SyncItemMasterData(sqliteConn, azureConn);

            // Step 3: Sync transactions (invoices and invoice items)
            SyncTransactions(sqliteConn, azureConn);

            // Step 4: Recalculate inventory from transactions
            RecalculateInventory(sqliteConn, azureConn);

            // Step 5: Sync reports
            SyncMasterData(sqliteConn, azureConn, "REPORT", new[] { "reportID", "reportType", "date", "staffID", "lastModified" });

            Console.WriteLine("Transaction-based sync completed successfully.");
        }

        private static void SyncMasterData(SqliteConnection sqliteConn, SqlConnection azureConn, string tableName, string[] columns)
        {
            // Get data from both databases
            var localData = GetTableData(sqliteConn, tableName, columns);
            var azureData = GetTableDataFromAzure(azureConn, tableName, columns);

            using var transaction = azureConn.BeginTransaction();
            try
            {
                foreach (DataRow localRow in localData.Rows)
                {
                    var primaryKey = localRow[columns[0]];
                    var localModified = DateTime.Parse(localRow["lastModified"].ToString());

                    // Find matching row in Azure
                    var azureRow = azureData.AsEnumerable().FirstOrDefault(r => r[columns[0]].ToString() == primaryKey.ToString());

                    if (azureRow == null)
                    {
                        // Insert new record to Azure
                        InsertToAzure(azureConn, transaction, tableName, columns, localRow);
                        Console.WriteLine($"[{tableName}] Inserted new record {primaryKey} to Azure");
                    }
                    else
                    {
                        var azureModified = DateTime.Parse(azureRow["lastModified"].ToString());

                        if (localModified > azureModified)
                        {
                            // Local is newer - update Azure
                            UpdateAzure(azureConn, transaction, tableName, columns, localRow);
                            Console.WriteLine($"[{tableName}] Updated Azure record {primaryKey} (local newer)");
                        }
                        else if (azureModified > localModified)
                        {
                            // Azure is newer - update local
                            UpdateLocal(sqliteConn, tableName, columns, azureRow);
                            Console.WriteLine($"[{tableName}] Updated local record {primaryKey} (Azure newer)");
                        }
                    }
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new Exception($"Failed to sync {tableName}: {ex.Message}", ex);
            }
        }

        private static void SyncItemMasterData(SqliteConnection sqliteConn, SqlConnection azureConn)
        {
            // Sync item data except stockQuantity and stockSold
            string[] columns = { "itemID", "description", "retailPrice", "costPrice", "supplierID", "sellerID", "lastModified" };

            var localData = GetTableData(sqliteConn, "ITEM", columns);
            var azureData = GetTableDataFromAzure(azureConn, "ITEM", columns);

            using var transaction = azureConn.BeginTransaction();
            try
            {
                foreach (DataRow localRow in localData.Rows)
                {
                    var itemID = localRow["itemID"];
                    var azureRow = azureData.AsEnumerable().FirstOrDefault(r => r["itemID"].ToString() == itemID.ToString());

                    if (azureRow == null)
                    {
                        // New item - insert with zero quantities
                        var insertSql = $@"INSERT INTO ITEM (itemID, description, retailPrice, costPrice, stockQuantity, stockSold, supplierID, sellerID) 
                                          VALUES (@itemID, @description, @retailPrice, @costPrice, 0, 0, @supplierID, @sellerID)";
                        using var cmd = new SqlCommand(insertSql, azureConn, transaction);
                        cmd.Parameters.AddWithValue("@itemID", itemID);
                        cmd.Parameters.AddWithValue("@description", localRow["description"]);
                        cmd.Parameters.AddWithValue("@retailPrice", localRow["retailPrice"]);
                        cmd.Parameters.AddWithValue("@costPrice", localRow["costPrice"]);
                        cmd.Parameters.AddWithValue("@supplierID", localRow["supplierID"] == DBNull.Value ? null : localRow["supplierID"]);
                        cmd.Parameters.AddWithValue("@sellerID", localRow["sellerID"] == DBNull.Value ? null : localRow["sellerID"]);
                        cmd.ExecuteNonQuery();
                        Console.WriteLine($"[ITEM] Added new item {itemID} to Azure");
                    }
                    else
                    {
                        // Update item details but not quantities
                        var updateSql = $@"UPDATE ITEM SET description=@description, retailPrice=@retailPrice, 
                                          costPrice=@costPrice, supplierID=@supplierID, sellerID=@sellerID 
                                          WHERE itemID=@itemID";
                        using var cmd = new SqlCommand(updateSql, azureConn, transaction);
                        cmd.Parameters.AddWithValue("@itemID", itemID);
                        cmd.Parameters.AddWithValue("@description", localRow["description"]);
                        cmd.Parameters.AddWithValue("@retailPrice", localRow["retailPrice"]);
                        cmd.Parameters.AddWithValue("@costPrice", localRow["costPrice"]);
                        cmd.Parameters.AddWithValue("@supplierID", localRow["supplierID"] == DBNull.Value ? null : localRow["supplierID"]);
                        cmd.Parameters.AddWithValue("@sellerID", localRow["sellerID"] == DBNull.Value ? null : localRow["sellerID"]);
                        cmd.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new Exception($"Failed to sync ITEM master data: {ex.Message}", ex);
            }
        }



        private static void SyncTransactions(SqliteConnection sqliteConn, SqlConnection azureConn)
        {
            // Sync unsynced invoices
            var localInvoices = new DataTable();
            using (var cmd = new SqliteCommand("SELECT * FROM INVOICEQUOTE WHERE synced = 0", sqliteConn))
            using (var reader = cmd.ExecuteReader())
            {
                localInvoices.Load(reader);
            }

            using var transaction = azureConn.BeginTransaction();
            try
            {
                foreach (DataRow invoice in localInvoices.Rows)
                {
                    // Check if invoice exists in Azure
                    var checkSql = "SELECT COUNT(*) FROM INVOICEQUOTE WHERE invoiceQuoteID = @id";
                    using var checkCmd = new SqlCommand(checkSql, azureConn, transaction);
                    checkCmd.Parameters.AddWithValue("@id", invoice["invoiceQuoteID"]);
                    var exists = (int)checkCmd.ExecuteScalar() > 0;

                    if (!exists)
                    {
                        // Insert invoice
                        var insertSql = @"INSERT INTO INVOICEQUOTE (invoiceQuoteID, date, type, totalAmount, customerID, staffID) 
                                         VALUES (@id, @date, @type, @amount, @custID, @staffID)";
                        using var insertCmd = new SqlCommand(insertSql, azureConn, transaction);
                        insertCmd.Parameters.AddWithValue("@id", invoice["invoiceQuoteID"]);
                        insertCmd.Parameters.AddWithValue("@date", invoice["date"]);
                        insertCmd.Parameters.AddWithValue("@type", invoice["type"]);
                        insertCmd.Parameters.AddWithValue("@amount", invoice["totalAmount"]);
                        insertCmd.Parameters.AddWithValue("@custID", invoice["customerID"] == DBNull.Value ? null : invoice["customerID"]);
                        insertCmd.Parameters.AddWithValue("@staffID", invoice["staffID"]);
                        insertCmd.ExecuteNonQuery();

                        Console.WriteLine($"[INVOICE] Synced invoice {invoice["invoiceQuoteID"]} to Azure");

                        // Sync invoice items
                        SyncInvoiceItems(sqliteConn, azureConn, transaction, (long)invoice["invoiceQuoteID"]);

                        // Mark as synced locally
                        using var updateCmd = new SqliteCommand("UPDATE INVOICEQUOTE SET synced = 1 WHERE invoiceQuoteID = @id", sqliteConn);
                        updateCmd.Parameters.AddWithValue("@id", invoice["invoiceQuoteID"]);
                        updateCmd.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new Exception($"Failed to sync transactions: {ex.Message}", ex);
            }
        }

        private static void SyncInvoiceItems(SqliteConnection sqliteConn, SqlConnection azureConn, SqlTransaction transaction, long invoiceID)
        {
            var items = new DataTable();
            using (var cmd = new SqliteCommand("SELECT * FROM INVOICEITEM WHERE invoiceQuoteID = @id AND synced = 0", sqliteConn))
            {
                cmd.Parameters.AddWithValue("@id", invoiceID);
                using var reader = cmd.ExecuteReader();
                items.Load(reader);
            }

            foreach (DataRow item in items.Rows)
            {
                // Check if item exists in Azure
                var checkSql = "SELECT COUNT(*) FROM INVOICEITEM WHERE invoiceItemID = @id";
                using var checkCmd = new SqlCommand(checkSql, azureConn, transaction);
                checkCmd.Parameters.AddWithValue("@id", item["invoiceItemID"]);
                var exists = (int)checkCmd.ExecuteScalar() > 0;

                if (!exists)
                {
                    var insertSql = @"INSERT INTO INVOICEITEM (invoiceItemID, quantity, priceAtSale, itemID, invoiceQuoteID) 
                                     VALUES (@id, @qty, @price, @itemID, @invoiceID)";
                    using var insertCmd = new SqlCommand(insertSql, azureConn, transaction);
                    insertCmd.Parameters.AddWithValue("@id", item["invoiceItemID"]);
                    insertCmd.Parameters.AddWithValue("@qty", item["quantity"]);
                    insertCmd.Parameters.AddWithValue("@price", item["priceAtSale"]);
                    insertCmd.Parameters.AddWithValue("@itemID", item["itemID"]);
                    insertCmd.Parameters.AddWithValue("@invoiceID", item["invoiceQuoteID"]);
                    insertCmd.ExecuteNonQuery();

                    Console.WriteLine($"[INVOICEITEM] Synced item {item["invoiceItemID"]} to Azure");

                    // Mark as synced locally
                    using var updateCmd = new SqliteCommand("UPDATE INVOICEITEM SET synced = 1 WHERE invoiceItemID = @id", sqliteConn);
                    updateCmd.Parameters.AddWithValue("@id", item["invoiceItemID"]);
                    updateCmd.ExecuteNonQuery();
                }
            }
        }

        private static void RecalculateInventory(SqliteConnection sqliteConn, SqlConnection azureConn)
        {
            Console.WriteLine("Recalculating inventory from transactions...");

            // Get all items from Azure
            var items = new DataTable();
            using (var cmd = new SqlCommand("SELECT itemID FROM ITEM", azureConn))
            using (var reader = cmd.ExecuteReader())
            {
                items.Load(reader);
            }

            using var transaction = azureConn.BeginTransaction();
            try
            {
                foreach (DataRow item in items.Rows)
                {
                    var itemID = item["itemID"];

                    // Calculate total sold from invoice items (type 1 = invoice/sale)
                    var soldSql = @"SELECT COALESCE(SUM(ii.quantity), 0) 
                                   FROM INVOICEITEM ii
                                   INNER JOIN INVOICEQUOTE iq ON ii.invoiceQuoteID = iq.invoiceQuoteID
                                   WHERE ii.itemID = @itemID AND iq.type = 1";

                    int totalSold = 0;
                    using (var soldCmd = new SqlCommand(soldSql, azureConn, transaction))
                    {
                        soldCmd.Parameters.AddWithValue("@itemID", itemID);
                        totalSold = Convert.ToInt32(soldCmd.ExecuteScalar());
                    }

                    // Get initial stock (you might want to track this separately)
                    // For now, we'll use current stockQuantity + stockSold as initial
                    var getCurrentSql = "SELECT stockQuantity, stockSold FROM ITEM WHERE itemID = @itemID";
                    int currentQty = 0;
                    int currentSold = 0;
                    using (var getCurrentCmd = new SqlCommand(getCurrentSql, azureConn, transaction))
                    {
                        getCurrentCmd.Parameters.AddWithValue("@itemID", itemID);
                        using var reader = getCurrentCmd.ExecuteReader();
                        if (reader.Read())
                        {
                            currentQty = reader.GetInt32(0);
                            currentSold = reader.GetInt32(1);
                        }
                    }

                    // Calculate initial stock
                    int initialStock = currentQty + currentSold;

                    // Update item with recalculated values
                    var updateSql = @"UPDATE ITEM 
                                     SET stockSold = @sold, 
                                         stockQuantity = @quantity 
                                     WHERE itemID = @itemID";
                    using var updateCmd = new SqlCommand(updateSql, azureConn, transaction);
                    updateCmd.Parameters.AddWithValue("@sold", totalSold);
                    updateCmd.Parameters.AddWithValue("@quantity", Math.Max(0, initialStock - totalSold));
                    updateCmd.Parameters.AddWithValue("@itemID", itemID);
                    updateCmd.ExecuteNonQuery();

                    Console.WriteLine($"[ITEM {itemID}] Recalculated: Initial={initialStock}, Sold={totalSold}, Remaining={Math.Max(0, initialStock - totalSold)}");
                }

                // Now sync calculated quantities back to local
                SyncInventoryToLocal(sqliteConn, azureConn, transaction);

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new Exception($"Failed to recalculate inventory: {ex.Message}", ex);
            }
        }

        private static void SyncInventoryToLocal(SqliteConnection sqliteConn, SqlConnection azureConn, SqlTransaction transaction)
        {
            // Pull the recalculated inventory from Azure to local
            var items = new DataTable();
            using (var cmd = new SqlCommand("SELECT itemID, stockQuantity, stockSold FROM ITEM", azureConn, transaction))
            using (var reader = cmd.ExecuteReader())
            {
                items.Load(reader);
            }

            foreach (DataRow item in items.Rows)
            {
                var updateSql = "UPDATE ITEM SET stockQuantity = @qty, stockSold = @sold WHERE itemID = @id";
                using var updateCmd = new SqliteCommand(updateSql, sqliteConn);
                updateCmd.Parameters.AddWithValue("@qty", item["stockQuantity"]);
                updateCmd.Parameters.AddWithValue("@sold", item["stockSold"]);
                updateCmd.Parameters.AddWithValue("@id", item["itemID"]);
                updateCmd.ExecuteNonQuery();
            }

            Console.WriteLine("Synced recalculated inventory to local database");
        }

        private static DataTable GetTableData(SqliteConnection connection, string tableName, string[] columns)
        {
            var dataTable = new DataTable();
            var columnList = string.Join(", ", columns);
            using var cmd = new SqliteCommand($"SELECT {columnList} FROM {tableName}", connection);
            using var reader = cmd.ExecuteReader();
            dataTable.Load(reader);
            return dataTable;
        }

        private static DataTable GetTableDataFromAzure(SqlConnection connection, string tableName, string[] columns)
        {
            var dataTable = new DataTable();
            var columnList = string.Join(", ", columns);
            using var cmd = new SqlCommand($"SELECT {columnList} FROM {tableName}", connection);
            using var reader = cmd.ExecuteReader();
            dataTable.Load(reader);
            return dataTable;
        }

        private static void InsertToAzure(SqlConnection connection, SqlTransaction transaction, string tableName, string[] columns, DataRow row)
        {
            var columnList = string.Join(", ", columns);
            var paramList = string.Join(", ", columns.Select(c => $"@{c}"));
            var insertSql = $"INSERT INTO {tableName} ({columnList}) VALUES ({paramList})";

            using var cmd = new SqlCommand(insertSql, connection, transaction);
            foreach (var column in columns)
            {
                cmd.Parameters.AddWithValue($"@{column}", row[column] == DBNull.Value ? null : row[column]);
            }
            cmd.ExecuteNonQuery();
        }

        private static void UpdateAzure(SqlConnection connection, SqlTransaction transaction, string tableName, string[] columns, DataRow row)
        {
            var primaryKey = columns[0];
            var setClause = string.Join(", ", columns.Skip(1).Select(c => $"{c} = @{c}"));
            var updateSql = $"UPDATE {tableName} SET {setClause} WHERE {primaryKey} = @{primaryKey}";

            using var cmd = new SqlCommand(updateSql, connection, transaction);
            foreach (var column in columns)
            {
                cmd.Parameters.AddWithValue($"@{column}", row[column] == DBNull.Value ? null : row[column]);
            }
            cmd.ExecuteNonQuery();
        }

        private static void UpdateLocal(SqliteConnection connection, string tableName, string[] columns, DataRow row)
        {
            var primaryKey = columns[0];
            var setClause = string.Join(", ", columns.Skip(1).Select(c => $"{c} = @{c}"));
            var updateSql = $"UPDATE {tableName} SET {setClause} WHERE {primaryKey} = @{primaryKey}";

            using var cmd = new SqliteCommand(updateSql, connection);
            foreach (var column in columns)
            {
                cmd.Parameters.AddWithValue($"@{column}", row[column] == DBNull.Value ? null : row[column]);
            }
            cmd.ExecuteNonQuery();
        }

        public static void MarkSyncRequired()
        {
            _syncRequired = true;
        }

        public static bool IsSyncRequired()
        {
            return _syncRequired;
        }

        public static DateTime GetLastSyncTime()
        {
            return _lastSyncTime;
        }

        public static DbConnection GetConnection()
        {
            if (CurrentDatabaseType == DatabaseType.SQLite)
            {
                return new SqliteConnection(SqliteConnectionString);
            }
            else
            {
                return new SqlConnection(AzureSqlConnectionString);
            }
        }

        public static void PrintAzureTables()
        {
            if (string.IsNullOrEmpty(AzureSqlConnectionString))
            {
                Console.WriteLine("Azure SQL connection string is not configured.");
                return;
            }

            try
            {
                using var connection = new SqlConnection(AzureSqlConnectionString);
                connection.Open();

                string query = @"SELECT TABLE_NAME 
                         FROM INFORMATION_SCHEMA.TABLES 
                         WHERE TABLE_TYPE = 'BASE TABLE' 
                         ORDER BY TABLE_NAME;";

                using var command = new SqlCommand(query, connection);
                using var reader = command.ExecuteReader();

                Console.WriteLine("Tables in Azure SQL database:");
                while (reader.Read())
                {
                    Console.WriteLine(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving tables: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to add inventory (e.g., when receiving stock)
        /// </summary>
        public static void AddStock(int itemID, int quantity)
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            var updateSql = "UPDATE ITEM SET stockQuantity = stockQuantity + @qty, lastModified = CURRENT_TIMESTAMP WHERE itemID = @id";
            using var cmd = new SqliteCommand(updateSql, connection);
            cmd.Parameters.AddWithValue("@qty", quantity);
            cmd.Parameters.AddWithValue("@id", itemID);
            cmd.ExecuteNonQuery();

            MarkSyncRequired();
            Console.WriteLine($"Added {quantity} units to item {itemID}");
        }

        /// <summary>
        /// Helper method called when processing a sale (after creating invoice)
        /// </summary>
        public static void ProcessSale(long invoiceID)
        {
            // Mark invoice and items as needing sync
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            // Get invoice items
            var items = new DataTable();
            using (var cmd = new SqliteCommand("SELECT itemID, quantity FROM INVOICEITEM WHERE invoiceQuoteID = @id", connection))
            {
                cmd.Parameters.AddWithValue("@id", invoiceID);
                using var reader = cmd.ExecuteReader();
                items.Load(reader);
            }

            // Update local inventory
            foreach (DataRow item in items.Rows)
            {
                var updateSql = @"UPDATE ITEM 
                                 SET stockQuantity = stockQuantity - @qty, 
                                     stockSold = stockSold + @qty,
                                     lastModified = CURRENT_TIMESTAMP 
                                 WHERE itemID = @id";
                using var cmd = new SqliteCommand(updateSql, connection);
                cmd.Parameters.AddWithValue("@qty", item["quantity"]);
                cmd.Parameters.AddWithValue("@id", item["itemID"]);
                cmd.ExecuteNonQuery();
            }

            MarkSyncRequired();
            Console.WriteLine($"Processed sale for invoice {invoiceID}");
        }
    }
}