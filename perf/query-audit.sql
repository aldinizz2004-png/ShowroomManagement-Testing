SET NOCOUNT ON;
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

PRINT '=== Q1 customer example: Chery, Available, 2025, 80k-150k, sort by price, page 1 ===';
SELECT [v].[Id], [v].[AvailabilityStatus], [v].[Brand], [v].[Color], [v].[CreatedAt], [v].[ManufacturingYear], [v].[Mileage], [v].[Model], [v].[Price]
FROM [Vehicles] AS [v]
WHERE [v].[Brand] = N'Chery' AND [v].[ManufacturingYear] = 2025 AND [v].[Price] >= 80000 AND [v].[Price] <= 150000 AND [v].[AvailabilityStatus] = N'Available'
ORDER BY [v].[Price], [v].[Id]
OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY;

PRINT '=== Q2 count for Q1 ===';
SELECT COUNT(*) FROM [Vehicles] AS [v]
WHERE [v].[Brand] = N'Chery' AND [v].[ManufacturingYear] = 2025 AND [v].[Price] >= 80000 AND [v].[Price] <= 150000 AND [v].[AvailabilityStatus] = N'Available';

PRINT '=== Q3 all available vehicles sorted by price, page 1 ===';
SELECT [v].[Id], [v].[AvailabilityStatus], [v].[Brand], [v].[Color], [v].[CreatedAt], [v].[ManufacturingYear], [v].[Mileage], [v].[Model], [v].[Price]
FROM [Vehicles] AS [v]
WHERE [v].[AvailabilityStatus] = N'Available'
ORDER BY [v].[Price], [v].[Id]
OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY;

PRINT '=== Q4 brand + price range, available, sorted by price ===';
SELECT [v].[Id], [v].[AvailabilityStatus], [v].[Brand], [v].[Color], [v].[CreatedAt], [v].[ManufacturingYear], [v].[Mileage], [v].[Model], [v].[Price]
FROM [Vehicles] AS [v]
WHERE [v].[Brand] = N'OMODA' AND [v].[AvailabilityStatus] = N'Available' AND [v].[Price] >= 100000 AND [v].[Price] <= 120000
ORDER BY [v].[Price], [v].[Id]
OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY;
