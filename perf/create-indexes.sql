IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Vehicles_Brand_Status_Price')
    CREATE INDEX IX_Vehicles_Brand_Status_Price ON Vehicles (Brand, AvailabilityStatus, Price) INCLUDE (ManufacturingYear);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Vehicles_Status_Price')
    CREATE INDEX IX_Vehicles_Status_Price ON Vehicles (AvailabilityStatus, Price);
