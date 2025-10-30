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
                // Get all Azure items
                var azureItems = GetTableDataFromAzure(azureConn, "ITEM",
                    new[] { "itemID", "sku", "itemGroup", "description", "retailPrice", "costPrice", "stockQuantity", "stockSold", "stockRecieved", "supplierID", "sellerID", "lastModified", "minimumStock" });

                // Get all local items
                var localItems = GetTableData(sqliteConn, "ITEM", new[] { "itemID" });

                var localItemIds = new HashSet<int>(localItems.AsEnumerable().Select(r => Convert.ToInt32(r["itemID"])));

                // Insert missing items from Azure
                foreach (DataRow azureRow in azureItems.Rows)
                {
                    var itemId = Convert.ToInt32(azureRow["itemID"]);

                    if (!localItemIds.Contains(itemId))
                    {
                        // This item exists in Azure but not locally - download it
                        var insertSql = @"
                        INSERT INTO ITEM 
                        (itemID, sku, itemGroup, description, retailPrice, costPrice, stockQuantity, stockSold, stockRecieved, supplierID, sellerID, lastModified, minimumStock)
                        VALUES 
                        (@itemID, @sku, @itemGroup, @description, @retailPrice, @costPrice, @stockQuantity, @stockSold, @stockRecieved, @supplierID, @sellerID, @lastModified, @minimumStock)";
                        using var cmd = new SqliteCommand(insertSql, sqliteConn);
                        cmd.Parameters.AddWithValue("@itemID", itemId);
                        cmd.Parameters.AddWithValue("@sku", azureRow["sku"] == DBNull.Value ? (object)DBNull.Value : azureRow["sku"]);
                        cmd.Parameters.AddWithValue("@itemGroup", azureRow["itemGroup"] == DBNull.Value ? (object)DBNull.Value : azureRow["itemGroup"]);
                        cmd.Parameters.AddWithValue("@description", azureRow["description"]);
                        cmd.Parameters.AddWithValue("@retailPrice", azureRow["retailPrice"]);
                        cmd.Parameters.AddWithValue("@costPrice", azureRow["costPrice"]);
                        cmd.Parameters.AddWithValue("@stockQuantity", azureRow["stockQuantity"]);
                        cmd.Parameters.AddWithValue("@stockSold", azureRow["stockSold"]);
                        cmd.Parameters.AddWithValue("@stockRecieved", azureRow["stockRecieved"]);
                        cmd.Parameters.AddWithValue("@minimumStock", azureRow["minimumStock"]);
                        cmd.Parameters.AddWithValue("@supplierID", azureRow["supplierID"] == DBNull.Value ? (object)DBNull.Value : azureRow["supplierID"]);
                        cmd.Parameters.AddWithValue("@sellerID", azureRow["sellerID"] == DBNull.Value ? (object)DBNull.Value : azureRow["sellerID"]);
                        cmd.Parameters.AddWithValue("@lastModified", azureRow["lastModified"]);

                        cmd.ExecuteNonQuery();
                        Console.WriteLine($"[SYNC] Downloaded missing item {itemId} from Azure with itemGroup: {azureRow["itemGroup"]}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading missing items: {ex.Message}");
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
        minimumStock INTEGER NOT NULL DEFAULT 0,
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
    CREATE TABLE IF NOT EXISTS EXPENSES(
                expenseID INTEGER PRIMARY KEY AUTOINCREMENT,
                expenseType TEXT NOT NULL,
                amount REAL NOT NULL,
                date TEXT NOT NULL,
                description TEXT,
                paymentMethod TEXT DEFAULT 'Cash',
                lastModified TEXT DEFAULT CURRENT_TIMESTAMP
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
        sku INT,
        itemGroup NVARCHAR(5),
        description NVARCHAR(500) NOT NULL,
        retailPrice FLOAT NOT NULL CHECK(retailPrice >= 0),
        costPrice FLOAT NOT NULL CHECK(costPrice >= 0),
        stockQuantity INT NOT NULL DEFAULT 0 CHECK(stockQuantity >= 0),
        stockRecieved INT NOT NULL,
        stockSold INT NOT NULL DEFAULT 0 CHECK(stockSold >= 0),
        supplierID INT,
        sellerID INT,
        lastModified DATETIME DEFAULT GETUTCDATE(),
        minimumStock INT NOT NULL DEFAULT 0,
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
        CREATE TABLE EXPENSES (
            expenseID INT IDENTITY(1,1) PRIMARY KEY,
            expenseType NVARCHAR(100) NOT NULL,
            amount FLOAT NOT NULL,
            date DATETIME NOT NULL,
            description NVARCHAR(500),
            paymentMethod NVARCHAR(50) DEFAULT 'Cash',
            lastModified DATETIME DEFAULT GETUTCDATE()
)

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
  INSERT INTO SELLER (sellerID, name, contactInfo, bankDetails, commissionRate) VALUES
(1, 'Robin Longbow', 'robin@archersguild.com', 'ACC-9876', 0.05),
(2, 'Marian Fletcher', 'marian@archeryworld.com', 'ACC-6543', 0.07),
(3, 'John Archer', 'john@bowpros.co.za', 'ACC-1122', 0.06);

    INSERT INTO SUPPLIER (supplierID, name, contactInfo, address) VALUES
(1, 'Precision Bows Ltd', 'sales@precisionbows.com', '45 Bowstring Lane, Johannesburg'),
(2, 'Eagle Arrows Co', 'info@eaglearrows.com', '210 Fletching Avenue, Cape Town'),
(3, 'TargetCraft Supplies', 'support@targetcraft.com', '33 Range Road, Durban'),
(4, 'African Archery Distributors', 'orders@africanarchery.co.za', '78 Safari Street, Pretoria');

    INSERT INTO ITEM (itemID, sku, itemGroup, description, retailPrice, costPrice, stockQuantity, stockRecieved, stockSold, supplierID, sellerID, minimumStock) VALUES
(1001, 1001, 'BOW', 'Longbow Elite', 4500.00, 3000.00, 15, 15, 0, 1, 1, 5),
(1002, 1002, 'BOW', 'Recurve Bow Pro', 5200.00, 3400.00, 10, 10, 0, 1, 2, 3),
(2001, 2001, 'ARR', 'Carbon Arrows (Pack of 12)', 850.00, 550.00, 50, 50, 0, 2, 1, 20),
(2002, 2002, 'ARR', 'Traditional Wooden Arrows', 600.00, 400.00, 40, 40, 0, 2, 2, 15),
(3001, 3001, 'TAR', 'Foam Target Block', 750.00, 500.00, 25, 25, 0, 3, 1, 10),
(3002, 3002, 'TAR', '3D Deer Target', 1800.00, 1200.00, 10, 10, 0, 3, 2, 3);


    INSERT INTO STAFF (staffID, name, Role, userName, passwordHash, salary) VALUES
(1, 'Ruben Janse', 'Admin', 'ruben', 'hashedpassword1', 15000.00),
(2, 'Sarah Ndlovu', 'Cashier', 'sarah', 'hashedpassword2', 9000.00),
(3, 'Michael Smith', 'Manager', 'michael', 'hashedpassword3', 12000.00),
(4, 'Emily Johnson', 'Cashier', 'emily', 'hashedpassword4', 9000.00),
(5, 'David Brown', 'Sales', 'david', 'hashedpassword5', 8000.00);

   INSERT INTO CUSTOMER (customerID, name, phone, email, credit) VALUES
(1, 'Alice Archer', '0712345678', 'alice@archerymail.com', 100.00),
(2, 'Bob Bowman', '0723456789', 'bob@archerymail.com', 50.00),
(3, 'Charlie Fletcher', '0734567890', 'charlie@archerymail.com', 75.00),
(4, 'Diana Huntress', '0745678901', 'diana@bowhunt.co.za', 0.00),
(5, 'Edward Marksman', '0756789012', 'edward@targetpro.com', 120.00);
";


            using var cmd = new SqliteCommand(insertDataSql, connection);
            cmd.ExecuteNonQuery();
        }

        private static void InsertTestDataAzureSQL(SqlConnection connection)
        {
            string insertDataSql = @"


      INSERT INTO SELLER (sellerID, name, contactInfo, bankDetails, commissionRate) VALUES
(1, 'Robin Longbow', 'robin@archersguild.com', 'ACC-9876', 0.05),
(2, 'Marian Fletcher', 'marian@archeryworld.com', 'ACC-6543', 0.07),
(3, 'John Archer', 'john@bowpros.co.za', 'ACC-1122', 0.06);

    INSERT INTO SUPPLIER (supplierID, name, contactInfo, address) VALUES
(1, 'Precision Bows Ltd', 'sales@precisionbows.com', '45 Bowstring Lane, Johannesburg'),
(2, 'Eagle Arrows Co', 'info@eaglearrows.com', '210 Fletching Avenue, Cape Town'),
(3, 'TargetCraft Supplies', 'support@targetcraft.com', '33 Range Road, Durban'),
(4, 'African Archery Distributors', 'orders@africanarchery.co.za', '78 Safari Street, Pretoria');

    INSERT INTO ITEM (itemID, sku, itemGroup, description, retailPrice, costPrice, stockQuantity, stockRecieved, stockSold, supplierID, sellerID, minimumStock) VALUES
(1001, 1001, 'BOW', 'Longbow Elite', 4500.00, 3000.00, 15, 15, 0, 1, 1, 5),
(1002, 1002, 'BOW', 'Recurve Bow Pro', 5200.00, 3400.00, 10, 10, 0, 1, 2, 3),
(2001, 2001, 'ARR', 'Carbon Arrows (Pack of 12)', 850.00, 550.00, 50, 50, 0, 2, 1, 20),
(2002, 2002, 'ARR', 'Traditional Wooden Arrows', 600.00, 400.00, 40, 40, 0, 2, 2, 15),
(3001, 3001, 'TAR', 'Foam Target Block', 750.00, 500.00, 25, 25, 0, 3, 1, 10),
(3002, 3002, 'TAR', '3D Deer Target', 1800.00, 1200.00, 10, 10, 0, 3, 2, 3);


    INSERT INTO STAFF (staffID, name, Role, userName, passwordHash, salary) VALUES
(1, 'Ruben Janse', 'Admin', 'ruben', 'hashedpassword1', 15000.00),
(2, 'Sarah Ndlovu', 'Cashier', 'sarah', 'hashedpassword2', 9000.00),
(3, 'Michael Smith', 'Manager', 'michael', 'hashedpassword3', 12000.00),
(4, 'Emily Johnson', 'Cashier', 'emily', 'hashedpassword4', 9000.00),
(5, 'David Brown', 'Sales', 'david', 'hashedpassword5', 8000.00);

   INSERT INTO CUSTOMER (customerID, name, phone, email, credit) VALUES
(1, 'Alice Archer', '0712345678', 'alice@archerymail.com', 100.00),
(2, 'Bob Bowman', '0723456789', 'bob@archerymail.com', 50.00),
(3, 'Charlie Fletcher', '0734567890', 'charlie@archerymail.com', 75.00),
(4, 'Diana Huntress', '0745678901', 'diana@bowhunt.co.za', 0.00),
(5, 'Edward Marksman', '0756789012', 'edward@targetpro.com', 120.00);
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
            SyncExpenses(sqliteConn, azureConn);
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
        /// <summary>
        /// Comprehensive sync that ensures all missing data is downloaded from Azure
        /// </summary>
        public static async Task<bool> SyncAllMissingDataAsync()
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
                await Task.Run(() => SyncAllMissingData());
                _syncRequired = false;
                _lastSyncTime = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Comprehensive sync failed: {ex.Message}");
                _syncRequired = true;
                return false;
            }
        }

        private static void SyncAllMissingData()
        {
            using var sqliteConn = new SqliteConnection(SqliteConnectionString);
            using var azureConn = new SqlConnection(AzureSqlConnectionString);

            sqliteConn.Open();
            azureConn.Open();

            Console.WriteLine("Starting comprehensive data sync...");

            // Step 1: Download all missing master data tables
            DownloadAllMissingMasterData(sqliteConn, azureConn);

            // Step 2: Download all missing items
            DownloadAllMissingItems(sqliteConn, azureConn);

            // Step 3: Download all missing transactions
            DownloadAllMissingTransactions(sqliteConn, azureConn);

            // Step 4: Upload any local data that's missing from Azure
            UploadMissingLocalData(sqliteConn, azureConn);

            Console.WriteLine("Comprehensive data sync completed successfully.");
        }
        private static void SyncExpenses(SqliteConnection sqliteConn, SqlConnection azureConn)
        {
            try
            {
                Console.WriteLine("[EXPENSES] Starting expense sync...");

                // Get local expenses
                var localExpenses = GetTableData(sqliteConn, "EXPENSES",
                    new[] { "expenseID", "expenseType", "amount", "date", "description", "paymentMethod", "lastModified" });

                // Get Azure expenses
                var azureExpenses = GetTableDataFromAzure(azureConn, "EXPENSES",
                    new[] { "expenseID", "expenseType", "amount", "date", "description", "paymentMethod", "lastModified" });

                using var transaction = azureConn.BeginTransaction();
                try
                {
                    // Upload local expenses to Azure
                    foreach (DataRow localRow in localExpenses.Rows)
                    {
                        var expenseID = Convert.ToInt32(localRow["expenseID"]);
                        var localModified = DateTime.Parse(localRow["lastModified"].ToString());

                        var azureRow = azureExpenses.AsEnumerable()
                            .FirstOrDefault(r => r["expenseID"].ToString() == expenseID.ToString());

                        if (azureRow == null)
                        {
                            // New expense - insert to Azure
                            var insertSql = @"
                                SET IDENTITY_INSERT EXPENSES ON;
                                INSERT INTO EXPENSES (expenseID, expenseType, amount, date, description, paymentMethod, lastModified)
                                VALUES (@expenseID, @expenseType, @amount, @date, @description, @paymentMethod, @lastModified);
                                SET IDENTITY_INSERT EXPENSES OFF;";

                            using var cmd = new SqlCommand(insertSql, azureConn, transaction);
                            cmd.Parameters.AddWithValue("@expenseID", expenseID);
                            cmd.Parameters.AddWithValue("@expenseType", localRow["expenseType"]);
                            cmd.Parameters.AddWithValue("@amount", localRow["amount"]);
                            cmd.Parameters.AddWithValue("@date", localRow["date"]);
                            cmd.Parameters.AddWithValue("@description", localRow["description"] == DBNull.Value ? (object)DBNull.Value : localRow["description"]);
                            cmd.Parameters.AddWithValue("@paymentMethod", localRow["paymentMethod"] == DBNull.Value ? "Cash" : localRow["paymentMethod"]);
                            cmd.Parameters.AddWithValue("@lastModified", localRow["lastModified"]);
                            cmd.ExecuteNonQuery();

                            Console.WriteLine($"[EXPENSES] Uploaded new expense {expenseID} to Azure");
                        }
                        else
                        {
                            // Expense exists - check which is newer
                            var azureModified = DateTime.Parse(azureRow["lastModified"].ToString());

                            if (localModified > azureModified)
                            {
                                // Local is newer - update Azure
                                var updateSql = @"
                                    UPDATE EXPENSES 
                                    SET expenseType=@expenseType, amount=@amount, date=@date, 
                                        description=@description, paymentMethod=@paymentMethod, 
                                        lastModified=@lastModified
                                    WHERE expenseID=@expenseID";

                                using var cmd = new SqlCommand(updateSql, azureConn, transaction);
                                cmd.Parameters.AddWithValue("@expenseID", expenseID);
                                cmd.Parameters.AddWithValue("@expenseType", localRow["expenseType"]);
                                cmd.Parameters.AddWithValue("@amount", localRow["amount"]);
                                cmd.Parameters.AddWithValue("@date", localRow["date"]);
                                cmd.Parameters.AddWithValue("@description", localRow["description"] == DBNull.Value ? (object)DBNull.Value : localRow["description"]);
                                cmd.Parameters.AddWithValue("@paymentMethod", localRow["paymentMethod"] == DBNull.Value ? "Cash" : localRow["paymentMethod"]);
                                cmd.Parameters.AddWithValue("@lastModified", localRow["lastModified"]);
                                cmd.ExecuteNonQuery();

                                Console.WriteLine($"[EXPENSES] Updated expense {expenseID} in Azure (local newer)");
                            }
                        }
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception($"Failed to upload expenses to Azure: {ex.Message}", ex);
                }

                // Download expenses from Azure to Local
                Console.WriteLine("[EXPENSES] Downloading changes from Azure to Local...");

                foreach (DataRow azureRow in azureExpenses.Rows)
                {
                    var expenseID = Convert.ToInt32(azureRow["expenseID"]);
                    var azureModified = DateTime.Parse(azureRow["lastModified"].ToString());

                    var localRow = localExpenses.AsEnumerable()
                        .FirstOrDefault(r => r["expenseID"].ToString() == expenseID.ToString());

                    if (localRow == null)
                    {
                        // New expense from Azure - insert to local
                        var insertLocalSql = @"
                            INSERT INTO EXPENSES (expenseID, expenseType, amount, date, description, paymentMethod, lastModified)
                            VALUES (@expenseID, @expenseType, @amount, @date, @description, @paymentMethod, @lastModified)";

                        using var cmd = new SqliteCommand(insertLocalSql, sqliteConn);
                        cmd.Parameters.AddWithValue("@expenseID", expenseID);
                        cmd.Parameters.AddWithValue("@expenseType", azureRow["expenseType"]);
                        cmd.Parameters.AddWithValue("@amount", azureRow["amount"]);
                        cmd.Parameters.AddWithValue("@date", azureRow["date"]);
                        cmd.Parameters.AddWithValue("@description", azureRow["description"] == DBNull.Value ? (object)DBNull.Value : azureRow["description"]);
                        cmd.Parameters.AddWithValue("@paymentMethod", azureRow["paymentMethod"] == DBNull.Value ? "Cash" : azureRow["paymentMethod"]);
                        cmd.Parameters.AddWithValue("@lastModified", azureRow["lastModified"]);
                        cmd.ExecuteNonQuery();

                        Console.WriteLine($"[EXPENSES] Downloaded new expense {expenseID} from Azure");
                    }
                    else
                    {
                        // Expense exists - check which is newer
                        var localModified = DateTime.Parse(localRow["lastModified"].ToString());

                        if (azureModified > localModified)
                        {
                            // Azure is newer - update local
                            var updateLocalSql = @"
                                UPDATE EXPENSES 
                                SET expenseType=@expenseType, amount=@amount, date=@date, 
                                    description=@description, paymentMethod=@paymentMethod, 
                                    lastModified=@lastModified
                                WHERE expenseID=@expenseID";

                            using var cmd = new SqliteCommand(updateLocalSql, sqliteConn);
                            cmd.Parameters.AddWithValue("@expenseID", expenseID);
                            cmd.Parameters.AddWithValue("@expenseType", azureRow["expenseType"]);
                            cmd.Parameters.AddWithValue("@amount", azureRow["amount"]);
                            cmd.Parameters.AddWithValue("@date", azureRow["date"]);
                            cmd.Parameters.AddWithValue("@description", azureRow["description"] == DBNull.Value ? (object)DBNull.Value : azureRow["description"]);
                            cmd.Parameters.AddWithValue("@paymentMethod", azureRow["paymentMethod"] == DBNull.Value ? "Cash" : azureRow["paymentMethod"]);
                            cmd.Parameters.AddWithValue("@lastModified", azureRow["lastModified"]);
                            cmd.ExecuteNonQuery();

                            Console.WriteLine($"[EXPENSES] Updated local expense {expenseID} from Azure (Azure newer)");
                        }
                    }
                }

                Console.WriteLine("[EXPENSES] Expense sync completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EXPENSES] Error syncing expenses: {ex.Message}");
                throw;
            }
        }
        private static void DownloadAllMissingMasterData(SqliteConnection sqliteConn, SqlConnection azureConn)
        {
            var masterTables = new[]
            {
        new { Table = "SELLER", Columns = new[] { "sellerID", "name", "contactInfo", "bankDetails", "commissionRate", "lastModified" } },
        new { Table = "SUPPLIER", Columns = new[] { "supplierID", "name", "contactInfo", "address", "lastModified" } },
        new { Table = "CUSTOMER", Columns = new[] { "customerID", "name", "phone", "email", "credit", "lastModified" } },
        new { Table = "STAFF", Columns = new[] { "staffID", "name", "Role", "userName", "passwordHash", "salary", "lastModified" } }
    };

            foreach (var table in masterTables)
            {
                DownloadMissingTableData(sqliteConn, azureConn, table.Table, table.Columns);
            }
        }

        private static void DownloadMissingTableData(SqliteConnection sqliteConn, SqlConnection azureConn, string tableName, string[] columns)
        {
            try
            {
                Console.WriteLine($"[SYNC] Downloading missing {tableName} data...");

                // Get Azure data
                var azureData = GetTableDataFromAzure(azureConn, tableName, columns);

                // Get local data IDs
                var localData = GetTableData(sqliteConn, tableName, new[] { columns[0] });
                var localIds = new HashSet<string>(localData.AsEnumerable().Select(r => r[columns[0]].ToString()));

                // Insert missing records
                foreach (DataRow azureRow in azureData.Rows)
                {
                    var primaryKey = azureRow[columns[0]].ToString();

                    if (!localIds.Contains(primaryKey))
                    {
                        InsertToLocal(sqliteConn, tableName, columns, azureRow);
                        Console.WriteLine($"[SYNC] Downloaded {tableName} record {primaryKey} from Azure");
                    }
                }

                Console.WriteLine($"[SYNC] Completed {tableName} sync");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYNC] Error downloading {tableName}: {ex.Message}");
            }
        }

        private static void DownloadAllMissingItems(SqliteConnection sqliteConn, SqlConnection azureConn)
        {
            try
            {
                Console.WriteLine("[SYNC] Downloading all missing items...");

                // Get all Azure items
                var azureItems = GetTableDataFromAzure(azureConn, "ITEM",
             new[] { "itemID", "sku", "itemGroup", "description", "retailPrice", "costPrice", "stockQuantity", "stockSold", "stockRecieved", "supplierID", "sellerID", "lastModified", "minimumStock" });

                // Get all local item IDs
                var localItems = GetTableData(sqliteConn, "ITEM", new[] { "itemID" });
                var localItemIds = new HashSet<string>(localItems.AsEnumerable().Select(r => r["itemID"].ToString()));

                // Insert missing items
                foreach (DataRow azureRow in azureItems.Rows)
                {
                    var itemId = azureRow["itemID"].ToString();

                    if (!localItemIds.Contains(itemId))
                    {
                        var insertSql = @"
INSERT INTO ITEM 
(itemID, sku, itemGroup, description, retailPrice, costPrice, stockQuantity, stockSold, stockRecieved, supplierID, sellerID, lastModified, minimumStock)
VALUES 
(@itemID, @sku, @itemGroup, @description, @retailPrice, @costPrice, @stockQuantity, @stockSold, @stockRecieved, @supplierID, @sellerID, @lastModified, @minimumStock)";

                        using var cmd = new SqliteCommand(insertSql, sqliteConn);

                        cmd.Parameters.AddWithValue("@itemID", azureRow["itemID"]);
                        cmd.Parameters.AddWithValue("@sku", azureRow["sku"] == DBNull.Value ? (object)DBNull.Value : azureRow["sku"]);
                        cmd.Parameters.AddWithValue("@itemGroup", azureRow["itemGroup"] == DBNull.Value ? (object)DBNull.Value : azureRow["itemGroup"]);
                        cmd.Parameters.AddWithValue("@description", azureRow["description"]);
                        cmd.Parameters.AddWithValue("@retailPrice", azureRow["retailPrice"]);
                        cmd.Parameters.AddWithValue("@costPrice", azureRow["costPrice"]);
                        cmd.Parameters.AddWithValue("@stockQuantity", azureRow["stockQuantity"]);
                        cmd.Parameters.AddWithValue("@stockSold", azureRow["stockSold"]);
                        cmd.Parameters.AddWithValue("@stockRecieved", azureRow["stockRecieved"]);
                        cmd.Parameters.AddWithValue("@minimumStock", azureRow["minimumStock"]);
                        cmd.Parameters.AddWithValue("@supplierID", azureRow["supplierID"] == DBNull.Value ? (object)DBNull.Value : azureRow["supplierID"]);
                        cmd.Parameters.AddWithValue("@sellerID", azureRow["sellerID"] == DBNull.Value ? (object)DBNull.Value : azureRow["sellerID"]);
                        cmd.Parameters.AddWithValue("@lastModified", azureRow["lastModified"]);

                        cmd.ExecuteNonQuery();
                        Console.WriteLine($"[SYNC] Downloaded missing item {itemId} from Azure");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYNC] Error downloading items: {ex.Message}");
            }
        }

        private static void DownloadAllMissingTransactions(SqliteConnection sqliteConn, SqlConnection azureConn)
        {
            try
            {
                Console.WriteLine("[SYNC] Downloading missing transactions...");

                // Download missing invoices
                var azureInvoices = GetTableDataFromAzure(azureConn, "INVOICEQUOTE",
                    new[] { "invoiceQuoteID", "date", "type", "totalAmount", "customerID", "staffID", "paymentMethod", "paymentStatus", "lastModified" });

                var localInvoices = GetTableData(sqliteConn, "INVOICEQUOTE", new[] { "invoiceQuoteID" });
                var localInvoiceIds = new HashSet<string>(localInvoices.AsEnumerable().Select(r => r["invoiceQuoteID"].ToString()));

                foreach (DataRow azureRow in azureInvoices.Rows)
                {
                    var invoiceId = azureRow["invoiceQuoteID"].ToString();

                    if (!localInvoiceIds.Contains(invoiceId))
                    {
                        // Insert invoice
                        var insertInvoiceSql = @"
                INSERT INTO INVOICEQUOTE 
                (invoiceQuoteID, date, type, totalAmount, customerID, staffID, paymentMethod, paymentStatus, lastModified, synced)
                VALUES 
                (@invoiceQuoteID, @date, @type, @totalAmount, @customerID, @staffID, @paymentMethod, @paymentStatus, @lastModified, 1)";

                        using var cmd = new SqliteCommand(insertInvoiceSql, sqliteConn);

                        cmd.Parameters.AddWithValue("@invoiceQuoteID", azureRow["invoiceQuoteID"]);
                        cmd.Parameters.AddWithValue("@date", azureRow["date"]);
                        cmd.Parameters.AddWithValue("@type", azureRow["type"]);
                        cmd.Parameters.AddWithValue("@totalAmount", azureRow["totalAmount"]);
                        cmd.Parameters.AddWithValue("@customerID", azureRow["customerID"] == DBNull.Value ? (object)DBNull.Value : azureRow["customerID"]);
                        cmd.Parameters.AddWithValue("@staffID", azureRow["staffID"]);
                        cmd.Parameters.AddWithValue("@paymentMethod", azureRow["paymentMethod"] == DBNull.Value ? (object)DBNull.Value : azureRow["paymentMethod"]);
                        cmd.Parameters.AddWithValue("@paymentStatus", azureRow["paymentStatus"] == DBNull.Value ? (object)DBNull.Value : azureRow["paymentStatus"]);
                        cmd.Parameters.AddWithValue("@lastModified", azureRow["lastModified"]);

                        cmd.ExecuteNonQuery();

                        // Download invoice items for this invoice
                        DownloadInvoiceItemsForInvoice(sqliteConn, azureConn, Convert.ToInt64(azureRow["invoiceQuoteID"]));

                        Console.WriteLine($"[SYNC] Downloaded invoice {invoiceId} from Azure");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYNC] Error downloading transactions: {ex.Message}");
            }
        }

        private static void DownloadInvoiceItemsForInvoice(SqliteConnection sqliteConn, SqlConnection azureConn, long invoiceId)
        {
            try
            {
                var azureItems = GetTableDataFromAzure(azureConn, "INVOICEITEM",
                    new[] { "invoiceItemID", "quantity", "priceAtSale", "itemID", "invoiceQuoteID", "lastModified" },
                    $"WHERE invoiceQuoteID = {invoiceId}");

                var localItems = GetTableData(sqliteConn, "INVOICEITEM", new[] { "invoiceItemID" }, $"WHERE invoiceQuoteID = {invoiceId}");
                var localItemIds = new HashSet<string>(localItems.AsEnumerable().Select(r => r["invoiceItemID"].ToString()));

                foreach (DataRow azureRow in azureItems.Rows)
                {
                    var itemId = azureRow["invoiceItemID"].ToString();

                    if (!localItemIds.Contains(itemId))
                    {
                        var insertSql = @"
                INSERT INTO INVOICEITEM 
                (invoiceItemID, quantity, priceAtSale, itemID, invoiceQuoteID, lastModified, synced)
                VALUES 
                (@invoiceItemID, @quantity, @priceAtSale, @itemID, @invoiceQuoteID, @lastModified, 1)";

                        using var cmd = new SqliteCommand(insertSql, sqliteConn);

                        cmd.Parameters.AddWithValue("@invoiceItemID", azureRow["invoiceItemID"]);
                        cmd.Parameters.AddWithValue("@quantity", azureRow["quantity"]);
                        cmd.Parameters.AddWithValue("@priceAtSale", azureRow["priceAtSale"]);
                        cmd.Parameters.AddWithValue("@itemID", azureRow["itemID"]);
                        cmd.Parameters.AddWithValue("@invoiceQuoteID", azureRow["invoiceQuoteID"]);
                        cmd.Parameters.AddWithValue("@lastModified", azureRow["lastModified"]);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYNC] Error downloading invoice items for invoice {invoiceId}: {ex.Message}");
            }
        }

        private static void UploadMissingLocalData(SqliteConnection sqliteConn, SqlConnection azureConn)
        {
            // This will use your existing SyncTransactionBased method to upload local data
            SyncTransactions(sqliteConn, azureConn);
        }

        // Helper method to get table data with WHERE clause
        private static DataTable GetTableDataFromAzure(SqlConnection connection, string tableName, string[] columns, string whereClause = "")
        {
            var dataTable = new DataTable();
            var columnList = string.Join(", ", columns);
            var sql = $"SELECT {columnList} FROM {tableName}";

            if (!string.IsNullOrEmpty(whereClause))
            {
                sql += " " + whereClause;
            }

            using var cmd = new SqlCommand(sql, connection);
            using var reader = cmd.ExecuteReader();
            dataTable.Load(reader);
            return dataTable;
        }

        private static DataTable GetTableData(SqliteConnection connection, string tableName, string[] columns, string whereClause = "")
        {
            var dataTable = new DataTable();
            var columnList = string.Join(", ", columns);
            var sql = $"SELECT {columnList} FROM {tableName}";

            if (!string.IsNullOrEmpty(whereClause))
            {
                sql += " " + whereClause;
            }

            using var cmd = new SqliteCommand(sql, connection);
            using var reader = cmd.ExecuteReader();
            dataTable.Load(reader);
            return dataTable;
        }
        private static void SyncItemMasterDataWithoutInventory(SqliteConnection sqliteConn, SqlConnection azureConn)
        {
            // FIXED: Now includes stockRecieved in sync
            string[] columns = {
        "itemID",
        "sku",
        "itemGroup",
        "description",
        "retailPrice",
        "costPrice",
        "stockRecieved",  // ← ADDED THIS
        "minimumStock",
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
                        // New item - insert to Azure with stockRecieved
                        var insertSql = @"
INSERT INTO ITEM 
(itemID, sku, itemGroup, description, retailPrice, costPrice, stockQuantity, stockSold, stockRecieved, supplierID, sellerID, lastModified, minimumStock)
VALUES 
(@itemID, @sku, @itemGroup, @description, @retailPrice, @costPrice, 0, 0, @stockRecieved, @supplierID, @sellerID, @lastModified, @minimumStock)";

                        using var cmd = new SqlCommand(insertSql, azureConn, transaction);
                        cmd.Parameters.AddWithValue("@itemID", itemID);
                        cmd.Parameters.AddWithValue("@sku", localRow["sku"] == DBNull.Value ? (object)DBNull.Value : localRow["sku"]);
                        cmd.Parameters.AddWithValue("@itemGroup", localRow["itemGroup"] == DBNull.Value ? (object)DBNull.Value : localRow["itemGroup"]);
                        cmd.Parameters.AddWithValue("@description", localRow["description"]);
                        cmd.Parameters.AddWithValue("@retailPrice", localRow["retailPrice"]);
                        cmd.Parameters.AddWithValue("@costPrice", localRow["costPrice"]);
                        cmd.Parameters.AddWithValue("@stockRecieved", localRow["stockRecieved"]);  // ← SYNCING THIS NOW
                        cmd.Parameters.AddWithValue("@minimumStock", localRow["minimumStock"]);
                        cmd.Parameters.AddWithValue("@supplierID", localRow["supplierID"] == DBNull.Value ? (object)DBNull.Value : localRow["supplierID"]);
                        cmd.Parameters.AddWithValue("@sellerID", localRow["sellerID"] == DBNull.Value ? (object)DBNull.Value : localRow["sellerID"]);
                        cmd.Parameters.AddWithValue("@lastModified", localRow["lastModified"]);
                        cmd.ExecuteNonQuery();

                        Console.WriteLine($"[ITEM] Added new item {itemID} to Azure with stockRecieved: {localRow["stockRecieved"]}");
                    }
                    else
                    {
                        // Item exists - check which is newer
                        var localModified = DateTime.Parse(localRow["lastModified"].ToString());
                        var azureModified = DateTime.Parse(azureRow["lastModified"].ToString());

                        if (localModified > azureModified)
                        {
                            // Local is newer - update Azure INCLUDING stockRecieved
                            var updateSql = @"
UPDATE ITEM 
SET description=@description, 
    sku=@sku,
    itemGroup=@itemGroup,
    retailPrice=@retailPrice, 
    costPrice=@costPrice, 
    stockRecieved=@stockRecieved,
    supplierID=@supplierID, 
    sellerID=@sellerID,
    minimumStock=@minimumStock,
    lastModified=@lastModified
WHERE itemID=@itemID";

                            using var cmd = new SqlCommand(updateSql, azureConn, transaction);
                            cmd.Parameters.AddWithValue("@itemID", itemID);
                            cmd.Parameters.AddWithValue("@description", localRow["description"]);
                            cmd.Parameters.AddWithValue("@sku", localRow["sku"] == DBNull.Value ? (object)DBNull.Value : localRow["sku"]);
                            cmd.Parameters.AddWithValue("@itemGroup", localRow["itemGroup"] == DBNull.Value ? (object)DBNull.Value : localRow["itemGroup"]);
                            cmd.Parameters.AddWithValue("@retailPrice", localRow["retailPrice"]);
                            cmd.Parameters.AddWithValue("@costPrice", localRow["costPrice"]);
                            cmd.Parameters.AddWithValue("@stockRecieved", localRow["stockRecieved"]);  // ← SYNCING THIS NOW
                            cmd.Parameters.AddWithValue("@supplierID", localRow["supplierID"] == DBNull.Value ? (object)DBNull.Value : localRow["supplierID"]);
                            cmd.Parameters.AddWithValue("@sellerID", localRow["sellerID"] == DBNull.Value ? (object)DBNull.Value : localRow["sellerID"]);
                            cmd.Parameters.AddWithValue("@minimumStock", localRow["minimumStock"]);
                            cmd.Parameters.AddWithValue("@lastModified", localRow["lastModified"]);
                            cmd.ExecuteNonQuery();

                            Console.WriteLine($"[ITEM] Updated item {itemID} in Azure (local newer) - stockRecieved: {localRow["stockRecieved"]}");
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

            Console.WriteLine("[ITEM] Downloading changes from Azure to Local...");

            // Download updates from Azure to Local
            foreach (DataRow azureRow in azureData.Rows)
            {
                var itemID = Convert.ToInt32(azureRow["itemID"]);
                var localRow = localData.AsEnumerable()
                    .FirstOrDefault(r => r["itemID"].ToString() == itemID.ToString());

                if (localRow == null)
                {
                    // Item exists in Azure but not locally - handled by DownloadMissingItemsFromAzure
                    continue;
                }

                var localModified = DateTime.Parse(localRow["lastModified"].ToString());
                var azureModified = DateTime.Parse(azureRow["lastModified"].ToString());

                // Azure is newer - update local
                if (azureModified > localModified)
                {
                    var updateLocalSql = @"
UPDATE ITEM 
SET description=@description, 
    sku=@sku,
    itemGroup=@itemGroup,
    retailPrice=@retailPrice, 
    costPrice=@costPrice, 
    stockRecieved=@stockRecieved,
    supplierID=@supplierID, 
    sellerID=@sellerID,
    minimumStock=@minimumStock,
    lastModified=@lastModified
WHERE itemID=@itemID";

                    using var cmd = new SqliteCommand(updateLocalSql, sqliteConn);
                    cmd.Parameters.AddWithValue("@itemID", itemID);
                    cmd.Parameters.AddWithValue("@description", azureRow["description"]);
                    cmd.Parameters.AddWithValue("@sku", azureRow["sku"] == DBNull.Value ? (object)DBNull.Value : azureRow["sku"]);
                    cmd.Parameters.AddWithValue("@itemGroup", azureRow["itemGroup"] == DBNull.Value ? (object)DBNull.Value : azureRow["itemGroup"]);
                    cmd.Parameters.AddWithValue("@retailPrice", azureRow["retailPrice"]);
                    cmd.Parameters.AddWithValue("@costPrice", azureRow["costPrice"]);
                    cmd.Parameters.AddWithValue("@stockRecieved", azureRow["stockRecieved"]);  // ← SYNCING THIS NOW
                    cmd.Parameters.AddWithValue("@supplierID", azureRow["supplierID"] == DBNull.Value ? (object)DBNull.Value : azureRow["supplierID"]);
                    cmd.Parameters.AddWithValue("@sellerID", azureRow["sellerID"] == DBNull.Value ? (object)DBNull.Value : azureRow["sellerID"]);
                    cmd.Parameters.AddWithValue("@minimumStock", azureRow["minimumStock"]);
                    cmd.Parameters.AddWithValue("@lastModified", azureRow["lastModified"]);
                    cmd.ExecuteNonQuery();

                    Console.WriteLine($"[ITEM] Updated local item {itemID} from Azure (Azure newer) - stockRecieved: {azureRow["stockRecieved"]}");
                }
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
            paymentMethod TEXT DEFAULT 'Cash',
            lastModified TEXT DEFAULT CURRENT_TIMESTAMP
        )";

            using var cmd = new SqliteCommand(createTableSql, connection);
            cmd.ExecuteNonQuery();

            // Update schema to ensure paymentMethod column exists
            UpdateExpensesTableSchema();

            // Insert sample expenses if table is empty
            string checkDataSql = "SELECT COUNT(*) FROM EXPENSES";
            using var checkCmd = new SqliteCommand(checkDataSql, connection);
            var count = Convert.ToInt32(checkCmd.ExecuteScalar());

            if (count == 0)
            {
                string insertSampleData = @"
            INSERT INTO EXPENSES (expenseType, amount, date, description, paymentMethod) VALUES
            ('Rent', 15000.00, date('now', 'start of month'), 'Monthly rent payment', 'EFT'),
            ('Utilities', 2500.00, date('now', 'start of month'), 'Electricity, water, internet', 'EFT'),
            ('Salaries', 45000.00, date('now', 'start of month'), 'Staff salaries', 'EFT'),
            ('Other', 5000.00, date('now', 'start of month'), 'Miscellaneous expenses', 'Cash')";

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


        /// <summary>
        /// Add a new expense to the EXPENSES table
        /// </summary>
        public static void AddExpense(string expenseType, double amount, string date, string description = "")
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            // Ensure expenses table exists
            InitializeExpensesTable();

            string insertSql = @"
        INSERT INTO EXPENSES (expenseType, amount, date, description, paymentMethod)
        VALUES (@expenseType, @amount, @date, @description, @paymentMethod)";

            using var cmd = new SqliteCommand(insertSql, connection);
            cmd.Parameters.AddWithValue("@expenseType", expenseType);
            cmd.Parameters.AddWithValue("@amount", amount);
            cmd.Parameters.AddWithValue("@date", date);
            cmd.Parameters.AddWithValue("@description", description);
            cmd.Parameters.AddWithValue("@paymentMethod", "Cash"); // Default payment method
            cmd.ExecuteNonQuery();

            Console.WriteLine($"Added expense: {expenseType} - R {amount:N2}");
        }



        /// <summary>
        /// Get all expenses for chart data
        /// </summary>
        public static DataTable GetExpensesForCharts()
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            // Ensure expenses table exists
            InitializeExpensesTable();

            string query = @"
        SELECT expenseType, SUM(amount) as TotalAmount
        FROM EXPENSES
        WHERE date >= date('now', '-30 days')
        GROUP BY expenseType
        ORDER BY TotalAmount DESC";

            using var cmd = new SqliteCommand(query, connection);
            var dataTable = new DataTable();
            using var reader = cmd.ExecuteReader();
            dataTable.Load(reader);

            return dataTable;
        }

        /// <summary>
        /// Get monthly expense trend data
        /// </summary>
        public static Dictionary<string, double> GetMonthlyExpenseTrend()
        {
            var trendData = new Dictionary<string, double>();

            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            // Ensure expenses table exists
            InitializeExpensesTable();

            string query = @"
        SELECT 
            strftime('%Y-%m', date) as Month,
            SUM(amount) as MonthlyTotal
        FROM EXPENSES
        WHERE date >= date('now', '-6 months')
        GROUP BY strftime('%Y-%m', date)
        ORDER BY Month DESC
        LIMIT 6";

            using var cmd = new SqliteCommand(query, connection);
            using var reader = cmd.ExecuteReader();

            var months = new List<string>();
            var amounts = new List<double>();

            while (reader.Read())
            {
                string month = reader["Month"].ToString();
                double amount = Convert.ToDouble(reader["MonthlyTotal"]);
                trendData[month] = amount;
            }

            return trendData;
        }

        /// <summary>
        /// Process a salary payment and record it as an expense
        /// </summary>
        public static void ProcessSalaryPayment(int staffID, double amount, string date, string paymentMethod = "EFT", string description = "Salary Payment")
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            // Ensure expenses table exists with correct schema
            InitializeExpensesTable();

            // Add salary payment to expenses
            string insertExpenseSql = @"
        INSERT INTO EXPENSES (expenseType, amount, date, description, paymentMethod)
        VALUES (@expenseType, @amount, @date, @description, @paymentMethod)";

            using var expenseCmd = new SqliteCommand(insertExpenseSql, connection);
            expenseCmd.Parameters.AddWithValue("@expenseType", "Salary Payment");
            expenseCmd.Parameters.AddWithValue("@amount", amount);
            expenseCmd.Parameters.AddWithValue("@date", date);
            expenseCmd.Parameters.AddWithValue("@description", $"Salary payment for staff ID: {staffID} - {description}");
            expenseCmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
            expenseCmd.ExecuteNonQuery();

            MarkSyncRequired();
            Console.WriteLine($"Processed salary payment: Staff {staffID} - R {amount:N2}");
        }

        /// <summary>
        /// Get all staff members with their salary information
        /// </summary>
        public static DataTable GetStaffWithSalaries()
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            string query = @"
        SELECT 
            staffID,
            name,
            Role,
            salary,
            userName,
            lastModified
        FROM STAFF 
        ORDER BY name";

            using var cmd = new SqliteCommand(query, connection);
            var dataTable = new DataTable();
            using var reader = cmd.ExecuteReader();
            dataTable.Load(reader);

            return dataTable;
        }

     

        /// <summary>
        /// Get all expenses for display in the finance page
        /// </summary>
        public static DataTable GetExpensesForDisplay()
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            // Ensure expenses table exists
            InitializeExpensesTable();

            string query = @"
        SELECT 
            expenseID as ID,
            expenseType as Category,
            amount as Amount,
            date as Date,
            description as Description,
            lastModified
        FROM EXPENSES
        ORDER BY date DESC, expenseID DESC";

            using var cmd = new SqliteCommand(query, connection);
            var dataTable = new DataTable();
            using var reader = cmd.ExecuteReader();
            dataTable.Load(reader);

            return dataTable;
        }

        /// <summary>
        /// Get salary payment history
        /// </summary>
        public static DataTable GetSalaryPaymentHistory()
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            string query = @"
        SELECT 
            e.expenseID as PaymentID,
            s.staffID,
            s.name as StaffName,
            s.Role,
            e.amount as Amount,
            e.date as PaymentDate,
            e.description as Description
        FROM EXPENSES e
        INNER JOIN STAFF s ON e.description LIKE '%' || s.name || '%' OR e.description LIKE '%Staff ID: ' || s.staffID || '%'
        WHERE e.expenseType = 'Salary Payment'
        ORDER BY e.date DESC, e.expenseID DESC";

            using var cmd = new SqliteCommand(query, connection);
            var dataTable = new DataTable();
            using var reader = cmd.ExecuteReader();
            dataTable.Load(reader);

            return dataTable;
        }

        /// <summary>
        /// Update staff salary in the STAFF table
        /// </summary>
        public static void UpdateStaffSalary(int staffID, double newSalary)
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            string updateSql = @"
        UPDATE STAFF 
        SET salary = @salary, 
            lastModified = CURRENT_TIMESTAMP 
        WHERE staffID = @staffID";

            using var cmd = new SqliteCommand(updateSql, connection);
            cmd.Parameters.AddWithValue("@salary", newSalary);
            cmd.Parameters.AddWithValue("@staffID", staffID);
            cmd.ExecuteNonQuery();

            MarkSyncRequired();
            Console.WriteLine($"Updated salary for staff {staffID} to R {newSalary:N2}");
        }

        /// <summary>
        /// Update EXPENSES table schema to include paymentMethod column
        /// </summary>
        public static void UpdateExpensesTableSchema()
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            // Check if paymentMethod column exists
            string checkColumnSql = @"
        SELECT COUNT(*) FROM pragma_table_info('EXPENSES') 
        WHERE name = 'paymentMethod'";

            using var checkCmd = new SqliteCommand(checkColumnSql, connection);
            var columnExists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

            if (!columnExists)
            {
                // Add paymentMethod column
                string alterTableSql = @"
            ALTER TABLE EXPENSES 
            ADD COLUMN paymentMethod TEXT DEFAULT 'Cash'";

                using var alterCmd = new SqliteCommand(alterTableSql, connection);
                alterCmd.ExecuteNonQuery();
                Console.WriteLine("Added paymentMethod column to EXPENSES table");
            }
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