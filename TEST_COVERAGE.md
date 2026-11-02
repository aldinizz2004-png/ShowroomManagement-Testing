# Test Coverage Summary

Showroom Management API · Task 1 (Vehicle Inventory & Testing) · measured with **coverlet** (XPlat Code Coverage) and **ReportGenerator**, November 2026.

## Test results

| Suite | Tool | Tests | Result |
| --- | --- | --- | --- |
| Unit tests (`tests/Showroom.UnitTests`) | xUnit + Moq | 51 | **51 passed, 0 failed** |
| Integration tests (`tests/Showroom.IntegrationTests`) | xUnit + WebApplicationFactory + SQLite in-memory | 42 | **42 passed, 0 failed** |
| **Total** | | **93** | **93 passed** |

## Coverage (API project `Showroom.Api`, migrations excluded)

| Scope | Line coverage | Branch coverage |
| --- | --- | --- |
| Unit + integration tests (combined) | **99.5 %** (201 / 202 lines) | **100 %** (48 / 48 branches) |
| Unit tests only | 48.5 % | 62.5 % |

Unit tests deliberately cover only the business logic (services, validation, token creation, policies); controllers and repositories are covered by the integration tests through real HTTP requests.

### Per class

| Class | Unit tests only | Combined |
| --- | --- | --- |
| `Services.VehicleService` | **100 %** | 100 % |
| `Services.VehicleValidator` | **96.8 %** | 100 % |
| `Services.AuthService` | **100 %** | 100 % |
| `Auth.TokenService` | **100 %** | 100 % |
| `Auth.Policies` | **100 %** | 100 % |
| `Common.ServiceResult<T>` | **100 %** | 100 % |
| `Controllers.VehiclesController` | 0 % (not a unit-test target) | 100 % |
| `Controllers.AuthController` | 0 % (not a unit-test target) | 100 % |
| `Repositories.VehicleRepository` | 0 % (mocked in unit tests) | 100 % |
| `Repositories.UserRepository` | 0 % (mocked in unit tests) | 100 % |
| `Data.AppDbContext` | 0 % | 100 % |

Not counted: EF Core migrations (generated code) and classes with no logic (entities and DTOs that only have auto-properties).

## What the tests cover

**Unit tests (business logic, no database)**
- Create: valid vehicle saved as `Available`; trimming; negative / zero price; each missing mandatory field; empty request (6 errors); invalid year (too old, not 4 digits, future); next-year model accepted; negative mileage; too-long color / unrealistic mileage; unknown brand value; too-long model and price above maximum. Invalid input is verified to **never reach the repository** (`Times.Never`).
- Get by id: found / not found. Filter: passes the filter to the repository; search for an unavailable or non-existent vehicle returns an empty list.
- Update: fields changed, status and `CreatedAt` kept; non-existent id → NotFound, nothing saved; invalid data → no database call at all.
- Status: allowed transitions; sold → available rejected; missing status; non-existent id.
- Delete: existing deleted; non-existent → NotFound, nothing deleted.
- Authentication: correct password, email normalisation, wrong password (no token issued), unknown email, register as Customer with hashed password, duplicate email.
- Token: `sub`, `email`, `role`, issuer, audience, expiry, HS256 signature.
- Authorization policies: ManageVehicles (Admin, SalesRep), DeleteVehicles (Admin), fallback rejects anonymous users.

**Integration tests (real HTTP pipeline, SQLite test database)**
- CRUD: 201 + `Location`, 200, 204, then 404 after delete; filters by brand, model (case-insensitive) and status; empty search → 200 `[]`.
- Errors: 400 for negative price, missing fields (all listed), unknown brand, invalid id (`abc`, `0`, `-5`), invalid update, sold → available; 404 for unknown id on GET / PUT / PATCH / DELETE.
- Authentication: login for each role (token contains the role), wrong password → 401, missing fields → 400, register → Customer, duplicate email → 400.
- Authorization: every vehicle endpoint without a token → 401; tampered token → 401; Customer can read but gets 403 on POST / PUT / PATCH / DELETE; SalesRep can add, update and change status but gets 403 on DELETE; Admin can delete.

## How to reproduce

```bash
dotnet tool restore
dotnet test ShowroomManagement.slnx --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory TestResults
dotnet reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"Html;TextSummary"
```

Open `coverage-report/index.html` for the line-by-line report.
