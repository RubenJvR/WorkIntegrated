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
        private static string _deviceId;
        public static void InitializeDeviceId()
        {
            // Generate or load unique device ID
            string deviceIdFile = "device_id.txt";

            if (File.Exists(deviceIdFile))
            {
                _deviceId = File.ReadAllText(deviceIdFile).Trim();
            }
            else
            {
                _deviceId = Guid.NewGuid().ToString("N").Substring(0, 8); // Short unique ID
                File.WriteAllText(deviceIdFile, _deviceId);
            }

            Console.WriteLine($"Device ID: {_deviceId}");
        }

        private const string SqliteConnectionString = "Data Source=ADIX.db";
        public static string AzureSqlConnectionString { get; set; } = "Server=tcp:adixserver.database.windows.net,1433;Initial Catalog=ADIXDB;User ID=adixAdmin;Password=A$12fe34dc56;Encrypt=True;";
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
            InitializeDeviceId();
            InitializeAsync().Wait();
        }
        public static long GetNextInvoiceNumber()
        {
            try
            {
                using var connection = new SqliteConnection(SqliteConnectionString);
                connection.Open();

                // Generate unique ID: deviceId (8 chars) + timestamp (milliseconds)
                // Example: ABC12345_1704123456789
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // For display purposes, also get a sequential number
                string query = "SELECT COALESCE(MAX(invoiceQuoteID), 0) + 1 FROM INVOICEQUOTE";
                using var cmd = new SqliteCommand(query, connection);
                long sequentialId = Convert.ToInt64(cmd.ExecuteScalar());

                // Use timestamp-based ID to avoid collisions
                // This ensures each device generates unique IDs even when offline
                return timestamp;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting invoice number: {ex.Message}", ex);
            }
        }


        
        private static void DownloadMissingItemsFromAzure(SqliteConnection sqliteConn, SqlConnection azureConn)
        {
            try
            {
                Console.WriteLine("[DOWNLOAD] Starting item download from Azure...");

                // Get all Azure items with full details including inventory
                var azureItems = GetTableDataFromAzure(azureConn, "ITEM",
                    new[] { "itemID", "description", "retailPrice", "costPrice", "stockQuantity", "stockSold", "stockRecieved", "supplierID", "sellerID", "lastModified" });

                Console.WriteLine($"[DOWNLOAD] Found {azureItems.Rows.Count} items in Azure");

                // Get all local items with full details
                var localItems = GetTableData(sqliteConn, "ITEM",
                    new[] { "itemID", "description", "stockQuantity", "stockSold", "stockRecieved", "lastModified" });

                Console.WriteLine($"[DOWNLOAD] Found {localItems.Rows.Count} items locally");

                var localItemsDict = localItems.AsEnumerable()
                    .ToDictionary(r => Convert.ToInt32(r["itemID"]), r => r);

                int newItems = 0;
                int updatedItems = 0;
                int skippedItems = 0;

                // Process each Azure item
                foreach (DataRow azureRow in azureItems.Rows)
                {
                    var itemId = Convert.ToInt32(azureRow["itemID"]);
                    string azureDesc = azureRow["description"]?.ToString() ?? "";

                    if (!localItemsDict.ContainsKey(itemId))
                    {
                        // NEW ITEM: This item exists in Azure but not locally - download it
                        var insertSql = @"
                    INSERT INTO ITEM 
                    (itemID, description, retailPrice, costPrice, stockQuantity, stockSold, stockRecieved, supplierID, sellerID, lastModified)
                    VALUES 
                    (@itemID, @description, @retailPrice, @costPrice, @stockQuantity, @stockSold, @stockRecieved, @supplierID, @sellerID, @lastModified)";

                        using var cmd = new SqliteCommand(insertSql, sqliteConn);
                        cmd.Parameters.AddWithValue("@itemID", itemId);
                        cmd.Parameters.AddWithValue("@description", azureRow["description"]);
                        cmd.Parameters.AddWithValue("@retailPrice", azureRow["retailPrice"]);
                        cmd.Parameters.AddWithValue("@costPrice", azureRow["costPrice"]);
                        cmd.Parameters.AddWithValue("@stockQuantity", azureRow["stockQuantity"]);
                        cmd.Parameters.AddWithValue("@stockSold", azureRow["stockSold"]);
                        cmd.Parameters.AddWithValue("@stockRecieved", azureRow["stockRecieved"]);
                        cmd.Parameters.AddWithValue("@supplierID", azureRow["supplierID"] == DBNull.Value ? (object)DBNull.Value : azureRow["supplierID"]);
                        cmd.Parameters.AddWithValue("@sellerID", azureRow["sellerID"] == DBNull.Value ? (object)DBNull.Value : azureRow["sellerID"]);
                        cmd.Parameters.AddWithValue("@lastModified", azureRow["lastModified"]);
                        cmd.ExecuteNonQuery();

                        newItems++;
                        Console.WriteLine($"[DOWNLOAD] NEW: Item {itemId} '{azureDesc}' (Qty: {azureRow["stockQuantity"]})");
                    }
                    else
                    {
                        // EXISTING ITEM: Check if Azure has newer data
                        var localRow = localItemsDict[itemId];
                        var azureModified = DateTime.Parse(azureRow["lastModified"].ToString());
                        var localModified = DateTime.Parse(localRow["lastModified"].ToString());

                        string localDesc = localRow["description"]?.ToString() ?? "";
                        int localQty = Convert.ToInt32(localRow["stockQuantity"]);
                        int azureQty = Convert.ToInt32(azureRow["stockQuantity"]);

                        Console.WriteLine($"[DOWNLOAD] Checking item {itemId} '{azureDesc}':");
                        Console.WriteLine($"  Local: Qty={localQty}, Modified={localModified:yyyy-MM-dd HH:mm:ss}");
                        Console.WriteLine($"  Azure: Qty={azureQty}, Modified={azureModified:yyyy-MM-dd HH:mm:ss}");

                        // If Azure is newer AND descriptions match (same item), update
                        if (localDesc.Equals(azureDesc, StringComparison.OrdinalIgnoreCase))
                        {
                            if (azureModified > localModified)
                            {
                                // Update local with Azure's data
                                var updateSql = @"
                            UPDATE ITEM 
                            SET stockQuantity = @stockQuantity,
                                stockSold = @stockSold,
                                stockRecieved = @stockRecieved,
                                retailPrice = @retailPrice,
                                costPrice = @costPrice,
                                lastModified = @lastModified
                            WHERE itemID = @itemID";

                                using var cmd = new SqliteCommand(updateSql, sqliteConn);
                                cmd.Parameters.AddWithValue("@itemID", itemId);
                                cmd.Parameters.AddWithValue("@stockQuantity", azureRow["stockQuantity"]);
                                cmd.Parameters.AddWithValue("@stockSold", azureRow["stockSold"]);
                                cmd.Parameters.AddWithValue("@stockRecieved", azureRow["stockRecieved"]);
                                cmd.Parameters.AddWithValue("@retailPrice", azureRow["retailPrice"]);
                                cmd.Parameters.AddWithValue("@costPrice", azureRow["costPrice"]);
                                cmd.Parameters.AddWithValue("@lastModified", azureRow["lastModified"]);
                                cmd.ExecuteNonQuery();

                                updatedItems++;
                                Console.WriteLine($"   UPDATED: {localQty} → {azureQty}");
                            }
                            else
                            {
                                skippedItems++;
                                Console.WriteLine($"  SKIPPED: Local is newer or same");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"   CONFLICT: Different descriptions!");
                            Console.WriteLine($"    Local: '{localDesc}'");
                            Console.WriteLine($"    Azure: '{azureDesc}'");
                        }
                    }
                }

                Console.WriteLine($"[DOWNLOAD SUMMARY] New: {newItems}, Updated: {updatedItems}, Skipped: {skippedItems}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading/updating items from Azure: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw;
            }
        }
        public static long GetNextItemID()
        {
            try
            {
                using var connection = new SqliteConnection(SqliteConnectionString);
                connection.Open();

                // Use timestamp-based ID to avoid collisions between offline devices
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // For items, use a smaller range to keep IDs manageable
                // Extract last 9 digits (still unique enough)
                long itemId = timestamp % 1000000000;

                return itemId;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting item ID: {ex.Message}", ex);
            }
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
            else
            {
                // Run migration for existing databases
                MigrateDatabase(connection);
            }
        }

        public static void InitializeStaffTableSQLite()
        {
            using var conn = new SqliteConnection(SqliteConnectionString);
            conn.Open();

            var createTableCmd = conn.CreateCommand();
            createTableCmd.CommandText =
            @"
            CREATE TABLE IF NOT EXISTS STAFF (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT NOT NULL UNIQUE,
                passwordhash TEXT NOT NULL
            );

            INSERT OR IGNORE INTO STAFF (username, passwordhash)
            VALUES ('Peter', 'passwordhash6');
            ";
            createTableCmd.ExecuteNonQuery();
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

        internal static bool ValidateUser(string username, string passwordhash)
        {
            using var conn = new SqliteConnection(SqliteConnectionString);
            conn.Open();

            string query = "SELECT COUNT(1) FROM STAFF WHERE username = @username AND passwordhash = @passwordhash";

            using var cmd = new SqliteCommand(query, conn);
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@passwordhash", passwordhash);

            var result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) > 0;
        }




        private static void CreateSQLiteTables(SqliteConnection connection)
        {
            string createTablesSql = @"
-- Drop tables in correct order to handle foreign key constraints

        
        CREATE TABLE IF NOT EXISTS SELLER(
            sellerID INTEGER NOT NULL PRIMARY KEY,
            name TEXT NOT NULL,
            contactInfo TEXT,
            bankDetails TEXT,
            commissionRate REAL CHECK(commissionRate >= 0 AND commissionRate <= 1),
            lastModified TEXT DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS USER(
            userId INTEGER NOT NULL PRIMARY KEY,
            username text not null,
            password text not null
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
        sku INTEGER,
        itemGroup TEXT,
        description TEXT NOT NULL,
        retailPrice REAL NOT NULL CHECK(retailPrice >= 0),
        costPrice REAL NOT NULL CHECK(costPrice >= 0),
        stockQuantity INTEGER NOT NULL DEFAULT 0 CHECK(stockQuantity >= 0),
        stockRecieved INTEGER NOT NULL,
        stockSold INTEGER NOT NULL DEFAULT 0 CHECK(stockSold >= 0),
        minimumStock INTEGER DEFAULT 0 CHECK(minimumStock >= 0),
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
            synced INTEGER DEFAULT 0 CHECK(synced IN(0,1)),
            paymentMethod TEXT,
            paymentStatus TEXT,
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

        -- FIXED: No CHECK constraint on quantity to allow negative values for refunds
        CREATE TABLE IF NOT EXISTS INVOICEITEM(
            invoiceItemID INTEGER NOT NULL PRIMARY KEY,
            quantity INTEGER NOT NULL,
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

-- Drop tables in correct order to handle foreign key constraints

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
        sku INT,
        itemGroup NVARCHAR(5),
        description NVARCHAR(500) NOT NULL,
        retailPrice FLOAT NOT NULL CHECK(retailPrice >= 0),
        costPrice FLOAT NOT NULL CHECK(costPrice >= 0),
        stockQuantity INT NOT NULL DEFAULT 0 CHECK(stockQuantity >= 0),
        stockRecieved INT NOT NULL,
        stockSold INT NOT NULL DEFAULT 0 CHECK(stockSold >= 0),
        minimumStock INT DEFAULT 0 CHECK(minimumStock >= 0),
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
            invoiceQuoteID BIGINT NOT NULL PRIMARY KEY,
            date DATETIME NOT NULL,
            type INT NOT NULL CHECK(type IN (1,2)),
            totalAmount FLOAT NOT NULL,
            customerID INT,
            staffID INT NOT NULL,
            lastModified DATETIME DEFAULT GETUTCDATE(),
            paymentMethod NVARCHAR(50),
            paymentStatus NVARCHAR(50),
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
            invoiceItemID BIGINT NOT NULL PRIMARY KEY,
            quantity INT NOT NULL,
            priceAtSale FLOAT NOT NULL CHECK(priceAtSale >= 0),
            itemID INT NOT NULL,
            invoiceQuoteID BIGINT NOT NULL,
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

        /// <summary>
        /// Migrate existing database to remove CHECK constraint from INVOICEITEM.quantity
        /// </summary>
        private static void MigrateDatabase(SqliteConnection connection)
        {
            try
            {
                // Check if we need to migrate (look for the old constraint)
                string checkConstraintSql = @"
            SELECT sql FROM sqlite_master 
            WHERE type='table' AND name='INVOICEITEM' 
            AND sql LIKE '%CHECK(quantity > 0)%'";

                using var checkCmd = new SqliteCommand(checkConstraintSql, connection);
                var result = checkCmd.ExecuteScalar()?.ToString();

                if (!string.IsNullOrEmpty(result))
                {
                    Console.WriteLine("Migrating INVOICEITEM table to remove CHECK constraint...");

                    // Create temporary table without constraint
                    string createTempTable = @"
                CREATE TABLE INVOICEITEM_TEMP(
                    invoiceItemID INTEGER NOT NULL PRIMARY KEY,
                    quantity INTEGER NOT NULL,
                    priceAtSale REAL NOT NULL CHECK(priceAtSale >= 0),
                    itemID INTEGER NOT NULL,
                    invoiceQuoteID INTEGER NOT NULL,
                    synced INTEGER DEFAULT 0,
                    lastModified TEXT DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(invoiceQuoteID) REFERENCES INVOICEQUOTE(invoiceQuoteID),
                    FOREIGN KEY(itemID) REFERENCES ITEM(itemID)
                )";

                    using var createCmd = new SqliteCommand(createTempTable, connection);
                    createCmd.ExecuteNonQuery();

                    // Copy data
                    string copyData = "INSERT INTO INVOICEITEM_TEMP SELECT * FROM INVOICEITEM";
                    using var copyCmd = new SqliteCommand(copyData, connection);
                    copyCmd.ExecuteNonQuery();

                    // Drop old table
                    string dropOld = "DROP TABLE INVOICEITEM";
                    using var dropCmd = new SqliteCommand(dropOld, connection);
                    dropCmd.ExecuteNonQuery();

                    // Rename temp table
                    string renameTable = "ALTER TABLE INVOICEITEM_TEMP RENAME TO INVOICEITEM";
                    using var renameCmd = new SqliteCommand(renameTable, connection);
                    renameCmd.ExecuteNonQuery();

                    // Recreate indexes
                    string createIndex1 = "CREATE INDEX idx_invoiceitem_invoice ON INVOICEITEM(invoiceQuoteID)";
                    string createIndex2 = "CREATE INDEX idx_invoiceitem_item ON INVOICEITEM(itemID)";

                    using var idx1Cmd = new SqliteCommand(createIndex1, connection);
                    idx1Cmd.ExecuteNonQuery();

                    using var idx2Cmd = new SqliteCommand(createIndex2, connection);
                    idx2Cmd.ExecuteNonQuery();

                    Console.WriteLine("Database migration completed successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database migration failed: {ex.Message}");
                throw;
            }
        }


        /// <summary>
        /// Migrate Azure SQL database to remove CHECK constraint from INVOICEITEM.quantity
        /// </summary>
        private static void MigrateAzureSQLDatabase(SqlConnection connection)
        {
            try
            {
                // Check if the constraint exists
                string checkConstraintSql = @"
            SELECT name 
            FROM sys.check_constraints 
            WHERE name = 'CK__INVOICEIT__quant__4E53A1AA' 
               OR OBJECT_DEFINITION(parent_object_id) LIKE '%quantity%> 0%'";

                using var checkCmd = new SqlCommand(checkConstraintSql, connection);
                var constraintName = checkCmd.ExecuteScalar()?.ToString();

                if (!string.IsNullOrEmpty(constraintName))
                {
                    Console.WriteLine($"Migrating Azure SQL INVOICEITEM table - dropping constraint: {constraintName}");

                    // Drop the constraint
                    string dropConstraintSql = $"ALTER TABLE INVOICEITEM DROP CONSTRAINT {constraintName}";
                    using var dropCmd = new SqlCommand(dropConstraintSql, connection);
                    dropCmd.ExecuteNonQuery();

                    Console.WriteLine("Azure SQL database migration completed successfully.");
                }
                else
                {
                    Console.WriteLine("No CHECK constraint found on INVOICEITEM.quantity - Azure SQL schema is already correct.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Azure SQL database migration failed: {ex.Message}");
                // Don't throw - we want to continue even if migration fails
            }
        }

        private static void InsertTestDataSQLite(SqliteConnection connection)
        {
            string insertDataSql = @"
    INSERT INTO SELLER (name, contactInfo, bankDetails, commissionRate) VALUES
    ('Robin Longbow', 'robin@archersguild.com', 'ACC-9876', 0.05),
    ('Marian Fletcher', 'marian@archeryworld.com', 'ACC-6543', 0.07);

    INSERT INTO SUPPLIER (name, contactInfo, address) VALUES
    ('Precision Bows Ltd', 'sales@precisionbows.com', '45 Bowstring Lane'),
    ('Eagle Arrows Co', 'info@eaglearrows.com', '210 Fletching Avenue'),
    ('TargetCraft Supplies', 'support@targetcraft.com', '33 Range Road');

    INSERT INTO ITEM (sku, itemGroup, description, retailPrice, costPrice, stockQuantity, stockRecieved, stockSold, supplierID, sellerID) VALUES
     (1001, 'BOW', 'Longbow Elite', 4500.00, 3000.00, 15, 15, 0, 1, 1),
     (1002, 'BOW', 'Recurve Bow Pro', 5200.00, 3400.00, 10, 10, 0, 1, 2),
     (2001, 'ARR', 'Carbon Arrows (Pack of 12)', 850.00, 550.00, 50, 50, 0, 2, 1),
     (2002, 'ARR', 'Traditional Wooden Arrows', 600.00, 400.00, 40, 40, 0, 2, 2),
     (3001, 'TAR', 'Foam Target Block', 750.00, 500.00, 25, 25, 0, 3, 1),
     (3002, 'TAR', '3D Deer Target', 1800.00, 1200.00, 10, 10, 0, 3, 2),
     (4001, 'ACC', 'Bowstring Wax', 120.00, 60.00, 100, 100, 0, 3, 1),
     (4002, 'ACC', 'Armguard Leather', 350.00, 200.00, 30, 30, 0, 1, 2),
     (4003, 'ACC', 'Finger Tab Deluxe', 280.00, 150.00, 45, 45, 0, 1, 1),
     (2003, 'ACC', 'Stabilizer Carbon Pro', 950.00, 600.00, 20, 20, 0, 2, 2);


    INSERT INTO STAFF (name, Role, userName, passwordHash, salary) VALUES
    ('Ruben Janse', 'Admin', 'ruben', 'hashedpassword1', 15000),
    ('Sarah Ndlovu', 'Cashier', 'sarah', 'hashedpassword2', 9000),
    ('Michael Smith', 'Manager', 'michael', 'hashedpassword3', 12000),
    ('Emily Johnson', 'Cashier', 'emily', 'hashedpassword4', 9000);

    INSERT INTO CUSTOMER (name, phone, email, credit) VALUES
    ('Alice Archer', '0712345678', 'alice@archerymail.com', 100),
    ('Bob Bowman', '0723456789', 'bob@archerymail.com', 50),
    ('Charlie Fletcher', '0734567890', 'charlie@archerymail.com', 75);
";


            using var cmd = new SqliteCommand(insertDataSql, connection);
            cmd.ExecuteNonQuery();
        }

        private static void InsertTestDataAzureSQL(SqlConnection connection)
        {
            string insertDataSql = @"


    INSERT INTO SELLER (name, contactInfo, bankDetails, commissionRate) VALUES
    ('Robin Longbow', 'robin@archersguild.com', 'ACC-9876', 0.05),
    ('Marian Fletcher', 'marian@archeryworld.com', 'ACC-6543', 0.07);

    INSERT INTO SUPPLIER (name, contactInfo, address) VALUES
    ('Precision Bows Ltd', 'sales@precisionbows.com', '45 Bowstring Lane'),
    ('Eagle Arrows Co', 'info@eaglearrows.com', '210 Fletching Avenue'),
    ('TargetCraft Supplies', 'support@targetcraft.com', '33 Range Road');

    INSERT INTO ITEM (sku, itemGroup, description, retailPrice, costPrice, stockQuantity, stockRecieved, stockSold, supplierID, sellerID) VALUES
     (1001, 'BOW', 'Longbow Elite', 4500.00, 3000.00, 15, 15, 0, 1, 1),
     (1002, 'BOW', 'Recurve Bow Pro', 5200.00, 3400.00, 10, 10, 0, 1, 2),
     (2001, 'ARR', 'Carbon Arrows (Pack of 12)', 850.00, 550.00, 50, 50, 0, 2, 1),
     (2002, 'ARR', 'Traditional Wooden Arrows', 600.00, 400.00, 40, 40, 0, 2, 2),
     (3001, 'TAR', 'Foam Target Block', 750.00, 500.00, 25, 25, 0, 3, 1),
     (3002, 'TAR', '3D Deer Target', 1800.00, 1200.00, 10, 10, 0, 3, 2),
     (4001, 'ACC', 'Bowstring Wax', 120.00, 60.00, 100, 100, 0, 3, 1),
     (4002, 'ACC', 'Armguard Leather', 350.00, 200.00, 30, 30, 0, 1, 2),
     (4003, 'ACC', 'Finger Tab Deluxe', 280.00, 150.00, 45, 45, 0, 1, 1),
     (2003, 'ACC', 'Stabilizer Carbon Pro', 950.00, 600.00, 20, 20, 0, 2, 2);

    INSERT INTO STAFF (name, Role, userName, passwordHash, salary) VALUES
    ('Ruben Janse', 'Admin', 'ruben', 'hashedpassword1', 15000),
    ('Sarah Ndlovu', 'Cashier', 'sarah', 'hashedpassword2', 9000),
    ('Michael Smith', 'Manager', 'michael', 'hashedpassword3', 12000),
    ('Emily Johnson', 'Cashier', 'emily', 'hashedpassword4', 9000);

    INSERT INTO CUSTOMER (name, phone, email, credit) VALUES
    ('Alice Archer', '0712345678', 'alice@archerymail.com', 100),
    ('Bob Bowman', '0723456789', 'bob@archerymail.com', 50),
    ('Charlie Fletcher', '0734567890', 'charlie@archerymail.com', 75);
";


            using var cmd = new SqlCommand(insertDataSql, connection);
            cmd.ExecuteNonQuery();
        }
        public static string GetCurrentTimestamp()
        {
            // South Africa is UTC+2
            DateTime southAfricaTime = DateTime.UtcNow.AddHours(2);
            return southAfricaTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static DateTime ParseTimestamp(string timestamp)
        {
            // Parse timestamp and assume it's already in South Africa time
            return DateTime.Parse(timestamp);
        }
        public static void FixTimestamps()
        {
            try
            {
                using var sqliteConn = new SqliteConnection(SqliteConnectionString);
                sqliteConn.Open();

                // Update all local timestamps to current South Africa time
                string updateSql = @"
            UPDATE ITEM 
            SET lastModified = @currentTime";

                using var cmd = new SqliteCommand(updateSql, sqliteConn);
                cmd.Parameters.AddWithValue("@currentTime", GetCurrentTimestamp());
                int rowsUpdated = cmd.ExecuteNonQuery();

                Console.WriteLine($"[TIMESTAMP FIX] Updated {rowsUpdated} items to South Africa time: {GetCurrentTimestamp()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TIMESTAMP FIX ERROR] {ex.Message}");
            }
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

            // Step 2: DOWNLOAD MISSING ITEMS FROM AZURE FIRST
            DownloadMissingItemsFromAzure(sqliteConn, azureConn);

            // Step 3: Sync item master data (prices, descriptions) but NOT quantities YET
            SyncItemMasterDataWithoutInventory(sqliteConn, azureConn);

            // Step 4: Sync transactions FIRST (upload local transactions to Azure)
            SyncTransactions(sqliteConn, azureConn);

            // Step 5: Recalculate inventory on BOTH databases independently
            RecalculateInventoryOnBothDatabases(sqliteConn, azureConn);

            // Step 6: Sync reports
            SyncMasterData(sqliteConn, azureConn, "REPORT", new[] { "reportID", "reportType", "date", "staffID", "lastModified" });

            Console.WriteLine("Transaction-based sync completed successfully.");
        }

        private static void SyncItemMasterDataWithoutInventory(SqliteConnection sqliteConn, SqlConnection azureConn)
        {
            string[] columns = {
        "itemID",
        "description",
        "retailPrice",
        "costPrice",
        "supplierID",
        "sellerID",
        "lastModified"
    };

            var localData = GetTableData(sqliteConn, "ITEM", columns);
            var azureData = GetTableDataFromAzure(azureConn, "ITEM", columns);

            using var transaction = azureConn.BeginTransaction();
            try
            {
                foreach (DataRow localRow in localData.Rows)
                {
                    var itemID = Convert.ToInt32(localRow["itemID"]);
                    var azureRow = azureData.AsEnumerable()
                        .FirstOrDefault(r => r["itemID"].ToString() == itemID.ToString());

                    if (azureRow == null)
                    {
                        // New item – insert with initial inventory from local
                        var localInventory = GetLocalInventory(sqliteConn, itemID);
                        var insertSql = @"
                    INSERT INTO ITEM 
                    (itemID, description, retailPrice, costPrice, stockQuantity, stockSold, stockRecieved, supplierID, sellerID, lastModified)
                    VALUES 
                    (@itemID, @description, @retailPrice, @costPrice, @stockQuantity, @stockSold, @stockRecieved, @supplierID, @sellerID, @lastModified)";

                        using var cmd = new SqlCommand(insertSql, azureConn, transaction);
                        cmd.Parameters.AddWithValue("@itemID", itemID);
                        cmd.Parameters.AddWithValue("@description", localRow["description"]);
                        cmd.Parameters.AddWithValue("@retailPrice", localRow["retailPrice"]);
                        cmd.Parameters.AddWithValue("@costPrice", localRow["costPrice"]);
                        cmd.Parameters.AddWithValue("@stockQuantity", localInventory.stockQuantity);
                        cmd.Parameters.AddWithValue("@stockSold", localInventory.stockSold);
                        cmd.Parameters.AddWithValue("@stockRecieved", localInventory.stockRecieved);
                        cmd.Parameters.AddWithValue("@supplierID", localRow["supplierID"] == DBNull.Value ? (object)DBNull.Value : localRow["supplierID"]);
                        cmd.Parameters.AddWithValue("@sellerID", localRow["sellerID"] == DBNull.Value ? (object)DBNull.Value : localRow["sellerID"]);
                        cmd.Parameters.AddWithValue("@lastModified", localRow["lastModified"]);
                        cmd.ExecuteNonQuery();

                        Console.WriteLine($"[ITEM] Added new item {itemID} to Azure");
                    }
                    else
                    {
                        // CONFLICT DETECTION: Check if it's the same item or different items with same ID
                        string localDesc = localRow["description"]?.ToString() ?? "";
                        string azureDesc = azureRow["description"]?.ToString() ?? "";

                        var localModified = ParseTimestamp(localRow["lastModified"].ToString());
                        var azureModified = ParseTimestamp(azureRow["lastModified"].ToString()).AddHours(2);

                        // If descriptions are significantly different, this is a collision!
                        if (!localDesc.Equals(azureDesc, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[CONFLICT] Item ID {itemID} collision detected!");
                            Console.WriteLine($"  Local: {localDesc}");
                            Console.WriteLine($"  Azure: {azureDesc}");

                            // Get local inventory for the conflicting item
                            var conflictInventory = GetLocalInventory(sqliteConn, itemID);

                            // Generate new ID for local item
                            int newItemId = Convert.ToInt32(GetNextItemID());

                            // Update local item with new ID
                            using var updateLocalCmd = new SqliteCommand(
                                "UPDATE ITEM SET itemID = @newId WHERE itemID = @oldId",
                                sqliteConn);
                            updateLocalCmd.Parameters.AddWithValue("@newId", newItemId);
                            updateLocalCmd.Parameters.AddWithValue("@oldId", itemID);
                            updateLocalCmd.ExecuteNonQuery();

                            // Update any invoice items referencing this item
                            using var updateInvoiceItemsCmd = new SqliteCommand(
                                "UPDATE INVOICEITEM SET itemID = @newId WHERE itemID = @oldId",
                                sqliteConn);
                            updateInvoiceItemsCmd.Parameters.AddWithValue("@newId", newItemId);
                            updateInvoiceItemsCmd.Parameters.AddWithValue("@oldId", itemID);
                            updateInvoiceItemsCmd.ExecuteNonQuery();

                            // Now insert with new ID
                            var insertConflictSql = @"
                        INSERT INTO ITEM 
                        (itemID, description, retailPrice, costPrice, stockQuantity, stockSold, stockRecieved, supplierID, sellerID, lastModified)
                        VALUES 
                        (@itemID, @description, @retailPrice, @costPrice, @stockQuantity, @stockSold, @stockRecieved, @supplierID, @sellerID, @lastModified)";

                            using var conflictCmd = new SqlCommand(insertConflictSql, azureConn, transaction);
                            conflictCmd.Parameters.AddWithValue("@itemID", newItemId);
                            conflictCmd.Parameters.AddWithValue("@description", localRow["description"]);
                            conflictCmd.Parameters.AddWithValue("@retailPrice", localRow["retailPrice"]);
                            conflictCmd.Parameters.AddWithValue("@costPrice", localRow["costPrice"]);
                            conflictCmd.Parameters.AddWithValue("@stockQuantity", conflictInventory.stockQuantity);
                            conflictCmd.Parameters.AddWithValue("@stockSold", conflictInventory.stockSold);
                            conflictCmd.Parameters.AddWithValue("@stockRecieved", conflictInventory.stockRecieved);
                            conflictCmd.Parameters.AddWithValue("@supplierID", localRow["supplierID"] == DBNull.Value ? (object)DBNull.Value : localRow["supplierID"]);
                            conflictCmd.Parameters.AddWithValue("@sellerID", localRow["sellerID"] == DBNull.Value ? (object)DBNull.Value : localRow["sellerID"]);
                            conflictCmd.Parameters.AddWithValue("@lastModified", localRow["lastModified"]);
                            conflictCmd.ExecuteNonQuery();

                            Console.WriteLine($"[CONFLICT RESOLVED] Reassigned local item to new ID: {newItemId}");
                        }
                        else if (localModified > azureModified)
                        {
                            // Same item, local is newer - update Azure (prices, etc.)
                            var updateSql = @"
                        UPDATE ITEM 
                        SET description=@description, 
                            retailPrice=@retailPrice, 
                            costPrice=@costPrice, 
                            supplierID=@supplierID, 
                            sellerID=@sellerID,
                            lastModified=@lastModified
                        WHERE itemID=@itemID";

                            using var cmd = new SqlCommand(updateSql, azureConn, transaction);
                            cmd.Parameters.AddWithValue("@itemID", itemID);
                            cmd.Parameters.AddWithValue("@description", localRow["description"]);
                            cmd.Parameters.AddWithValue("@retailPrice", localRow["retailPrice"]);
                            cmd.Parameters.AddWithValue("@costPrice", localRow["costPrice"]);
                            cmd.Parameters.AddWithValue("@supplierID", localRow["supplierID"] == DBNull.Value ? (object)DBNull.Value : localRow["supplierID"]);
                            cmd.Parameters.AddWithValue("@sellerID", localRow["sellerID"] == DBNull.Value ? (object)DBNull.Value : localRow["sellerID"]);
                            cmd.Parameters.AddWithValue("@lastModified", localRow["lastModified"]);
                            cmd.ExecuteNonQuery();

                            Console.WriteLine($"[ITEM] Updated item {itemID} in Azure (local newer)");
                        }
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
        
      
        private static (int stockQuantity, int stockSold, int stockRecieved) GetLocalInventory(SqliteConnection conn, int itemID)
        {
            var query = "SELECT stockQuantity, stockSold, stockRecieved FROM ITEM WHERE itemID = @itemID";
            using var cmd = new SqliteCommand(query, conn);
            cmd.Parameters.AddWithValue("@itemID", itemID);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
            }

            return (0, 0, 0);
        }

        public static void InitializeStockReceived(int itemID)
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            // Set stockRecieved = current stockQuantity + stockSold if stockRecieved is 0
            var updateSql = @"UPDATE ITEM 
                      SET stockRecieved = stockQuantity + stockSold,
                          lastModified = CURRENT_TIMESTAMP
                      WHERE itemID = @id AND stockRecieved = 0";

            using var cmd = new SqliteCommand(updateSql, connection);
            cmd.Parameters.AddWithValue("@id", itemID);
            cmd.ExecuteNonQuery();
        }

        private static void RecalculateInventoryOnBothDatabases(SqliteConnection sqliteConn, SqlConnection azureConn)
        {
            Console.WriteLine("Recalculating inventory from transactions on both databases...");

            // Recalculate Azure inventory
            RecalculateInventoryForDatabase(azureConn, isAzure: true);

            // Recalculate Local inventory
            RecalculateInventoryForDatabase(sqliteConn, isAzure: false);

            Console.WriteLine("Inventory recalculation completed for both databases");
        }

        private static void RecalculateInventoryForDatabase(DbConnection connection, bool isAzure)
        {
            var items = new DataTable();
            string getItemsSql = "SELECT itemID, stockRecieved FROM ITEM";

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = getItemsSql;
                using var reader = cmd.ExecuteReader();
                items.Load(reader);
            }

            DbTransaction transaction = null;
            if (isAzure)
                transaction = ((SqlConnection)connection).BeginTransaction();
            else
                transaction = ((SqliteConnection)connection).BeginTransaction();

            try
            {
                foreach (DataRow item in items.Rows)
                {
                    var itemID = item["itemID"];
                    var stockReceived = Convert.ToInt32(item["stockRecieved"]);

                    // Calculate total sold from invoice items (type 1 = invoice/sale)
                    var soldSql = @"SELECT COALESCE(SUM(ii.quantity), 0) 
                           FROM INVOICEITEM ii
                           INNER JOIN INVOICEQUOTE iq ON ii.invoiceQuoteID = iq.invoiceQuoteID
                           WHERE ii.itemID = @itemID AND iq.type = 1";

                    int totalSold = 0;
                    using (var soldCmd = connection.CreateCommand())
                    {
                        soldCmd.Transaction = transaction;
                        soldCmd.CommandText = soldSql;

                        var param = soldCmd.CreateParameter();
                        param.ParameterName = "@itemID";
                        param.Value = itemID;
                        soldCmd.Parameters.Add(param);

                        totalSold = Convert.ToInt32(soldCmd.ExecuteScalar());
                    }

                    // Calculate remaining stock
                    int remainingStock = Math.Max(0, stockReceived - totalSold);

                    // Update item with recalculated values
                    var updateSql = @"UPDATE ITEM 
                             SET stockSold = @sold, 
                                 stockQuantity = @quantity 
                             WHERE itemID = @itemID";

                    using var updateCmd = connection.CreateCommand();
                    updateCmd.Transaction = transaction;
                    updateCmd.CommandText = updateSql;

                    var soldParam = updateCmd.CreateParameter();
                    soldParam.ParameterName = "@sold";
                    soldParam.Value = totalSold;
                    updateCmd.Parameters.Add(soldParam);

                    var qtyParam = updateCmd.CreateParameter();
                    qtyParam.ParameterName = "@quantity";
                    qtyParam.Value = remainingStock;
                    updateCmd.Parameters.Add(qtyParam);

                    var idParam = updateCmd.CreateParameter();
                    idParam.ParameterName = "@itemID";
                    idParam.Value = itemID;
                    updateCmd.Parameters.Add(idParam);

                    updateCmd.ExecuteNonQuery();

                    string dbName = isAzure ? "Azure" : "Local";
                    Console.WriteLine($"[{dbName} ITEM {itemID}] Recalculated: Received={stockReceived}, Sold={totalSold}, Remaining={remainingStock}");
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new Exception($"Failed to recalculate inventory: {ex.Message}", ex);
            }
        }

        private static void SyncMasterData(SqliteConnection sqliteConn, SqlConnection azureConn, string tableName, string[] columns)
        {
            var localData = GetTableData(sqliteConn, tableName, columns);
            var azureData = GetTableDataFromAzure(azureConn, tableName, columns);

            using var transaction = azureConn.BeginTransaction();
            try
            {
                // Sync from Azure to Local (download changes from cloud)
                foreach (DataRow azureRow in azureData.Rows)
                {
                    var primaryKey = azureRow[columns[0]];
                    var azureModified = DateTime.Parse(azureRow["lastModified"].ToString());

                    var localRow = localData.AsEnumerable().FirstOrDefault(r => r[columns[0]].ToString() == primaryKey.ToString());

                    if (localRow == null)
                    {
                        InsertToLocal(sqliteConn, tableName, columns, azureRow);
                        Console.WriteLine($"[{tableName}] Downloaded new record {primaryKey} from Azure");
                    }
                    else
                    {
                        var localModified = DateTime.Parse(localRow["lastModified"].ToString());

                        if (azureModified > localModified)
                        {
                            UpdateLocal(sqliteConn, tableName, columns, azureRow);
                            Console.WriteLine($"[{tableName}] Updated local record {primaryKey} (Azure newer)");
                        }
                    }
                }

                // Sync from Local to Azure (upload changes to cloud)
                foreach (DataRow localRow in localData.Rows)
                {
                    var primaryKey = localRow[columns[0]];
                    var localModified = DateTime.Parse(localRow["lastModified"].ToString());

                    var azureRow = azureData.AsEnumerable().FirstOrDefault(r => r[columns[0]].ToString() == primaryKey.ToString());

                    if (azureRow == null)
                    {
                        // Check for duplicate (same name, different ID)
                        if (tableName == "CUSTOMER")
                        {
                            var nameDuplicateCheck = @"SELECT customerID FROM CUSTOMER WHERE name = @name";
                            using var dupCmd = new SqlCommand(nameDuplicateCheck, azureConn, transaction);
                            dupCmd.Parameters.AddWithValue("@name", localRow["name"]);
                            var existingId = dupCmd.ExecuteScalar();

                            if (existingId != null)
                            {
                                // Merge: Update local to use Azure's ID
                                Console.WriteLine($"[{tableName}] Merging duplicate customer: Local ID {primaryKey} -> Azure ID {existingId}");

                                using var mergeCmd = new SqliteCommand(
                                    "UPDATE INVOICEQUOTE SET customerID = @azureId WHERE customerID = @localId",
                                    sqliteConn);
                                mergeCmd.Parameters.AddWithValue("@azureId", existingId);
                                mergeCmd.Parameters.AddWithValue("@localId", primaryKey);
                                mergeCmd.ExecuteNonQuery();

                                continue; // Skip insert
                            }
                        }

                        InsertToAzure(azureConn, transaction, tableName, columns, localRow);
                        Console.WriteLine($"[{tableName}] Uploaded new record {primaryKey} to Azure");
                    }
                    else
                    {
                        var azureModified = DateTime.Parse(azureRow["lastModified"].ToString());

                        if (localModified > azureModified)
                        {
                            UpdateAzure(azureConn, transaction, tableName, columns, localRow);
                            Console.WriteLine($"[{tableName}] Updated Azure record {primaryKey} (local newer)");
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

        private static void SyncTransactions(SqliteConnection sqliteConn, SqlConnection azureConn)
        {
            // Sync unsynced invoices
            var localInvoices = new DataTable();
            using (var cmd = new SqliteCommand("SELECT * FROM INVOICEQUOTE WHERE synced = 0 ORDER BY date ASC", sqliteConn))
            using (var reader = cmd.ExecuteReader())
            {
                localInvoices.Load(reader);
            }

            using var transaction = azureConn.BeginTransaction();
            try
            {
                foreach (DataRow invoice in localInvoices.Rows)
                {
                    long invoiceId = Convert.ToInt64(invoice["invoiceQuoteID"]);

                    // Check if invoice exists in Azure
                    var checkSql = "SELECT COUNT(*) FROM INVOICEQUOTE WHERE invoiceQuoteID = @id";
                    using var checkCmd = new SqlCommand(checkSql, azureConn, transaction);
                    checkCmd.Parameters.AddWithValue("@id", invoiceId);
                    var exists = (int)checkCmd.ExecuteScalar() > 0;

                    if (exists)
                    {
                        // CONFLICT: Invoice ID already exists in Azure (from another device)
                        Console.WriteLine($"[CONFLICT] Invoice {invoiceId} already exists in Azure. Generating new ID...");

                        // Generate new unique ID for this invoice
                        long newInvoiceId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        // Update local invoice with new ID
                        using var updateLocalCmd = new SqliteCommand(
                            "UPDATE INVOICEQUOTE SET invoiceQuoteID = @newId WHERE invoiceQuoteID = @oldId",
                            sqliteConn);
                        updateLocalCmd.Parameters.AddWithValue("@newId", newInvoiceId);
                        updateLocalCmd.Parameters.AddWithValue("@oldId", invoiceId);
                        updateLocalCmd.ExecuteNonQuery();

                        // Update invoice items with new ID
                        using var updateItemsCmd = new SqliteCommand(
                            "UPDATE INVOICEITEM SET invoiceQuoteID = @newId WHERE invoiceQuoteID = @oldId",
                            sqliteConn);
                        updateItemsCmd.Parameters.AddWithValue("@newId", newInvoiceId);
                        updateItemsCmd.Parameters.AddWithValue("@oldId", invoiceId);
                        updateItemsCmd.ExecuteNonQuery();

                        // Use new ID for Azure insert
                        invoiceId = newInvoiceId;
                        invoice["invoiceQuoteID"] = newInvoiceId;

                        Console.WriteLine($"[CONFLICT RESOLVED] Reassigned to new ID: {newInvoiceId}");
                    }

                    // Insert invoice header to Azure
                    var insertSql = @"INSERT INTO INVOICEQUOTE 
                (invoiceQuoteID, date, type, totalAmount, customerID, staffID, paymentMethod, paymentStatus, lastModified) 
                VALUES (@id, @date, @type, @amount, @custID, @staffID, @paymentMethod, @paymentStatus, @lastModified)";

                    using var insertCmd = new SqlCommand(insertSql, azureConn, transaction);
                    insertCmd.Parameters.AddWithValue("@id", invoiceId);
                    insertCmd.Parameters.AddWithValue("@date", invoice["date"]);
                    insertCmd.Parameters.AddWithValue("@type", invoice["type"]);
                    insertCmd.Parameters.AddWithValue("@amount", invoice["totalAmount"]);
                    insertCmd.Parameters.AddWithValue("@custID", invoice["customerID"] == DBNull.Value ? (object)DBNull.Value : invoice["customerID"]);
                    insertCmd.Parameters.AddWithValue("@staffID", invoice["staffID"]);
                    insertCmd.Parameters.AddWithValue("@paymentMethod", invoice["paymentMethod"] == DBNull.Value ? (object)DBNull.Value : invoice["paymentMethod"]);
                    insertCmd.Parameters.AddWithValue("@paymentStatus", invoice["paymentStatus"] == DBNull.Value ? (object)DBNull.Value : invoice["paymentStatus"]);
                    insertCmd.Parameters.AddWithValue("@lastModified", invoice["lastModified"]);
                    insertCmd.ExecuteNonQuery();

                    Console.WriteLine($"[INVOICE] Synced invoice {invoiceId} to Azure");

                    // Sync invoice items only (NO inventory adjustment here)
                    SyncInvoiceItemsOnly(sqliteConn, azureConn, transaction, invoiceId);

                    // Mark as synced locally
                    using var updateCmd = new SqliteCommand("UPDATE INVOICEQUOTE SET synced = 1 WHERE invoiceQuoteID = @id", sqliteConn);
                    updateCmd.Parameters.AddWithValue("@id", invoiceId);
                    updateCmd.ExecuteNonQuery();
                }

                transaction.Commit();
                Console.WriteLine($"[SYNC] Successfully synced {localInvoices.Rows.Count} invoice(s). Inventory will be recalculated next.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new Exception($"Failed to sync transactions: {ex.Message}", ex);
            }
        }

        // New method - just sync invoice items without touching inventory
        private static void SyncInvoiceItemsOnly(
            SqliteConnection sqliteConn,
            SqlConnection azureConn,
            SqlTransaction transaction,
            long invoiceQuoteID)
        {
            // Get invoice items for this invoice
            var invoiceItems = new DataTable();
            using (var cmd = new SqliteCommand(
                "SELECT * FROM INVOICEITEM WHERE invoiceQuoteID = @id",
                sqliteConn))
            {
                cmd.Parameters.AddWithValue("@id", invoiceQuoteID);
                using var reader = cmd.ExecuteReader();
                invoiceItems.Load(reader);
            }

            foreach (DataRow item in invoiceItems.Rows)
            {
                // Check if invoice item already exists in Azure
                var checkItemSql = "SELECT COUNT(*) FROM INVOICEITEM WHERE invoiceItemID = @itemID";
                using var checkItemCmd = new SqlCommand(checkItemSql, azureConn, transaction);
                checkItemCmd.Parameters.AddWithValue("@itemID", item["invoiceItemID"]);
                var itemExists = (int)checkItemCmd.ExecuteScalar() > 0;

                if (!itemExists)
                {
                    // Insert the invoice item only - NO inventory changes
                    var insertItemSql = @"INSERT INTO INVOICEITEM 
                (invoiceItemID, quantity, priceAtSale, itemID, invoiceQuoteID, lastModified)
                VALUES (@itemID, @quantity, @price, @productID, @invoiceID, @lastModified)";

                    using var insertItemCmd = new SqlCommand(insertItemSql, azureConn, transaction);
                    insertItemCmd.Parameters.AddWithValue("@itemID", item["invoiceItemID"]);
                    insertItemCmd.Parameters.AddWithValue("@quantity", item["quantity"]);
                    insertItemCmd.Parameters.AddWithValue("@price", item["priceAtSale"]);
                    insertItemCmd.Parameters.AddWithValue("@productID", item["itemID"]);
                    insertItemCmd.Parameters.AddWithValue("@invoiceID", invoiceQuoteID);
                    insertItemCmd.Parameters.AddWithValue("@lastModified", item["lastModified"]);
                    insertItemCmd.ExecuteNonQuery();

                    Console.WriteLine($"  [INVOICEITEM] Synced invoice item {item["invoiceItemID"]} for item {item["itemID"]} (qty: {item["quantity"]})");
                }
            }
        }

        // Add these methods to the Database class in Database.cs

        /// <summary>
        /// Get monthly sales transactions for reporting
        /// </summary>
        public static DataTable GetMonthlyTransactions(int month, int year)
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            string query = @"
        SELECT 
            iq.date as Date,
            COALESCE(c.name, 'Walk-in Customer') as CustomerName,
            s.name as SalesStaff,
            iq.totalAmount as Paid,
            iq.totalAmount as PurchaseAmount,
            COALESCE(iq.paymentMethod, 'Cash') as PaymentMethod,
            iq.invoiceQuoteID as InvoiceNumber,
            iq.type as TransactionType
        FROM INVOICEQUOTE iq
        LEFT JOIN CUSTOMER c ON iq.customerID = c.customerID
        INNER JOIN STAFF s ON iq.staffID = s.staffID
        WHERE strftime('%m', iq.date) = @month 
        AND strftime('%Y', iq.date) = @year
        ORDER BY iq.date";

            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@month", month.ToString("00"));
            cmd.Parameters.AddWithValue("@year", year.ToString());

            var dataTable = new DataTable();
            using var reader = cmd.ExecuteReader();
            dataTable.Load(reader);

            return dataTable;
        }

        /// <summary>
        /// Get monthly financial summary
        /// </summary>
        public static (decimal cardAmount, decimal cashAmount, decimal eftAmount,
                       decimal returnAmount, decimal creditAmount, decimal totalTurnover)
            GetMonthlyFinancialSummary(int month, int year)
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            // Sales by payment method
            string salesQuery = @"
        SELECT 
            COALESCE(paymentMethod, 'Cash') as PaymentMethod,
            SUM(totalAmount) as TotalAmount
        FROM INVOICEQUOTE 
        WHERE strftime('%m', date) = @month 
        AND strftime('%Y', date) = @year
        AND type = 1  -- Sales
        GROUP BY COALESCE(paymentMethod, 'Cash')";

            using var salesCmd = new SqliteCommand(salesQuery, connection);
            salesCmd.Parameters.AddWithValue("@month", month.ToString("00"));
            salesCmd.Parameters.AddWithValue("@year", year.ToString());

            decimal cardAmount = 0, cashAmount = 0, eftAmount = 0, creditAmount = 0;

            using var salesReader = salesCmd.ExecuteReader();
            while (salesReader.Read())
            {
                var method = salesReader["PaymentMethod"].ToString().ToLower();
                var amount = Convert.ToDecimal(salesReader["TotalAmount"]);

                switch (method)
                {
                    case "card":
                        cardAmount = amount;
                        break;
                    case "cash":
                        cashAmount = amount;
                        break;
                    case "eft":
                        eftAmount = amount;
                        break;
                    case "credit":
                        creditAmount = amount;
                        break;
                }
            }

            // Returns (negative amounts from invoice items)
            string returnsQuery = @"
        SELECT COALESCE(SUM(ii.quantity * ii.priceAtSale), 0) as ReturnAmount
        FROM INVOICEITEM ii
        INNER JOIN INVOICEQUOTE iq ON ii.invoiceQuoteID = iq.invoiceQuoteID
        WHERE strftime('%m', iq.date) = @month 
        AND strftime('%Y', iq.date) = @year
        AND ii.quantity < 0";

            using var returnsCmd = new SqliteCommand(returnsQuery, connection);
            returnsCmd.Parameters.AddWithValue("@month", month.ToString("00"));
            returnsCmd.Parameters.AddWithValue("@year", year.ToString());

            decimal returnAmount = Math.Abs(Convert.ToDecimal(returnsCmd.ExecuteScalar() ?? 0m));

            decimal totalTurnover = cardAmount + cashAmount + eftAmount + creditAmount - returnAmount;

            return (cardAmount, cashAmount, eftAmount, returnAmount, creditAmount, totalTurnover);
        }

        /// <summary>
        /// Get cost of goods sold for the month
        /// </summary>
        public static decimal GetMonthlyCOGS(int month, int year)
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            string query = @"
        SELECT COALESCE(SUM(ii.quantity * i.costPrice), 0) as COGS
        FROM INVOICEITEM ii
        INNER JOIN INVOICEQUOTE iq ON ii.invoiceQuoteID = iq.invoiceQuoteID
        INNER JOIN ITEM i ON ii.itemID = i.itemID
        WHERE strftime('%m', iq.date) = @month 
        AND strftime('%Y', iq.date) = @year
        AND iq.type = 1  -- Sales only
        AND ii.quantity > 0";  // Positive quantities only (exclude returns)

            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@month", month.ToString("00"));
            cmd.Parameters.AddWithValue("@year", year.ToString());

            var result = cmd.ExecuteScalar();
            return result != DBNull.Value ? Convert.ToDecimal(result) : 0m;
        }

        /// <summary>
        /// Create expenses table if it doesn't exist
        /// </summary>
        public static void InitializeExpensesTable()
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            string createTableSql = @"
        CREATE TABLE IF NOT EXISTS EXPENSES (
            expenseID INTEGER PRIMARY KEY AUTOINCREMENT,
            expenseType TEXT NOT NULL,
            amount DECIMAL(10,2) NOT NULL,
            date TEXT NOT NULL,
            description TEXT,
            lastModified TEXT DEFAULT CURRENT_TIMESTAMP
        )";

            using var cmd = new SqliteCommand(createTableSql, connection);
            cmd.ExecuteNonQuery();

            // Insert sample expenses if table is empty
            string checkDataSql = "SELECT COUNT(*) FROM EXPENSES";
            using var checkCmd = new SqliteCommand(checkDataSql, connection);
            var count = Convert.ToInt32(checkCmd.ExecuteScalar());

            if (count == 0)
            {
                string insertSampleData = @"
            INSERT INTO EXPENSES (expenseType, amount, date, description) VALUES
            ('Rent', 15000.00, date('now', 'start of month'), 'Monthly rent payment'),
            ('Utilities', 2500.00, date('now', 'start of month'), 'Electricity, water, internet'),
            ('Salaries', 45000.00, date('now', 'start of month'), 'Staff salaries'),
            ('Other', 5000.00, date('now', 'start of month'), 'Miscellaneous expenses')";

                using var insertCmd = new SqliteCommand(insertSampleData, connection);
                insertCmd.ExecuteNonQuery();
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
                    insertCmd.Parameters.AddWithValue("@qty", item["quantity"]); // Can be negative for refunds
                    insertCmd.Parameters.AddWithValue("@price", item["priceAtSale"]);
                    insertCmd.Parameters.AddWithValue("@itemID", item["itemID"]);
                    insertCmd.Parameters.AddWithValue("@invoiceID", item["invoiceQuoteID"]);

                    try
                    {
                        insertCmd.ExecuteNonQuery();
                        Console.WriteLine($"[INVOICEITEM] Synced item {item["invoiceItemID"]} to Azure (Quantity: {item["quantity"]})");
                    }
                    catch (SqlException ex) when (ex.Message.Contains("CHECK constraint"))
                    {
                        throw new Exception($"Azure SQL still has CHECK constraint on quantity. Please run migration: {ex.Message}", ex);
                    }

                    // Mark as synced locally
                    using var updateCmd = new SqliteCommand("UPDATE INVOICEITEM SET synced = 1 WHERE invoiceItemID = @id", sqliteConn);
                    updateCmd.Parameters.AddWithValue("@id", item["invoiceItemID"]);
                    updateCmd.ExecuteNonQuery();
                }
            }
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

        private static void InsertToLocal(SqliteConnection connection, string tableName, string[] columns, DataRow row)
        {
            var columnList = string.Join(", ", columns);
            var paramList = string.Join(", ", columns.Select(c => $"@{c}"));
            var insertSql = $"INSERT INTO {tableName} ({columnList}) VALUES ({paramList})";

            using var cmd = new SqliteCommand(insertSql, connection);
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
        public static void ProcessSale(long invoiceId)
        {
            try
            {
                using var connection = new SqliteConnection(SqliteConnectionString);
                connection.Open();

                // Get South Africa time (UTC+2)
                DateTime southAfricaTime = DateTime.UtcNow.AddHours(2);
                string saTimestamp = southAfricaTime.ToString("yyyy-MM-dd HH:mm:ss");

                // Get only the items from this specific invoice
                var itemsQuery = @"
            SELECT ii.itemID, ii.quantity 
            FROM INVOICEITEM ii 
            WHERE ii.invoiceQuoteID = @invoiceId";

                using var itemsCmd = new SqliteCommand(itemsQuery, connection);
                itemsCmd.Parameters.AddWithValue("@invoiceId", invoiceId);

                using var reader = itemsCmd.ExecuteReader();

                int updatedItems = 0;
                while (reader.Read())
                {
                    int itemId = reader.GetInt32(0);
                    int quantity = reader.GetInt32(1);

                    // Update ONLY this specific item's inventory
                    var updateSql = @"
                UPDATE ITEM 
                SET stockQuantity = stockQuantity - @qty,
                    stockSold = stockSold + CASE WHEN @qty > 0 THEN @qty ELSE 0 END,
                    lastModified = @saTime
                WHERE itemID = @itemId";

                    using var updateCmd = new SqliteCommand(updateSql, connection);
                    updateCmd.Parameters.AddWithValue("@qty", quantity);
                    updateCmd.Parameters.AddWithValue("@saTime", saTimestamp);
                    updateCmd.Parameters.AddWithValue("@itemId", itemId);

                    int rowsAffected = updateCmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        updatedItems++;
                        Console.WriteLine($"[PROCESSSALE] Updated item {itemId}: {quantity} units at {saTimestamp}");
                    }
                    else
                    {
                        Console.WriteLine($"[PROCESSSALE WARNING] Item {itemId} not found - no update performed");
                    }
                }

                MarkSyncRequired();
                Console.WriteLine($"[PROCESSSALE] Processed {updatedItems} items for invoice {invoiceId} at South Africa time");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROCESSSALE ERROR] {ex.Message}");
                throw;
            }
        }
        public static void ForceSyncAllInventory()
        {
            try
            {
                using var sqliteConn = new SqliteConnection(SqliteConnectionString);
                using var azureConn = new SqlConnection(AzureSqlConnectionString);

                sqliteConn.Open();
                azureConn.Open();

                Console.WriteLine("[FORCE SYNC ALL] Starting complete inventory sync...");

                // Get all items from Azure
                var azureCmd = new SqlCommand(
                    "SELECT itemID, stockQuantity, stockSold, stockRecieved, lastModified FROM ITEM",
                    azureConn);

                var azureItems = new Dictionary<int, (int qty, int sold, int received, DateTime azureModified)>();

                using (var azureReader = azureCmd.ExecuteReader())
                {
                    while (azureReader.Read())
                    {
                        int itemId = Convert.ToInt32(azureReader["itemID"]);
                        DateTime azureModified = Convert.ToDateTime(azureReader["lastModified"]);

                        // Convert Azure UTC time to South Africa time
                        DateTime southAfricaTime = azureModified.ToUniversalTime().AddHours(2);

                        azureItems[itemId] = (
                            Convert.ToInt32(azureReader["stockQuantity"]),
                            Convert.ToInt32(azureReader["stockSold"]),
                            Convert.ToInt32(azureReader["stockRecieved"]),
                            southAfricaTime
                        );
                    }
                }

                Console.WriteLine($"[FORCE SYNC ALL] Found {azureItems.Count} items in Azure");

                // Update local database to match Azure exactly with South Africa time
                int updatedCount = 0;
                foreach (var azureItem in azureItems)
                {
                    string updateSql = @"
                UPDATE ITEM 
                SET stockQuantity = @qty,
                    stockSold = @sold,
                    stockRecieved = @received,
                    lastModified = @modified
                WHERE itemID = @id";

                    using var updateCmd = new SqliteCommand(updateSql, sqliteConn);
                    updateCmd.Parameters.AddWithValue("@qty", azureItem.Value.qty);
                    updateCmd.Parameters.AddWithValue("@sold", azureItem.Value.sold);
                    updateCmd.Parameters.AddWithValue("@received", azureItem.Value.received);
                    updateCmd.Parameters.AddWithValue("@modified", azureItem.Value.azureModified.ToString("yyyy-MM-dd HH:mm:ss"));
                    updateCmd.Parameters.AddWithValue("@id", azureItem.Key);

                    int rowsAffected = updateCmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        updatedCount++;
                        Console.WriteLine($"[FORCE SYNC ALL] Updated item {azureItem.Key}: Qty {azureItem.Value.qty}, Time: {azureItem.Value.azureModified:HH:mm} SA Time");
                    }
                }

                Console.WriteLine($"[FORCE SYNC ALL] Completed: {updatedCount} items updated with South Africa time");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FORCE SYNC ALL ERROR] {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Syncs all tables (except ITEM) from Azure to local database (one-way sync from Azure to local)
        /// </summary>
        public static void SyncAllTablesFromAzure()
        {
            try
            {
                using var sqliteConn = new SqliteConnection(SqliteConnectionString);
                using var azureConn = new SqlConnection(AzureSqlConnectionString);

                sqliteConn.Open();
                azureConn.Open();

                Console.WriteLine("[SYNC ALL] Starting Azure to local sync with proper foreign key order...");

                // CRITICAL: Sync in correct foreign key dependency order
                // 1. Parent tables first
                SyncTableFromAzure(sqliteConn, azureConn, "SELLER", new[] { "sellerID", "name", "contactInfo", "bankDetails", "commissionRate", "lastModified" });
                SyncTableFromAzure(sqliteConn, azureConn, "SUPPLIER", new[] { "supplierID", "name", "contactInfo", "address", "lastModified" });
                SyncTableFromAzure(sqliteConn, azureConn, "STAFF", new[] { "staffID", "name", "Role", "userName", "passwordHash", "salary", "lastModified" });
                SyncTableFromAzure(sqliteConn, azureConn, "CUSTOMER", new[] { "customerID", "name", "phone", "email", "credit", "lastModified" });

                // 2. ITEM table (depends on SELLER and SUPPLIER)
                SyncTableFromAzure(sqliteConn, azureConn, "ITEM", new[] { "itemID", "description", "retailPrice", "costPrice", "stockQuantity", "stockSold", "stockRecieved", "supplierID", "sellerID", "lastModified" });

                // 3. INVOICEQUOTE (depends on CUSTOMER and STAFF)
                SyncTableFromAzure(sqliteConn, azureConn, "INVOICEQUOTE", new[] { "invoiceQuoteID", "date", "type", "totalAmount", "customerID", "staffID", "paymentMethod", "paymentStatus", "lastModified" });

                // 4. INVOICEITEM (depends on INVOICEQUOTE and ITEM)
                SyncTableFromAzure(sqliteConn, azureConn, "INVOICEITEM", new[] { "invoiceItemID", "quantity", "priceAtSale", "itemID", "invoiceQuoteID", "lastModified" });

                // 5. Other tables
                SyncTableFromAzure(sqliteConn, azureConn, "REPORT", new[] { "reportID", "reportType", "date", "staffID", "lastModified" });

                Console.WriteLine("[SYNC ALL] Azure to local sync completed successfully with proper foreign key order");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYNC ALL ERROR] {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Syncs a single table from Azure to local (Azure wins in conflicts)
        /// </summary>
        private static void SyncTableFromAzure(SqliteConnection sqliteConn, SqlConnection azureConn, string tableName, string[] columns)
        {
            try
            {
                Console.WriteLine($"[SYNC ALL] Syncing {tableName} from Azure...");

                var azureData = GetTableDataFromAzure(azureConn, tableName, columns);
                var localData = GetTableData(sqliteConn, tableName, columns);

                int newRecords = 0;
                int updatedRecords = 0;

                foreach (DataRow azureRow in azureData.Rows)
                {
                    var primaryKey = azureRow[columns[0]];
                    var localRow = localData.AsEnumerable().FirstOrDefault(r => r[columns[0]].ToString() == primaryKey.ToString());

                    if (localRow == null)
                    {
                        // Insert new record from Azure
                        InsertToLocal(sqliteConn, tableName, columns, azureRow);
                        newRecords++;
                        Console.WriteLine($"[SYNC ALL] {tableName}: Added new record {primaryKey}");
                    }
                    else
                    {
                        // Update existing record (Azure data wins)
                        UpdateLocal(sqliteConn, tableName, columns, azureRow);
                        updatedRecords++;
                        Console.WriteLine($"[SYNC ALL] {tableName}: Updated record {primaryKey}");
                    }
                }

                Console.WriteLine($"[SYNC ALL] {tableName}: {newRecords} new, {updatedRecords} updated records");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYNC ALL ERROR] Failed to sync {tableName}: {ex.Message}");
                throw;
            }
        }


    }
}