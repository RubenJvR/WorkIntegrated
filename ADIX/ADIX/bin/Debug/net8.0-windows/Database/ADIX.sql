
--needed for foreign keys
PRAGMA foreign_keys = ON;

--for those who are unfamiliar with SQLITE does 
--not have date or DECIMAL data types, instead we use
--the following datatypes

CREATE TABLE IF NOT EXISTS SELLER(
    sellerID INTEGER NOT NULL PRIMARY KEY,
    name TEXT,
    contactInfo TEXT,
    bankDetails TEXT,
    commissionRate REAL

);

CREATE TABLE IF NOT EXISTS SUPPLIER(
    supplierID INTEGER NOT NULL PRIMARY KEY,
    name TEXT,
    contactInfo TEXT,
    address TEXT
);

CREATE TABLE IF NOT EXISTS ITEM(

    itemID INTEGER NOT NULL PRIMARY KEY,
    description TEXT,
    retailPrice REAL,
    costPrice REAL,
    stockQuantity INTEGER,
    stockSold INTEGER,
    supplierID INTEGER,
    sellerID INTEGER,
    FOREIGN KEY(supplierID) REFERENCES SUPPLIER(supplierID),
    FOREIGN KEY(sellerID) REFERENCES SELLER(sellerID)
);



CREATE TABLE IF NOT EXISTS CUSTOMER(
    customerID INTEGER NOT NULL PRIMARY KEY,
    name TEXT,
    phone TEXT,
    email TEXT,
    credit REAL
);

CREATE TABLE IF NOT EXISTS STAFF(
    staffID INTEGER NOT NULL PRIMARY KEY,
    name TEXT,
    Role TEXT,
    userName TEXT,
    passwordHash TEXT,
    salary REAL
);

CREATE TABLE IF NOT EXISTS INVOICEQUOTE(
    invoiceQuoteID INTEGER NOT NULL PRIMARY KEY,
    date TEXT,
    type INTEGER,
    totalAmount INTEGER,
    customerID INTEGER,
    staffID INTEGER,
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
    quantity INTEGER,
    priceAtSale REAL,
    itemID INTEGER,
    invoiceQuoteID INTEGER,
    FOREIGN KEY(invoiceQuoteID) REFERENCES INVOICEQUOTE(invoiceQuoteID),
    FOREIGN KEY(itemID) REFERENCES ITEM(itemID)
);

-- === Test Data for ADIX.db ===

-- Add some sellers
INSERT INTO SELLER (name, contactInfo, bankDetails, commissionRate) VALUES
('John Doe', 'john@example.com', '12345678', 0.05),
('Jane Smith', 'jane@example.com', '87654321', 0.07);

-- Add some suppliers
INSERT INTO SUPPLIER (name, contactInfo, address) VALUES
('GreenFoods Ltd', 'greenfoods@example.com', '123 Green St'),
('BeverageCorp', 'info@beveragecorp.com', '456 Juice Ave');

-- Add some items
INSERT INTO ITEM (description, retailPrice, costPrice, stockQuantity, stockSold, supplierID, sellerID) VALUES
('Apples (1kg)', 25.50, 15.00, 50, 0, 1, 1),
('Orange Juice (1L)', 35.00, 20.00, 30, 0, 2, 2),
('Chips (Large)', 15.00, 8.00, 80, 0, 2, 1),
('Bananas (1kg)', 20.00, 12.00, 40, 0, 1, 2),
('Cola (330ml)', 12.50, 7.00, 100, 0, 2, 1);

-- Add some staff
INSERT INTO STAFF (name, Role, userName, passwordHash, salary) VALUES
('Ruben Janse', 'Admin', 'ruben', 'hashedpassword1', 15000),
('Sarah Ndlovu', 'Cashier', 'sarah', 'hashedpassword2', 9000),
('Michael Smith', 'Manager', 'michael', 'hashedpassword3', 12000);

-- Add some customers
INSERT INTO CUSTOMER (name, phone, email, credit) VALUES
('Alice Brown', '0712345678', 'alice@example.com', 100),
('Bob White', '0723456789', 'bob@example.com', 50);

-- Add an invoice quote (sale)
INSERT INTO INVOICEQUOTE (date, type, totalAmount, customerID, staffID) VALUES
('2025-10-09', 1, 72.50, 1, 2); -- type=1 could mean "sale"

-- Add invoice items for that invoice
INSERT INTO INVOICEITEM (quantity, priceAtSale, itemID, invoiceQuoteID) VALUES
(1, 25.50, 1, 1),
(1, 15.00, 3, 1),
(2, 16.00, 5, 1);


