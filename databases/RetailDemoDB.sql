-- RetailDemoDB: sample SQL Server database for testing the NL-to-SQL engine.
-- Run: sqlcmd -S KRITIKKANDWAL-1 -E -C -i databases\RetailDemoDB.sql
IF DB_ID('RetailDemoDB') IS NOT NULL
BEGIN
    ALTER DATABASE RetailDemoDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE RetailDemoDB;
END
GO
CREATE DATABASE RetailDemoDB;
GO
USE RetailDemoDB;
GO

CREATE TABLE Departments (
    DepartmentId INT IDENTITY(1,1) PRIMARY KEY,
    DepartmentName NVARCHAR(50) NOT NULL,
    Location NVARCHAR(50) NOT NULL,
    Budget DECIMAL(12,2) NOT NULL
);

CREATE TABLE Employees (
    EmployeeId INT IDENTITY(1,1) PRIMARY KEY,
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    Email NVARCHAR(100) NOT NULL,
    DepartmentId INT NOT NULL REFERENCES Departments(DepartmentId),
    JobTitle NVARCHAR(60) NOT NULL,
    Salary DECIMAL(10,2) NOT NULL,
    HireDate DATE NOT NULL
);

CREATE TABLE Customers (
    CustomerId INT IDENTITY(1,1) PRIMARY KEY,
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    Email NVARCHAR(100) NOT NULL,
    City NVARCHAR(50) NOT NULL,
    Country NVARCHAR(50) NOT NULL,
    JoinDate DATE NOT NULL
);

CREATE TABLE Categories (
    CategoryId INT IDENTITY(1,1) PRIMARY KEY,
    CategoryName NVARCHAR(50) NOT NULL,
    Description NVARCHAR(200) NULL
);

CREATE TABLE Suppliers (
    SupplierId INT IDENTITY(1,1) PRIMARY KEY,
    SupplierName NVARCHAR(80) NOT NULL,
    Country NVARCHAR(50) NOT NULL,
    ContactEmail NVARCHAR(100) NOT NULL
);

CREATE TABLE Products (
    ProductId INT IDENTITY(1,1) PRIMARY KEY,
    ProductName NVARCHAR(100) NOT NULL,
    CategoryId INT NOT NULL REFERENCES Categories(CategoryId),
    SupplierId INT NOT NULL REFERENCES Suppliers(SupplierId),
    Price DECIMAL(10,2) NOT NULL,
    StockQuantity INT NOT NULL
);

CREATE TABLE Orders (
    OrderId INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId INT NOT NULL REFERENCES Customers(CustomerId),
    EmployeeId INT NOT NULL REFERENCES Employees(EmployeeId),
    OrderDate DATE NOT NULL,
    Status NVARCHAR(20) NOT NULL,
    TotalAmount DECIMAL(12,2) NOT NULL
);

CREATE TABLE OrderItems (
    OrderItemId INT IDENTITY(1,1) PRIMARY KEY,
    OrderId INT NOT NULL REFERENCES Orders(OrderId),
    ProductId INT NOT NULL REFERENCES Products(ProductId),
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(10,2) NOT NULL
);
GO

INSERT INTO Departments (DepartmentName, Location, Budget) VALUES
('Sales','New York',500000),('Marketing','Chicago',250000),('Engineering','San Francisco',900000),
('Human Resources','Boston',150000),('Support','Austin',200000);

INSERT INTO Employees (FirstName, LastName, Email, DepartmentId, JobTitle, Salary, HireDate) VALUES
('Olivia','Martin','olivia.martin@retail.com',1,'Sales Manager',85000,'2019-03-15'),
('Liam','Johnson','liam.johnson@retail.com',1,'Sales Executive',55000,'2020-07-01'),
('Emma','Williams','emma.williams@retail.com',1,'Sales Executive',57000,'2021-01-10'),
('Noah','Brown','noah.brown@retail.com',2,'Marketing Lead',72000,'2018-11-20'),
('Ava','Davis','ava.davis@retail.com',2,'Content Strategist',52000,'2022-04-05'),
('William','Miller','william.miller@retail.com',3,'Software Engineer',98000,'2017-09-12'),
('Sophia','Wilson','sophia.wilson@retail.com',3,'Senior Engineer',120000,'2016-02-28'),
('James','Moore','james.moore@retail.com',3,'DevOps Engineer',105000,'2019-08-19'),
('Isabella','Taylor','isabella.taylor@retail.com',4,'HR Manager',78000,'2018-05-30'),
('Benjamin','Anderson','benjamin.anderson@retail.com',5,'Support Agent',42000,'2023-01-16'),
('Mia','Thomas','mia.thomas@retail.com',5,'Support Lead',60000,'2020-10-12'),
('Lucas','Jackson','lucas.jackson@retail.com',1,'Account Executive',62000,'2022-09-01');

INSERT INTO Customers (FirstName, LastName, Email, City, Country, JoinDate) VALUES
('Rahul','Sharma','rahul.sharma@mail.com','Mumbai','India','2022-01-12'),
('Priya','Patel','priya.patel@mail.com','Ahmedabad','India','2022-03-08'),
('John','Smith','john.smith@mail.com','New York','USA','2021-11-23'),
('Emily','Clark','emily.clark@mail.com','Los Angeles','USA','2022-06-17'),
('Hans','Mueller','hans.mueller@mail.com','Berlin','Germany','2021-08-02'),
('Sophie','Dubois','sophie.dubois@mail.com','Paris','France','2023-02-14'),
('Carlos','Garcia','carlos.garcia@mail.com','Madrid','Spain','2022-10-30'),
('Yuki','Tanaka','yuki.tanaka@mail.com','Tokyo','Japan','2023-05-09'),
('Oliver','Brown','oliver.brown@mail.com','London','UK','2021-12-01'),
('Charlotte','Evans','charlotte.evans@mail.com','Manchester','UK','2023-07-21'),
('Amit','Verma','amit.verma@mail.com','Delhi','India','2023-09-05'),
('Michael','Lee','michael.lee@mail.com','Chicago','USA','2022-12-19'),
('Anna','Kowalski','anna.kowalski@mail.com','Warsaw','Poland','2024-01-10'),
('David','Kim','david.kim@mail.com','Seoul','South Korea','2024-02-22');

INSERT INTO Categories (CategoryName, Description) VALUES
('Electronics','Phones, laptops and accessories'),('Clothing','Men and women apparel'),
('Home & Kitchen','Appliances and home essentials'),('Books','Fiction, non-fiction and education'),
('Sports','Sports and fitness equipment');

INSERT INTO Suppliers (SupplierName, Country, ContactEmail) VALUES
('TechWorld Inc','USA','sales@techworld.com'),('Fashion Hub Ltd','India','contact@fashionhub.in'),
('HomeEase GmbH','Germany','info@homeease.de'),('BookNest Publishers','UK','orders@booknest.co.uk'),
('ActiveGear Co','Japan','hello@activegear.jp');

INSERT INTO Products (ProductName, CategoryId, SupplierId, Price, StockQuantity) VALUES
('Smartphone X12',1,1,699.99,120),('Laptop Pro 15',1,1,1299.00,60),('Wireless Earbuds',1,1,89.50,300),
('Cotton T-Shirt',2,2,19.99,500),('Denim Jeans',2,2,49.90,250),('Winter Jacket',2,2,129.00,80),
('Air Fryer',3,3,99.00,90),('Coffee Maker',3,3,59.99,140),('Vacuum Cleaner',3,3,189.00,45),
('Mystery Novel',4,4,12.99,400),('Python Programming Guide',4,4,39.95,200),('World History Atlas',4,4,29.50,150),
('Yoga Mat',5,5,24.99,320),('Running Shoes',5,5,84.00,210),('Dumbbell Set',5,5,149.00,70);

INSERT INTO Orders (CustomerId, EmployeeId, OrderDate, Status, TotalAmount) VALUES
(1,2,'2024-01-05','Delivered',789.49),(2,3,'2024-01-18','Delivered',69.89),
(3,2,'2024-02-02','Delivered',1299.00),(4,12,'2024-02-14','Shipped',253.49),
(5,3,'2024-03-09','Delivered',198.00),(6,2,'2024-03-21','Cancelled',129.00),
(7,12,'2024-04-11','Delivered',92.94),(8,3,'2024-04-25','Delivered',233.00),
(9,2,'2024-05-06','Pending',52.94),(10,12,'2024-05-19','Shipped',738.49),
(11,3,'2024-06-03','Delivered',189.00),(12,2,'2024-06-27','Delivered',173.99),
(1,12,'2024-07-15','Delivered',1388.50),(3,3,'2024-08-08','Pending',113.99),
(5,2,'2024-09-12','Shipped',299.00);

INSERT INTO OrderItems (OrderId, ProductId, Quantity, UnitPrice) VALUES
(1,1,1,699.99),(1,3,1,89.50),
(2,4,2,19.99),(2,10,1,12.99),(2,13,1,24.99)  ,
(3,2,1,1299.00),
(4,14,1,84.00),(4,15,1,149.00),(4,13,1,24.99)  ,
(5,7,2,99.00),
(6,6,1,129.00),
(7,4,1,19.99),(7,8,1,59.99),(7,10,1,12.99),
(8,9,1,189.00),(8,12,1,29.50)  ,
(9,4,1,19.99),(9,11,1,39.95)  ,
(10,1,1,699.99),(10,11,1,39.95)  ,
(11,9,1,189.00),
(12,14,1,84.00),(12,3,1,89.50) ,
(13,2,1,1299.00),(13,3,1,89.50),
(14,5,1,49.90),(14,8,1,59.99)  ,
(15,15,2,149.00);
-- Note: a few OrderItems rows intentionally approximate TotalAmount; this is demo data.
GO
