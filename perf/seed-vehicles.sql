SET NOCOUNT ON;

DECLARE @count int = $(VehicleCount);

WITH numbers AS (
    SELECT TOP (@count) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT INTO Vehicles (Brand, Model, ManufacturingYear, Price, Color, Mileage, AvailabilityStatus, CreatedAt)
SELECT
    CASE i % 3 WHEN 0 THEN 'Chery' WHEN 1 THEN 'OMODA' ELSE 'JAECOO' END,
    CASE i % 3
        WHEN 0 THEN CHOOSE((i / 3) % 5 + 1, 'Tiggo 4 Pro', 'Tiggo 7 Pro', 'Tiggo 8 Pro', 'Tiggo 9', 'Arrizo 8')
        WHEN 1 THEN CHOOSE((i / 3) % 3 + 1, 'C5', 'E5', 'C7')
        ELSE CHOOSE((i / 3) % 3 + 1, 'J5', 'J7', 'J8')
    END + ' #' + CAST(i AS varchar(10)),
    2018 + (i / 7) % 9,
    CAST(60000 + (i * 7919) % 160000 AS decimal(18, 2)),
    CHOOSE(i % 6 + 1, 'White', 'Black', 'Grey', 'Red', 'Blue', 'Green'),
    (i * 31) % 120000,
    CASE WHEN i % 10 < 7 THEN 'Available' WHEN i % 10 < 9 THEN 'Sold' ELSE 'Reserved' END,
    SYSUTCDATETIME()
FROM numbers;

SELECT COUNT(*) AS Vehicles,
       SUM(CASE WHEN AvailabilityStatus = 'Available' THEN 1 ELSE 0 END) AS Available
FROM Vehicles;
