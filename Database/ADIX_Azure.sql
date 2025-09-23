-- SELLER table
CREATE TABLE SELLER(
    sellerID INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(255),
    contactInfo NVARCHAR(255),
    bankDetails NVARCHAR(255),
    commissionRate DECIMAL(18,2)
);

-- SUPPLIER table
CREATE TABLE SUPPLIER(
    supplierID INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(255),
    contactInfo NVARCHAR(255),
    address NVARCHAR(255)
);

-- ITEM table
CREATE TABLE ITEM(
    itemID INT IDENTITY(1,1) PRIMARY KEY,
    description NVARCHAR(255),
    retailPrice DECIMAL(18,2),
    costPrice DECIMAL(18,2),
    stockQuantity INT,
    stockSold INT,
    supplierID INT,
    sellerID INT,
    FOREIGN KEY(supplierID) REFERENCES SUPPLIER(supplierID),
    FOREIGN KEY(sellerID) REFERENCES SELLER(sellerID)
);

-- CUSTOMER table
CREATE TABLE CUSTOMER(
    customerID INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(255),
    phone NVARCHAR(50),
    email NVARCHAR(255),
    credit DECIMAL(18,2)
);

-- STAFF table
CREATE TABLE STAFF(
    staffID INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(255),
    role NVARCHAR(100),
    userName NVARCHAR(100),
    passwordHash NVARCHAR(255),
    salary DECIMAL(18,2)
);

-- INVOICEQUOTE table
CREATE TABLE INVOICEQUOTE(
    invoiceQuoteID INT IDENTITY(1,1) PRIMARY KEY,
    date DATETIME2,
    type INT,
    totalAmount DECIMAL(18,2),
    customerID INT,
    staffID INT,
    FOREIGN KEY(customerID) REFERENCES CUSTOMER(customerID),
    FOREIGN KEY(staffID) REFERENCES STAFF(staffID)
);

-- REPORT table
CREATE TABLE REPORT(
    reportID INT IDENTITY(1,1) PRIMARY KEY,
    reportType INT,
    date DATETIME2,
    staffID INT,
    FOREIGN KEY(staffID) REFERENCES STAFF(staffID)
);

-- INVOICEITEM table
CREATE TABLE INVOICEITEM(
    invoiceItemID INT IDENTITY(1,1) PRIMARY KEY,
    quantity INT,
    priceAtSale DECIMAL(18,2),
    itemID INT,
    invoiceQuoteID INT,
    FOREIGN KEY(invoiceQuoteID) REFERENCES INVOICEQUOTE(invoiceQuoteID),
    FOREIGN KEY(itemID) REFERENCES ITEM(itemID)
);
