
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

INSERT INTO ITEM (itemID, description, retailPrice, costPrice, stockQuantity, stockSold, supplierID, sellerID)
VALUES (1, 'Bow', 3000, 15.00, 50, 0, 1, 1);

