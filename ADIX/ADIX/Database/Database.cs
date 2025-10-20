using Microsoft.Data.Sqlite;

namespace ADIX
{
    public static class Database
    {
        private const string ConnectionString = "Data Source=ADIX.db";

        public static void Initialize()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
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
                    CreateTables(connection);
                    InsertTestData(connection);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Database initialization failed: {ex.Message}", ex);
            }
        }

        internal static bool ValidateUser(string username, string password)
        {
            throw new NotImplementedException();
        }

        private static void CreateTables(SqliteConnection connection)
        {
            string createTablesSql = @"
                CREATE TABLE IF NOT EXISTS SELLER(
                    sellerID INTEGER NOT NULL PRIMARY KEY,
                    name TEXT NOT NULL,
                    contactInfo TEXT,
                    bankDetails TEXT,
                    commissionRate REAL CHECK(commissionRate >= 0 AND commissionRate <= 1)
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

        private static void InsertTestData(SqliteConnection connection)
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
    }
}