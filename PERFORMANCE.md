# Performance Review: Filtering, Caching and Indexing

Showroom Management API · Task 3 · November 2026

This document records how `GET /api/vehicles` and `GET /api/vehicles/{id}` perform with and without caching and database indexes, the SQL that EF Core generates, and why the indexes were chosen.

## 1. Test setup

| Item | Value |
| --- | --- |
| Data | 100,000 vehicles (`perf/seed-vehicles.sql`): 3 brands, 9 years (2018 to 2026), prices 60,000 to 220,000 NIS, 70 % Available, 20 % Sold, 10 % Reserved |
| Database | SQL Server Express 2025 (`.\SQLEXPRESS01`), separate database `ShowroomPerf` |
| API | Release build on the same laptop, SQL command logging off (`perf/run-api.ps1`) |
| Client | `perf/measure.ps1`: one `HttpClient`, sequential requests, 10 warm-up + 200 timed requests per scenario |
| User | Customer token, so every list query also has `AvailabilityStatus = 'Available'` (the most frequent, public browsing path) |

Three configurations were measured:

1. **No cache, no indexes:** composite indexes dropped (`perf/drop-indexes.sql`), `VehicleCache:Enabled = false`.
2. **No cache, with indexes:** indexes from the migrations, cache still disabled.
3. **Cache and indexes:** normal configuration. After warm-up every request is a cache hit.

## 2. Latency results (milliseconds, end-to-end HTTP)

| Scenario | No cache, no indexes<br>avg / p95 | No cache, with indexes<br>avg / p95 | Cache + indexes<br>avg / p95 |
| --- | --- | --- | --- |
| Specification example: `brand=Chery&minPrice=80000&maxPrice=150000&year=2025&sortBy=price` | 22.81 / 29.39 | **2.96** / 5.16 | **0.93** / 1.47 |
| All available, sorted by price, page 1 | 71.46 / 94.86 | **9.56** / 14.71 | **0.86** / 1.37 |
| `brand=OMODA&minPrice=100000&maxPrice=120000&sortBy=price` | 26.26 / 28.84 | **1.63** / 2.83 | **0.81** / 1.12 |
| Deep page: `sortBy=price&page=500&pageSize=20` | 71.12 / 78.51 | **24.05** / 28.29 | **0.68** / 0.91 |
| Vehicle details: `/api/vehicles/12341` | 0.91 / 1.67 | 0.96 / 1.71 | **0.65** / 0.75 |

Raw numbers (avg, p50, p95, min, max): [`perf/results.csv`](perf/results.csv).

**Findings**

- Indexes make uncached list queries **3x to 16x** faster (for example 71.5 ms to 9.6 ms when listing all available vehicles by price).
- The cache removes the database from repeated requests: every scenario drops to **under 1 ms**, about 25x to 100x faster than the unindexed database and 2x to 35x faster than the indexed one.
- Details by id are already fast (primary key seek), so the cache gains little there (0.96 ms to 0.65 ms). It still saves one database round trip per request.
- The remaining slowest uncached case is the deep page (24 ms): `OFFSET 9980` has to walk past 9,980 index rows. Keyset pagination would fix this, but the task asks for page numbers, so the cache covers repeated deep pages.
- "All available" (9.6 ms) is dominated by `COUNT(*)` over 70,000 matching rows. That count is cached with the page.

## 3. EF Core query audit

All filtering, sorting and pagination run in SQL Server. The repository builds an `IQueryable` and materializes only the requested page. SQL is visible in three ways:

- In Development, every command is logged (`Microsoft.EntityFrameworkCore.Database.Command: Information`).
- Search queries carry the tag `-- Vehicle search` (`TagWith`), so they are easy to find in logs and in SQL Server monitoring.
- `VehicleQuerySqlTests` prints the SQL with `ToQueryString()` and asserts its shape.

Each uncached list request sends two commands (none when the page is cached). Both are parameterized, so SQL Server reuses the plan for any filter values.

**Count:**

```sql
-- Vehicle search
SELECT COUNT(*)
FROM [Vehicles] AS [v]
WHERE [v].[Brand] = @brand AND [v].[ManufacturingYear] = @year
  AND [v].[Price] >= @minPrice AND [v].[Price] <= @maxPrice
```

**Page** (not run when the total count is 0):

```sql
-- Vehicle search
SELECT [v].[Id], [v].[AvailabilityStatus], [v].[Brand], [v].[Color], [v].[CreatedAt],
       [v].[ManufacturingYear], [v].[Mileage], [v].[Model], [v].[Price]
FROM [Vehicles] AS [v]
WHERE [v].[Brand] = @brand AND [v].[ManufacturingYear] = @year
  AND [v].[Price] >= @minPrice AND [v].[Price] <= @maxPrice
ORDER BY [v].[Price], [v].[Id]
OFFSET @p ROWS FETCH NEXT @p5 ROWS ONLY
```

Text filters use escaped `LIKE`, so `%` or `_` typed by a user are matched as text:

```sql
WHERE [v].[Model] LIKE @pattern ESCAPE N'\' AND [v].[Color] LIKE @pattern3 ESCAPE N'\'
```

Other observations:

- `AsNoTracking()` on all reads: no change tracking cost for data that is only returned.
- `ThenBy(Id)` makes the order deterministic, so pages never repeat or skip a vehicle when prices are equal.
- No N+1 queries: vehicles have no navigation properties loaded in the list.

## 4. Index design

### Frequent query paths

| Path | Filter | Sort |
| --- | --- | --- |
| Customer browsing (every customer list) | `AvailabilityStatus = 'Available'` | price |
| Customer browsing by brand and budget | Brand + Available + price range (+ year) | price |
| Staff search | brand, price range, status | price, model, year |

### Indexes

| Index | Columns | Include | Serves |
| --- | --- | --- | --- |
| `IX_Vehicles_Brand_Status_Price` | Brand, AvailabilityStatus, Price | ManufacturingYear | brand + status + price range; year is checked inside the index |
| `IX_Vehicles_Status_Price` | AvailabilityStatus, Price | | all available vehicles ordered by price; status-only filters |

**Column order:** columns compared with `=` come first (Brand, AvailabilityStatus), and the range and sort column (Price) comes last. SQL Server can then seek directly to `Chery + Available + 80,000` and read rows already ordered by price, so no Sort operator is needed and `TOP 10` stops after 10 rows.

**Removed indexes:**
- `(Brand, Model)`: the model filter is `LIKE '%text%'`, and a leading wildcard cannot use an index.
- `(AvailabilityStatus)`: it is the first column of `IX_Vehicles_Status_Price`, so it was redundant.

### Measured effect (SQL Server `SET STATISTICS IO`, logical reads, 100,000 rows)

| Query | No indexes | First version<br>(Brand, Status, Price) | Final<br>(+ INCLUDE year) |
| --- | --- | --- | --- |
| Q1 page: Chery, Available, 2025, 80k-150k, by price | 1,519 (scan + sort) | 281 | **39** |
| Q2 count for Q1 | 1,519 | 1,519 | **76** |
| Q3 page: all available by price | 1,519 (scan + sort) | **71** | **71** |
| Q4 page: OMODA, Available, 100k-120k, by price | 1,519 (scan + sort) | **71** | **71** |

Plans (`perf/query-plans.sql`):

```
Without indexes:  Sort(TOP 10, ORDER BY Price, Id)  <-  Clustered Index Scan (PK_Vehicles, all 100,000 rows)
With indexes:     Top(10)  <-  Index Seek IX_Vehicles_Brand_Status_Price (Brand, Status, Price range) ORDERED FORWARD
                               + Key Lookup on PK_Vehicles for the 10 returned rows
```

**Audit finding:** the first version left the count query (Q2) as a full scan, because `ManufacturingYear` was not in the index and SQL Server preferred scanning over thousands of key lookups. Adding `ManufacturingYear` as an included column (migration `IncludeYearInBrandIndex`) lets SQL Server filter the year inside the index. That cut Q2 from 1,519 to 76 reads, and the specification example from 17.3 ms to 3.0 ms uncached.

## 5. Trade-offs

| Cost | Measured / reason | Decision |
| --- | --- | --- |
| Storage | Indexes 5.4 MB + 3.8 MB next to a 11.9 MB table | Acceptable for the read gain |
| Slower writes | Bulk insert of 50,000 vehicles: about 0.6 to 0.9 s with indexes, 0.3 s without | Vehicles are written rarely (staff only) and read constantly |
| Not covering | Selected rows still need a key lookup for the other columns | Only page-size rows (10 to 50) are looked up; a covering index would duplicate the table |
| No index on color, year alone, model | Low selectivity (6 colors, 9 years) or `LIKE '%x%'` | They are applied as residual filters after the seek |
| Deep `OFFSET` pages | Cost grows with page number | Cached; keyset pagination is the next step if needed |

## 6. Caching design

- **Cache-aside** with `IMemoryCache` in `VehicleCache` (singleton). Size limit 1,024 entries, every entry has size 1.
- **Keys:** details `vehicles:detail:{id}`. Lists `vehicles:list:{VehicleSearch}`, a record of all filters, sort, page and page size. A customer's search always contains `Status = Available`, so customer and staff results never mix.
- **Expiration** (`appsettings.json`, section `VehicleCache`): lists 1 min sliding / 5 min absolute, details 2 min sliding / 10 min absolute. Sliding removes idle entries early; absolute caps how long any entry can live.
- **Invalidation:**
  - Create: clears all lists.
  - Update, status change or delete: removes the vehicle's detail entry and clears all lists.
  - Reservation create, cancel or complete: the same, because they change the vehicle status.
  - All lists are cleared at once by cancelling a shared `CancellationChangeToken`.
- **Freshness:** a version counter is read before loading. If any invalidation happens while the database call is running, the loaded value is returned but not cached, so a stale value can never be stored after a change.
- **Limits:** the cache is per API instance. With several instances behind a load balancer, invalidation would need a distributed cache (for example Redis) or a message to all instances. Concurrent misses for the same key may both hit the database once (no request coalescing). This is acceptable at this scale.

## 7. Reproduce

```powershell
.\perf\run-api.ps1 -NoCache
sqlcmd -S .\SQLEXPRESS01 -E -d ShowroomPerf -v VehicleCount=100000 -i perf\seed-vehicles.sql

sqlcmd -S .\SQLEXPRESS01 -E -d ShowroomPerf -i perf\drop-indexes.sql
.\perf\measure.ps1 -BaseUrl http://localhost:5060 -Label "No cache, no indexes" -OutFile perf\results.csv
sqlcmd -S .\SQLEXPRESS01 -E -d ShowroomPerf -i perf\create-indexes.sql
.\perf\measure.ps1 -BaseUrl http://localhost:5060 -Label "No cache, with indexes" -OutFile perf\results.csv

.\perf\run-api.ps1
.\perf\measure.ps1 -BaseUrl http://localhost:5060 -Label "With cache and indexes" -OutFile perf\results.csv

sqlcmd -S .\SQLEXPRESS01 -E -d ShowroomPerf -i perf\query-audit.sql
sqlcmd -S .\SQLEXPRESS01 -E -d ShowroomPerf -i perf\query-plans.sql
```

The first command creates `ShowroomPerf` through the migrations. Stop the API with `Ctrl + C` between runs. Absolute numbers depend on the machine; the ratios are what matter.
