-- ===========================================
-- ДОПОЛНИТЕЛЬНЫЕ ТАБЛИЦЫ ДЛЯ АВТОСАЛОНА
-- ===========================================

-- 1. Поставщики (Suppliers) - откуда привозят автомобили
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Suppliers]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Suppliers] (
        [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(255) NOT NULL,
        [ContactPerson] NVARCHAR(255) NULL,
        [Phone] NVARCHAR(32) NULL,
        [Email] NVARCHAR(255) NULL,
        [Address] NVARCHAR(500) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETDATE()
    );
    
    CREATE INDEX IX_Suppliers_Name ON [dbo].[Suppliers]([Name]);
END
GO

-- 2. Поставки (Deliveries) - когда и какие автомобили поступили
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Deliveries]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Deliveries] (
        [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [VehicleId] BIGINT NOT NULL,
        [SupplierId] BIGINT NULL,
        [DeliveryDate] DATETIME2(7) NOT NULL,
        [PurchasePrice] DECIMAL(10,2) NULL,
        [InvoiceNumber] NVARCHAR(64) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETDATE(),
        
        CONSTRAINT FK_Deliveries_Vehicles_VehicleId 
            FOREIGN KEY ([VehicleId]) REFERENCES [dbo].[Vehicles]([Id]) ON DELETE CASCADE,
        CONSTRAINT FK_Deliveries_Suppliers_SupplierId 
            FOREIGN KEY ([SupplierId]) REFERENCES [dbo].[Suppliers]([Id]) ON DELETE SET NULL
    );
    
    CREATE INDEX IX_Deliveries_VehicleId ON [dbo].[Deliveries]([VehicleId]);
    CREATE INDEX IX_Deliveries_SupplierId ON [dbo].[Deliveries]([SupplierId]);
    CREATE INDEX IX_Deliveries_DeliveryDate ON [dbo].[Deliveries]([DeliveryDate]);
END
GO

-- 3. Резервирования (Reservations) - детальная информация о резервировании
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Reservations]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Reservations] (
        [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [VehicleId] BIGINT NOT NULL,
        [ClientId] BIGINT NOT NULL,
        [ReservedBy] BIGINT NULL, -- Кто оформил резерв (сотрудник)
        [ReservationDate] DATETIME2(7) NOT NULL DEFAULT GETDATE(),
        [ExpiryDate] DATETIME2(7) NULL, -- До какой даты резерв действует
        [Deposit] DECIMAL(10,2) NULL, -- Залог
        [Status] NVARCHAR(32) NOT NULL DEFAULT N'Активен', -- Активен, Отменен, Истек
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETDATE(),
        
        CONSTRAINT FK_Reservations_Vehicles_VehicleId 
            FOREIGN KEY ([VehicleId]) REFERENCES [dbo].[Vehicles]([Id]) ON DELETE CASCADE,
        CONSTRAINT FK_Reservations_Clients_ClientId 
            FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients]([Id]) ON DELETE CASCADE,
        CONSTRAINT FK_Reservations_Users_ReservedBy 
            FOREIGN KEY ([ReservedBy]) REFERENCES [dbo].[Users]([Id]) ON DELETE SET NULL
    );
    
    CREATE INDEX IX_Reservations_VehicleId ON [dbo].[Reservations]([VehicleId]);
    CREATE INDEX IX_Reservations_ClientId ON [dbo].[Reservations]([ClientId]);
    CREATE INDEX IX_Reservations_Status ON [dbo].[Reservations]([Status]);
    CREATE INDEX IX_Reservations_ExpiryDate ON [dbo].[Reservations]([ExpiryDate]);
END
GO

-- 4. Фото автомобилей (VehiclePhotos) - для каталога
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[VehiclePhotos]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[VehiclePhotos] (
        [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [VehicleId] BIGINT NOT NULL,
        [PhotoPath] NVARCHAR(500) NOT NULL, -- Путь к файлу или URL
        [IsMain] BIT NOT NULL DEFAULT 0, -- Главное фото
        [DisplayOrder] INT NULL DEFAULT 0, -- Порядок отображения
        [Description] NVARCHAR(255) NULL,
        [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETDATE(),
        
        CONSTRAINT FK_VehiclePhotos_Vehicles_VehicleId 
            FOREIGN KEY ([VehicleId]) REFERENCES [dbo].[Vehicles]([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX IX_VehiclePhotos_VehicleId ON [dbo].[VehiclePhotos]([VehicleId]);
    CREATE INDEX IX_VehiclePhotos_IsMain ON [dbo].[VehiclePhotos]([VehicleId], [IsMain]);
END
GO

-- 5. Справочник марок (Brands) - для унификации
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Brands]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Brands] (
        [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(64) NOT NULL UNIQUE,
        [Country] NVARCHAR(64) NULL,
        [LogoPath] NVARCHAR(500) NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETDATE()
    );
    
    CREATE INDEX IX_Brands_Name ON [dbo].[Brands]([Name]);
END
GO

-- 6. Справочник моделей (Models) - для унификации
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Models]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Models] (
        [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [BrandId] BIGINT NOT NULL,
        [Name] NVARCHAR(64) NOT NULL,
        [BodyType] NVARCHAR(32) NULL, -- Седан, Хэтчбек, Внедорожник и т.д.
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETDATE(),
        
        CONSTRAINT FK_Models_Brands_BrandId 
            FOREIGN KEY ([BrandId]) REFERENCES [dbo].[Brands]([Id]) ON DELETE CASCADE,
        CONSTRAINT UQ_Models_BrandId_Name UNIQUE ([BrandId], [Name])
    );
    
    CREATE INDEX IX_Models_BrandId ON [dbo].[Models]([BrandId]);
    CREATE INDEX IX_Models_Name ON [dbo].[Models]([Name]);
END
GO

PRINT 'Дополнительные таблицы созданы успешно!';
PRINT 'Всего таблиц теперь: 11 (Clients, Users, Vehicles, Sales, Suppliers, Deliveries, Reservations, VehiclePhotos, Brands, Models)';


