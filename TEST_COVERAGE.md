# Test Coverage Summary

Showroom Management API · Tasks 1, 2 & 3 · measured with **coverlet** (XPlat Code Coverage) and **ReportGenerator**, November 2026. The same test suites run in GitHub Actions on every push and pull request.

## Test results

| Suite | Tool | Tests | Result |
| --- | --- | --- | --- |
| Unit tests (`tests/Showroom.UnitTests`) | xUnit + Moq | 164 | **164 passed, 0 failed** |
| Integration tests (`tests/Showroom.IntegrationTests`) | xUnit + WebApplicationFactory + SQLite in-memory | 128 | **128 passed, 0 failed** |
| **Total** | | **292** | **292 passed** |

## Coverage (API project `Showroom.Api`, migrations excluded)

| Scope | Line coverage | Branch coverage |
| --- | --- | --- |
| Unit + integration tests (combined) | **99.3 %** (487 / 490 lines) | **94.1 %** (177 / 188 branches) |
| Unit tests only | 51 % | 64.3 % |

Unit tests target the business logic only; controllers and repositories are covered by the integration tests through real HTTP requests.

| Class | Unit tests only | Combined |
| --- | --- | --- |
| `Services.VehicleService` | **100 %** | 100 % |
| `Services.VehicleQueryValidator` | **100 %** | 100 % |
| `Caching.VehicleCache` | 87.8 % | 100 % |
| `Common.PagedResult<T>` | **100 %** | 100 % |
| `Services.ReservationService` | **100 %** | 100 % |
| `Services.CustomerService` | **100 %** | 100 % |
| `Services.CustomerValidator` | **100 %** | 100 % |
| `Services.VehicleValidator` | 96.8 % | 96.8 % |
| `Services.AuthService` | **100 %** | 100 % |
| `Auth.TokenService` | **100 %** | 100 % |
| `Auth.Policies` | **100 %** | 100 % |
| Controllers (Vehicles, Customers, Reservations, Auth, base) | not a unit-test target | 100 % |
| `Repositories.VehicleRepository` | mocked in unit tests | 97.7 % |
| `Repositories.ReservationRepository` | mocked in unit tests | 94.7 % |
| Other repositories (Customer, User) | mocked in unit tests | 100 % |

## What the tests cover

**Vehicles (Task 1):** create / update / delete / get, invalid data (negative price, missing fields, invalid year, mileage, brand), non-existent ids, status rules, deleting a vehicle with reservations (400), customers see only available vehicles.

**Filtering, sorting and pagination (Task 3):**
- Each filter alone: brand, year, min / max price (inclusive boundaries), color (case-insensitive), status.
- Several filters combined, and the exact example query from the specification.
- Sorting by price, model and year in both directions.
- Page metadata (`totalCount`, `totalPages`, `hasNextPage`, `hasPreviousPage`), every vehicle exactly once across pages, and a page beyond the last.
- Empty results, and `%` in a filter treated as text.
- Invalid parameters → 400 with the field name: page, pageSize, year, negative or reversed prices, unknown sortBy or sortOrder, wrong types and unknown enum values.
- Customers keep seeing only available vehicles with any filter.

**Caching (Task 3):**
- Unit, with a real `MemoryCache`: cache miss then hit, different searches are separate entries, missing vehicles are not cached, sliding expiration renewed by access, absolute expiration, invalidating one vehicle or all lists, disabled mode, and a value loaded during an invalidation is not cached.
- Service: repeated reads hit the repository once; create, update, status change and delete clear the right entries; failed updates clear nothing.
- Reservations: create, cancel and complete invalidate the vehicle.
- Integration: a change made directly in the database is not visible while cached (proves the hit); after POST, PUT, PATCH status, DELETE and a reservation, the next read returns fresh data.

**SQL and indexes (Task 3):** the SQL Server query generated for the specification example contains `WHERE` on every filter, `ORDER BY [Price], [Id]`, `OFFSET / FETCH` and the `-- Vehicle search` tag; text filters use escaped `LIKE`; the EF model contains both composite indexes and the included year column.

**Customers and reservations (Task 2):** create with normalized email, invalid email and phone, duplicate email, staff-only access, registration linking; reserving available vs. reserved / sold vehicles, purchase date limits (0 and 90 days), ownership (403), cancel and complete rules, 401 without a token.

**Authentication and authorization:** login for each role, wrong password (401), token claims and expiry, registration, all four policies and the fallback policy, tampered token (401).

## How to reproduce

```bash
dotnet tool restore
dotnet test ShowroomManagement.slnx --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory TestResults
dotnet reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"Html;TextSummary"
```
