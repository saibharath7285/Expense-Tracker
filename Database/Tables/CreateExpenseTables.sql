CREATE TABLE dbo.ExpenseCategories (
	Id INT IDENTITY(1,1) PRIMARY KEY,
	Name NVARCHAR(150) NOT NULL,
)

CREATE TABLE dbo.ExpenseSubCategories (
	Id INT IDENTITY(1,1) PRIMARY KEY,
	CategoryId INT NOT NULL FOREIGN KEY REFERENCES dbo.ExpenseCategories(Id),
	Name NVARCHAR(150) NOT NULL,
	CreateDateTime DATETIME2 DEFAULT SYSUTCDATETIME(),

	CONSTRAINT UQ_ExpenseSubCategories_Category_Name UNIQUE (CategoryId, Name)
)

CREATE TABLE dbo.Expenses (
	Id INT IDENTITY(1,1) PRIMARY KEY,
	Title NVARCHAR(150) NOT NULL,
	CategoryId INT NOT NULL FOREIGN KEY REFERENCES dbo.ExpenseCategories(Id),
    SubCategoryId INT NOT NULL FOREIGN KEY REFERENCES dbo.ExpenseSubCategories(Id),
	Amount DECIMAL(18, 2) NOT NULL,
	ExpenseDate DATETIME2 NOT NULL,
	Notes NVARCHAR(500) NULL,
	CreateDateTime DATETIME2 DEFAULT SYSUTCDATETIME()
)

INSERT INTO dbo.ExpenseCategories (Name) VALUES 
('Food & Dining'),
('Travel & Transportation'),
('Utilities & Bills'),
('Office Supplies');
GO

INSERT INTO dbo.ExpenseSubCategories (CategoryId, Name) VALUES 
(1, 'Pasta'),
(1, 'Pizza'),
(1, 'Chicken'),
(1, 'Dal');
GO

INSERT INTO dbo.ExpenseSubCategories (CategoryId, Name) VALUES 
(2, 'Flight'),
(2, 'Train'),
(2, 'Taxi / Cab'),
(2, 'Ship');
GO

INSERT INTO dbo.ExpenseSubCategories (CategoryId, Name) VALUES 
(3, 'Electricity'),
(3, 'Water'),
(3, 'Internet'),
(3, 'Mobile Phone');
GO

INSERT INTO dbo.ExpenseSubCategories (CategoryId, Name) VALUES 
(4, 'Stationery'),
(4, 'Software Subscription'),
(4, 'Hardware / Peripherals');
GO