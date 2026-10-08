IF OBJECT_ID(N'dbo.WhoIsAdmin', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WhoIsAdmin
    (
        PersenolId NVARCHAR(50) NOT NULL CONSTRAINT PK_WhoIsAdmin PRIMARY KEY,
        Eposta     NVARCHAR(320) NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.UserTable', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserTable
    (
        UserId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserTable PRIMARY KEY,
        [Password] NVARCHAR(255) NOT NULL,
        AktifPasif BIT NOT NULL CONSTRAINT DF_UserTable_AktifPasif DEFAULT (1),
        Ad         NVARCHAR(100) NOT NULL,
        SoyAd      NVARCHAR(100) NOT NULL,
        Mail       NVARCHAR(320) NOT NULL
    );
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.UserTable')
      AND name = N'UX_UserTable_Mail'
)
BEGIN
    CREATE UNIQUE INDEX UX_UserTable_Mail ON dbo.UserTable(Mail);
END;

IF OBJECT_ID(N'dbo.Tables', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Tables]
    (
        TableName  NVARCHAR(128) NOT NULL CONSTRAINT PK_Tables PRIMARY KEY,
        AktifPasif BIT NOT NULL CONSTRAINT DF_Tables_AktifPasif DEFAULT (1)
    );
END;

IF OBJECT_ID(N'dbo.KullaniciYetkiTable', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KullaniciYetkiTable
    (
        UserId    INT NOT NULL,
        TableName NVARCHAR(128) NOT NULL,
        Okuma     BIT NOT NULL CONSTRAINT DF_KullaniciYetkiTable_Okuma DEFAULT (0),
        Yazma     BIT NOT NULL CONSTRAINT DF_KullaniciYetkiTable_Yazma DEFAULT (0),
        Silme     BIT NOT NULL CONSTRAINT DF_KullaniciYetkiTable_Silme DEFAULT (0),
        Onizleme  BIT NOT NULL CONSTRAINT DF_KullaniciYetkiTable_Onizleme DEFAULT (0),
        CONSTRAINT PK_KullaniciYetkiTable PRIMARY KEY (UserId, TableName),
        CONSTRAINT FK_KullaniciYetkiTable_UserTable FOREIGN KEY (UserId)
            REFERENCES dbo.UserTable(UserId) ON DELETE CASCADE,
        CONSTRAINT FK_KullaniciYetkiTable_Tables FOREIGN KEY (TableName)
            REFERENCES dbo.[Tables](TableName)
    );
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_KullaniciYetkiTable_UserTable'
      AND parent_object_id = OBJECT_ID(N'dbo.KullaniciYetkiTable')
)
BEGIN
    ALTER TABLE dbo.KullaniciYetkiTable WITH CHECK
    ADD CONSTRAINT FK_KullaniciYetkiTable_UserTable FOREIGN KEY (UserId)
        REFERENCES dbo.UserTable(UserId) ON DELETE CASCADE;
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_KullaniciYetkiTable_Tables'
      AND parent_object_id = OBJECT_ID(N'dbo.KullaniciYetkiTable')
)
BEGIN
    ALTER TABLE dbo.KullaniciYetkiTable WITH CHECK
    ADD CONSTRAINT FK_KullaniciYetkiTable_Tables FOREIGN KEY (TableName)
        REFERENCES dbo.[Tables](TableName);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.[Tables] WHERE TableName = N'STOK')
    INSERT INTO dbo.[Tables] (TableName) VALUES (N'STOK');
IF NOT EXISTS (SELECT 1 FROM dbo.[Tables] WHERE TableName = N'AMBALAJ REÇETESİ')
    INSERT INTO dbo.[Tables] (TableName) VALUES (N'AMBALAJ REÇETESİ');
IF NOT EXISTS (SELECT 1 FROM dbo.[Tables] WHERE TableName = N'DEPO TRANSFERİ')
    INSERT INTO dbo.[Tables] (TableName) VALUES (N'DEPO TRANSFERİ');
IF NOT EXISTS (SELECT 1 FROM dbo.[Tables] WHERE TableName = N'STOK HAREKETLERİ')
    INSERT INTO dbo.[Tables] (TableName) VALUES (N'STOK HAREKETLERİ');
IF NOT EXISTS (SELECT 1 FROM dbo.[Tables] WHERE TableName = N'STOK RAPORU')
    INSERT INTO dbo.[Tables] (TableName) VALUES (N'STOK RAPORU');
IF NOT EXISTS (SELECT 1 FROM dbo.[Tables] WHERE TableName = N'ÜRETİM KAYDI')
    INSERT INTO dbo.[Tables] (TableName) VALUES (N'ÜRETİM KAYDI');

IF OBJECT_ID(N'dbo.PackagingRecipes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PackagingRecipes
    (
        PackagingRecipeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PackagingRecipes PRIMARY KEY,
        RecipeName NVARCHAR(100) NOT NULL,
        ProductType NVARCHAR(30) NOT NULL,
        Unit NVARCHAR(20) NOT NULL,
        QuantityPerPackage DECIMAL(18,3) NOT NULL,
        PackagesPerPallet INT NOT NULL,
        CONSTRAINT UQ_PackagingRecipes_Recipe UNIQUE (RecipeName, ProductType, Unit),
        CONSTRAINT CK_PackagingRecipes_ProductType CHECK (ProductType IN (N'Hammadde', N'Mamul')),
        CONSTRAINT CK_PackagingRecipes_Unit CHECK (Unit IN (N'Adet', N'Kg', N'Metre', N'Litre')),
        CONSTRAINT CK_PackagingRecipes_Quantity CHECK (QuantityPerPackage > 0),
        CONSTRAINT CK_PackagingRecipes_Pallet CHECK (PackagesPerPallet > 0)
    );
END;

IF OBJECT_ID(N'dbo.StockItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockItems
    (
        StockItemId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockItems PRIMARY KEY,
        ProductName NVARCHAR(150) NOT NULL,
        ProductType NVARCHAR(30) NOT NULL,
        Quantity DECIMAL(18,3) NOT NULL,
        MinimumQuantity DECIMAL(18,3) NOT NULL CONSTRAINT DF_StockItems_MinimumQuantity DEFAULT (0),
        Unit NVARCHAR(20) NOT NULL,
        Warehouse NVARCHAR(30) NOT NULL,
        PackagingRecipeId INT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_StockItems_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_StockItems_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_StockItems_MinimumQuantity CHECK (MinimumQuantity >= 0),
        CONSTRAINT CK_StockItems_ProductType CHECK (ProductType IN (N'Hammadde', N'Mamul')),
        CONSTRAINT CK_StockItems_Unit CHECK (Unit IN (N'Adet', N'Kg', N'Metre', N'Litre')),
        CONSTRAINT CK_StockItems_Warehouse CHECK (Warehouse IN (N'Depo 1', N'Depo 2', N'Depo 3'))
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_StockItems_PackagingRecipes')
BEGIN
    ALTER TABLE dbo.StockItems WITH CHECK
    ADD CONSTRAINT FK_StockItems_PackagingRecipes FOREIGN KEY (PackagingRecipeId)
        REFERENCES dbo.PackagingRecipes(PackagingRecipeId);
END;

IF OBJECT_ID(N'dbo.StockTransfers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockTransfers
    (
        StockTransferId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockTransfers PRIMARY KEY,
        ProductName NVARCHAR(150) NOT NULL,
        ProductType NVARCHAR(30) NOT NULL,
        Quantity DECIMAL(18,3) NOT NULL,
        Unit NVARCHAR(20) NOT NULL,
        FromWarehouse NVARCHAR(30) NOT NULL,
        ToWarehouse NVARCHAR(30) NOT NULL,
        PackagingRecipeId INT NULL,
        TransferredByUserId INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_StockTransfers_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_StockTransfers_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_StockTransfers_Warehouses CHECK (FromWarehouse <> ToWarehouse),
        CONSTRAINT CK_StockTransfers_FromWarehouse CHECK (FromWarehouse IN (N'Depo 1', N'Depo 2', N'Depo 3')),
        CONSTRAINT CK_StockTransfers_ToWarehouse CHECK (ToWarehouse IN (N'Depo 1', N'Depo 2', N'Depo 3')),
        CONSTRAINT FK_StockTransfers_PackagingRecipes FOREIGN KEY (PackagingRecipeId)
            REFERENCES dbo.PackagingRecipes(PackagingRecipeId),
        CONSTRAINT FK_StockTransfers_UserTable FOREIGN KEY (TransferredByUserId)
            REFERENCES dbo.UserTable(UserId)
    );
END;

IF OBJECT_ID(N'dbo.StockMovements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockMovements
    (
        StockMovementId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockMovements PRIMARY KEY,
        StockItemId INT NULL,
        ProductName NVARCHAR(150) NOT NULL,
        ProductType NVARCHAR(30) NOT NULL,
        MovementType NVARCHAR(10) NOT NULL,
        Quantity DECIMAL(18,3) NOT NULL,
        Unit NVARCHAR(20) NOT NULL,
        Warehouse NVARCHAR(30) NOT NULL,
        Reason NVARCHAR(50) NOT NULL,
        Note NVARCHAR(300) NULL,
        PerformedByUserId INT NOT NULL,
        RelatedTransferId INT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_StockMovements_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_StockMovements_Type CHECK (MovementType IN (N'Giriş', N'Çıkış')),
        CONSTRAINT CK_StockMovements_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_StockMovements_Warehouse CHECK (Warehouse IN (N'Depo 1', N'Depo 2', N'Depo 3')),
        CONSTRAINT FK_StockMovements_UserTable FOREIGN KEY (PerformedByUserId)
            REFERENCES dbo.UserTable(UserId),
        CONSTRAINT FK_StockMovements_StockTransfers FOREIGN KEY (RelatedTransferId)
            REFERENCES dbo.StockTransfers(StockTransferId)
    );
END;

IF OBJECT_ID(N'dbo.ProductionRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductionRecords
    (
        ProductionRecordId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductionRecords PRIMARY KEY,
        ProductName NVARCHAR(150) NOT NULL,
        Quantity DECIMAL(18,3) NOT NULL,
        Unit NVARCHAR(20) NOT NULL,
        Warehouse NVARCHAR(30) NOT NULL,
        PackagingRecipeId INT NOT NULL,
        ProducedByUserId INT NOT NULL,
        ProducedAt DATETIME2 NOT NULL,
        Note NVARCHAR(300) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ProductionRecords_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_ProductionRecords_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_ProductionRecords_Warehouse CHECK (Warehouse IN (N'Depo 1', N'Depo 2', N'Depo 3')),
        CONSTRAINT FK_ProductionRecords_PackagingRecipes FOREIGN KEY (PackagingRecipeId)
            REFERENCES dbo.PackagingRecipes(PackagingRecipeId),
        CONSTRAINT FK_ProductionRecords_UserTable FOREIGN KEY (ProducedByUserId)
            REFERENCES dbo.UserTable(UserId)
    );
END;

IF OBJECT_ID(N'dbo.CompanyProfile', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanyProfile
    (
        ProfileId TINYINT NOT NULL CONSTRAINT PK_CompanyProfile PRIMARY KEY,
        CompanyName NVARCHAR(200) NOT NULL,
        TaxOffice NVARCHAR(100) NULL,
        TaxNumber NVARCHAR(30) NULL,
        Phone NVARCHAR(30) NULL,
        Email NVARCHAR(320) NULL,
        CompanyAddress NVARCHAR(500) NULL,
        FactoryName NVARCHAR(200) NULL,
        FactoryAddress NVARCHAR(500) NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_CompanyProfile_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL,
        CONSTRAINT CK_CompanyProfile_SingleRow CHECK (ProfileId = 1),
        CONSTRAINT FK_CompanyProfile_UserTable FOREIGN KEY (UpdatedByUserId)
            REFERENCES dbo.UserTable(UserId)
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.[Tables] WHERE TableName = N'MÜŞTERİLER')
    INSERT INTO dbo.[Tables] (TableName) VALUES (N'MÜŞTERİLER');
IF NOT EXISTS (SELECT 1 FROM dbo.[Tables] WHERE TableName = N'SATIŞ')
    INSERT INTO dbo.[Tables] (TableName) VALUES (N'SATIŞ');
IF NOT EXISTS (SELECT 1 FROM dbo.[Tables] WHERE TableName = N'SATIŞ RAPORU')
    INSERT INTO dbo.[Tables] (TableName) VALUES (N'SATIŞ RAPORU');

IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers
    (
        CustomerId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
        CustomerCode NVARCHAR(30) NOT NULL CONSTRAINT UQ_Customers_CustomerCode UNIQUE,
        CustomerType NVARCHAR(20) NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        TaxOffice NVARCHAR(100) NULL,
        TaxNumber NVARCHAR(30) NULL,
        ContactName NVARCHAR(150) NULL,
        Phone NVARCHAR(30) NULL,
        Email NVARCHAR(320) NULL,
        BillingAddress NVARCHAR(500) NULL,
        DeliveryAddress NVARCHAR(500) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Customers_IsActive DEFAULT (1),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_Customers_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId INT NOT NULL,
        UpdatedByUserId INT NOT NULL,
        CONSTRAINT CK_Customers_CustomerType CHECK (CustomerType IN (N'Şirket', N'Şahıs')),
        CONSTRAINT FK_Customers_CreatedByUser FOREIGN KEY (CreatedByUserId) REFERENCES dbo.UserTable(UserId),
        CONSTRAINT FK_Customers_UpdatedByUser FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.UserTable(UserId)
    );
END;

IF OBJECT_ID(N'dbo.Sales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sales
    (
        SaleId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sales PRIMARY KEY,
        CustomerId INT NOT NULL,
        ProductName NVARCHAR(150) NOT NULL,
        ProductType NVARCHAR(30) NOT NULL,
        Quantity DECIMAL(18,3) NOT NULL,
        Unit NVARCHAR(20) NOT NULL,
        Warehouse NVARCHAR(30) NOT NULL,
        UnitPrice DECIMAL(18,4) NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL,
        ShipmentType NVARCHAR(50) NOT NULL,
        SaleDate DATETIME2 NOT NULL,
        Note NVARCHAR(300) NULL,
        CreatedByUserId INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Sales_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_Sales_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_Sales_UnitPrice CHECK (UnitPrice > 0),
        CONSTRAINT CK_Sales_TotalAmount CHECK (TotalAmount > 0),
        CONSTRAINT CK_Sales_Unit CHECK (Unit IN (N'Adet', N'Kg', N'Metre', N'Litre')),
        CONSTRAINT CK_Sales_Warehouse CHECK (Warehouse IN (N'Depo 1', N'Depo 2', N'Depo 3')),
        CONSTRAINT CK_Sales_ShipmentType CHECK (ShipmentType IN (N'Karayolu (Tır/Kamyon)', N'Demiryolu (Tren)', N'Denizyolu', N'Havayolu', N'Müşteri teslim alacak', N'Diğer')),
        CONSTRAINT FK_Sales_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId),
        CONSTRAINT FK_Sales_UserTable FOREIGN KEY (CreatedByUserId) REFERENCES dbo.UserTable(UserId)
    );
END;
