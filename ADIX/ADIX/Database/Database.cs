using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using System;
using System.IO;
using System.Data.Common;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data;

namespace ADIX
{
    public static class Database
    {
        private const string SqliteConnectionString = "Data Source=ADIX.db";
        public static string AzureSqlConnectionString { get; set; } = "";
        public static DatabaseType CurrentDatabaseType { get; set; } = DatabaseType.SQLite;

        // Track if sync is needed
        private static bool _syncRequired = false;

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
                    // Ping Google's DNS as a reliable internet check
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
                await SyncLocalToAzureAsync();
                _syncRequired = false;
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
                // Always initialize SQLite first (local database)
                InitializeSQLite();

                // Check internet and initialize Azure SQL if available
                if (!string.IsNullOrEmpty(AzureSqlConnectionString) && IsInternetAvailable())
                {
                    try
                    {
                        InitializeAzureSQL();
                        await SyncLocalToAzureAsync();
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

        /// <summary>
        /// Synchronous initialization (for backward compatibility)
        /// </summary>
        public static void Initialize()
        {
            InitializeAsync().Wait();
        }

        private static void InitializeSQLite()
        {
            using var connection = new SqliteConnection(SqliteConnectionString);
            connection.Open();

            // Enable foreign keys
            using var pragmaCmd = new SqliteCommand("PRAGMA foreign_keys = ON;", connection);
            pragmaCmd.ExecuteNonQuery();

            // Check if database is already initialized
            string checkQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name='SELLER'";
            using var checkCmd = new SqliteCommand(checkQuery, connection);
            var result = checkCmd.ExecuteScalar();

            if (result == null)
            {
                // Database doesn't exist, create it
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
                    commissionRate REAL CHECK(commissionRate >= 0 AND commissionRate <= 1)
                );

                CREATE TABLE IF NOT EXISTS SUPPLIER(
                    supplierID INTEGER NOT NULL PRIMARY KEY,
                    name TEXT NOT NULL,
                    contactInfo TEXT,
                    address TEXT
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
                    FOREIGN KEY(supplierID) REFERENCES SUPPLIER(supplierID),
                    FOREIGN KEY(sellerID) REFERENCES SELLER(sellerID)
                );

                CREATE TABLE IF NOT EXISTS CUSTOMER(
                    customerID INTEGER NOT NULL PRIMARY KEY,
                    name TEXT NOT NULL,
                    phone TEXT,
                    email TEXT,
                    credit REAL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS STAFF(
                    staffID INTEGER NOT NULL PRIMARY KEY,
                    name TEXT NOT NULL,
                    Role TEXT,
                    userName TEXT UNIQUE,
                    passwordHash TEXT,
                    salary REAL
                );

                CREATE TABLE IF NOT EXISTS INVOICEQUOTE(
                    invoiceQuoteID INTEGER NOT NULL PRIMARY KEY,
                    date TEXT NOT NULL,
                    type INTEGER NOT NULL CHECK(type IN (1,2)),
                    totalAmount REAL NOT NULL,
                    customerID INTEGER,
                    staffID INTEGER NOT NULL,
                    FOREIGN KEY(customerID) REFERENCES CUSTOMER(customerID),
                    FOREIGN KEY(staffID) REFERENCES STAFF(staffID)
                );

                CREATE TABLE IF NOT EXISTS REPORT(
                    reportID INTEGER NOT NULL PRIMARY KEY,
                    reportType INTEGER,
                    date TEXT,
                    staffID INTEGER,
                    FOREIGN KEY(staffID) REFERENCES STAFF(staffID)
                );

                CREATE TABLE IF NOT EXISTS INVOICEITEM(
                    invoiceItemID INTEGER NOT NULL PRIMARY KEY,
                    quantity INTEGER NOT NULL CHECK(quantity > 0),
                    priceAtSale REAL NOT NULL CHECK(priceAtSale >= 0),
                    itemID INTEGER NOT NULL,
                    invoiceQuoteID INTEGER NOT NULL,
                    FOREIGN KEY(invoiceQuoteID) REFERENCES INVOICEQUOTE(invoiceQuoteID),
                    FOREIGN KEY(itemID) REFERENCES ITEM(itemID)
                );

                CREATE INDEX IF NOT EXISTS idx_item_supplier ON ITEM(supplierID);
                CREATE INDEX IF NOT EXISTS idx_item_seller ON ITEM(sellerID);
                CREATE INDEX IF NOT EXISTS idx_invoice_customer ON INVOICEQUOTE(customerID);
                CREATE INDEX IF NOT EXISTS idx_invoice_staff ON INVOICEQUOTE(staffID);
                CREATE INDEX IF NOT EXISTS idx_invoice_date ON INVOICEQUOTE(date);
                CREATE INDEX IF NOT EXISTS idx_invoiceitem_invoice ON INVOICEITEM(invoiceQuoteID);
                CREATE INDEX IF NOT EXISTS idx_invoiceitem_item ON INVOICEITEM(itemID);
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
                    commissionRate FLOAT CHECK(commissionRate >= 0 AND commissionRate <= 1)
                );

                CREATE TABLE SUPPLIER(
                    supplierID INT NOT NULL PRIMARY KEY,
                    name NVARCHAR(255) NOT NULL,
                    contactInfo NVARCHAR(255),
                    address NVARCHAR(500)
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
                    FOREIGN KEY(supplierID) REFERENCES SUPPLIER(supplierID),
                    FOREIGN KEY(sellerID) REFERENCES SELLER(sellerID)
                );

                CREATE TABLE CUSTOMER(
                    customerID INT NOT NULL PRIMARY KEY,
                    name NVARCHAR(255) NOT NULL,
                    phone NVARCHAR(50),
                    email NVARCHAR(255),
                    credit FLOAT DEFAULT 0
                );

                CREATE TABLE STAFF(
                    staffID INT NOT NULL PRIMARY KEY,
                    name NVARCHAR(255) NOT NULL,
                    Role NVARCHAR(100),
                    userName NVARCHAR(100) UNIQUE,
                    passwordHash NVARCHAR(255),
                    salary FLOAT
                );

                CREATE TABLE INVOICEQUOTE(
                    invoiceQuoteID INT NOT NULL PRIMARY KEY,
                    date DATETIME NOT NULL,
                    type INT NOT NULL CHECK(type IN (1,2)),
                    totalAmount FLOAT NOT NULL,
                    customerID INT,
                    staffID INT NOT NULL,
                    FOREIGN KEY(customerID) REFERENCES CUSTOMER(customerID),
                    FOREIGN KEY(staffID) REFERENCES STAFF(staffID)
                );

                CREATE TABLE REPORT(
                    reportID INT NOT NULL PRIMARY KEY,
                    reportType INT,
                    date DATETIME,
                    staffID INT,
                    FOREIGN KEY(staffID) REFERENCES STAFF(staffID)
                );

                CREATE TABLE INVOICEITEM(
                    invoiceItemID INT NOT NULL PRIMARY KEY,
                    quantity INT NOT NULL CHECK(quantity > 0),
                    priceAtSale FLOAT NOT NULL CHECK(priceAtSale >= 0),
                    itemID INT NOT NULL,
                    invoiceQuoteID INT NOT NULL,
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

                INSERT INTO STAFF (staffID, name, Role, userName, passwordHash, salary) VALUES
                (1, 'Ruben Janse', 'Admin', 'ruben', 'hashedpassword1', 15000),
                (2, 'Sarah Ndlovu', 'Cashier', 'sarah', 'hashedpassword2', 9000),
                (3, 'Michael Smith', 'Manager', 'michael', 'hashedpassword3', 12000),
                (4, 'Emily Johnson', 'Cashier', 'emily', 'hashedpassword4', 9000);

                INSERT INTO CUSTOMER (customerID, name, phone, email, credit) VALUES
                (1, 'Alice Brown', '0712345678', 'alice@example.com', 100),
                (2, 'Bob White', '0723456789', 'bob@example.com', 50),
                (3, 'Charlie Green', '0734567890', 'charlie@example.com', 75);
            ";

            using var cmd = new SqlCommand(insertDataSql, connection);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Synchronizes all data from SQLite to Azure SQL
        /// </summary>
        public static async Task SyncLocalToAzureAsync()
        {
            if (string.IsNullOrEmpty(AzureSqlConnectionString))
            {
                throw new InvalidOperationException("Azure SQL connection string is not configured.");
            }

            await Task.Run(() => SyncLocalToAzure());
        }

        private static void SyncLocalToAzure()
        {
            using var sqliteConn = new SqliteConnection(SqliteConnectionString);
            using var azureConn = new SqlConnection(AzureSqlConnectionString);

            sqliteConn.Open();
            azureConn.Open();

            // Sync tables in order (respecting foreign key dependencies)
            SyncTable(sqliteConn, azureConn, "SELLER", new[] { "sellerID", "name", "contactInfo", "bankDetails", "commissionRate" });
            SyncTable(sqliteConn, azureConn, "SUPPLIER", new[] { "supplierID", "name", "contactInfo", "address" });
            SyncTable(sqliteConn, azureConn, "ITEM", new[] { "itemID", "description", "retailPrice", "costPrice", "stockQuantity", "stockSold", "supplierID", "sellerID" });
            SyncTable(sqliteConn, azureConn, "CUSTOMER", new[] { "customerID", "name", "phone", "email", "credit" });
            SyncTable(sqliteConn, azureConn, "STAFF", new[] { "staffID", "name", "Role", "userName", "passwordHash", "salary" });
            SyncTable(sqliteConn, azureConn, "INVOICEQUOTE", new[] { "invoiceQuoteID", "date", "type", "totalAmount", "customerID", "staffID" });
            SyncTable(sqliteConn, azureConn, "REPORT", new[] { "reportID", "reportType", "date", "staffID" });
            SyncTable(sqliteConn, azureConn, "INVOICEITEM", new[] { "invoiceItemID", "quantity", "priceAtSale", "itemID", "invoiceQuoteID" });

            Console.WriteLine("Database sync completed successfully.");
        }

        private static void SyncTable(SqliteConnection sqliteConn, SqlConnection azureConn, string tableName, string[] columns)
        {
            // Read all data from SQLite
            var dataTable = new DataTable();
            using (var sqliteCmd = new SqliteCommand($"SELECT * FROM {tableName}", sqliteConn))
            using (var reader = sqliteCmd.ExecuteReader())
            {
                dataTable.Load(reader);
            }

            if (dataTable.Rows.Count == 0)
            {
                Console.WriteLine($"No data to sync for table {tableName}");
                return;
            }

            // Clear Azure SQL table and insert fresh data
            using (var transaction = azureConn.BeginTransaction())
            {
                try
                {
                    // Delete existing data
                    using (var deleteCmd = new SqlCommand($"DELETE FROM {tableName}", azureConn, transaction))
                    {
                        deleteCmd.ExecuteNonQuery();
                    }

                    // Insert data from SQLite
                    foreach (DataRow row in dataTable.Rows)
                    {
                        var columnList = string.Join(", ", columns);
                        var paramList = string.Join(", ", columns.Select(c => $"@{c}"));
                        var insertSql = $"INSERT INTO {tableName} ({columnList}) VALUES ({paramList})";

                        using var insertCmd = new SqlCommand(insertSql, azureConn, transaction);
                        foreach (var column in columns)
                        {
                            var value = row[column];
                            insertCmd.Parameters.AddWithValue($"@{column}", value == DBNull.Value ? null : value);
                        }
                        insertCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    Console.WriteLine($"Synced {dataTable.Rows.Count} rows for table {tableName}");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception($"Failed to sync table {tableName}: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Call this after making changes to local database to mark sync as needed
        /// </summary>
        public static void MarkSyncRequired()
        {
            _syncRequired = true;
        }

        /// <summary>
        /// Check if sync is required
        /// </summary>
        public static bool IsSyncRequired()
        {
            return _syncRequired;
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
    }
}