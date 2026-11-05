SET SHOWPLAN_TEXT ON;
GO
SELECT [v].[Id], [v].[Price] FROM [Vehicles] AS [v]
WHERE [v].[Brand] = N'Chery' AND [v].[ManufacturingYear] = 2025 AND [v].[Price] >= 80000 AND [v].[Price] <= 150000 AND [v].[AvailabilityStatus] = N'Available'
ORDER BY [v].[Price], [v].[Id] OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY;
GO
SELECT [v].[Id], [v].[Price] FROM [Vehicles] AS [v]
WHERE [v].[AvailabilityStatus] = N'Available'
ORDER BY [v].[Price], [v].[Id] OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY;
GO
SET SHOWPLAN_TEXT OFF;
GO
