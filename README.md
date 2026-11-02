# Showroom Management API: Task 1 (Vehicle Inventory & Testing)

ProGineer Backend Practical Assignment · **Izzaldeen** · ASP.NET Core (.NET 10) · EF Core 10 · SQL Server · JWT + roles · xUnit · Moq · WebApplicationFactory

Backend REST API for a Palestinian automotive showroom selling **Chery, OMODA and JAECOO**. Task 1 implements the **vehicle inventory module** and tests it with **unit tests** (business logic, mocked repository) and **integration tests** (real HTTP pipeline, test database).

**Results:** 93 tests (51 unit + 42 integration), all passing · line coverage 99.5 %, branch coverage 100 % · see [`TEST_COVERAGE.md`](TEST_COVERAGE.md)

---

## 1. Solution structure

```
ShowroomManagement/
├── ShowroomManagement.slnx
├── src/Showroom.Api/
│   ├── Controllers/     VehiclesController (CRUD, filter, status), AuthController (register, login)
│   ├── Services/        VehicleService + VehicleValidator (business rules), AuthService
│   ├── Repositories/    IVehicleRepository / VehicleRepository, IUserRepository / UserRepository (EF Core)
│   ├── Auth/            Roles + Policies, JwtOptions, TokenService
│   ├── Data/            AppDbContext (constraints, indexes), DbInitializer (migrate + seed staff accounts)
│   ├── DTOs/            requests (nullable fields), responses, query
│   ├── Models/          Vehicle, User, enums (Brand, AvailabilityStatus)
│   ├── Common/          ServiceResult<T> (Ok / Validation / NotFound)
│   └── Migrations/      InitialCreate, AddUsers
├── tests/Showroom.UnitTests/          xUnit + Moq: VehicleService, AuthService, TokenService, policies
├── tests/Showroom.IntegrationTests/   xUnit + WebApplicationFactory + SQLite in-memory
├── coverlet.runsettings               coverage settings (API project only, migrations excluded)
├── dotnet-tools.json                  ReportGenerator (local tool)
└── TEST_COVERAGE.md                   coverage summary report
```

**Layers:** `Controller → Service → IRepository → Repository → DbContext`.
The service holds every business rule and depends only on the repository **interface**, so unit tests replace it with a Moq fake and run without a database.

## 2. Vehicle model

| Field | Type | Rules |
| --- | --- | --- |
| `brand` | enum | `Chery`, `OMODA`, `JAECOO` (required) |
| `model` | string | required, max 50 |
| `manufacturingYear` | int | required, 1990 to next year |
| `price` | decimal (NIS) | required, > 0, max 10,000,000 |
| `color` | string | required, max 30 |
| `mileage` | int (km) | required, 0 to 2,000,000 |
| `availabilityStatus` | enum | `Available` (default), `Reserved`, `Sold`; a sold vehicle cannot change status |

The database enforces the same limits with `CHECK` constraints; enums are stored as text; indexes on `(Brand, Model)` and `AvailabilityStatus`.

## 3. Endpoints and roles

| Method | Route | Admin | SalesRep | Customer | No token | Responses |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/api/auth/register` | public | | | | 201 · 400 |
| POST | `/api/auth/login` | public | | | | 200 · 400 · 401 |
| GET | `/api/vehicles?brand=&model=&status=` | ✓ | ✓ | ✓ | 401 | 200 |
| GET | `/api/vehicles/{id}` | ✓ | ✓ | ✓ | 401 | 200 · 400 · 404 |
| POST | `/api/vehicles` | ✓ | ✓ | 403 | 401 | 201 · 400 |
| PUT | `/api/vehicles/{id}` | ✓ | ✓ | 403 | 401 | 200 · 400 · 404 |
| PATCH | `/api/vehicles/{id}/status` | ✓ | ✓ | 403 | 401 | 200 · 400 · 404 |
| DELETE | `/api/vehicles/{id}` | ✓ | 403 | 403 | 401 | 204 · 404 |

- Filters are optional and combinable: `brand` (exact), `model` (contains, case-insensitive), `status`. "All available vehicles" = `?status=Available`. No match → `200 []`.
- Public registration always creates a **Customer**; Admin and SalesRep accounts are seeded from configuration.
- Errors use RFC 7807 `ProblemDetails`; validation errors list each field (`errors.price`, `errors.model`, ...).

Example:

```http
POST /api/vehicles
Authorization: Bearer <token>
Content-Type: application/json

{ "brand": "Chery", "model": "Tiggo 8 Pro", "manufacturingYear": 2025, "price": 145000, "color": "White", "mileage": 0 }
```

## 4. Build, migrate and run

**Prerequisites:** .NET 10 SDK, SQL Server (Express is fine), EF Core CLI (`dotnet tool install --global dotnet-ef`).

1. **Connection string:** `src/Showroom.Api/appsettings.json` → `ConnectionStrings:Default` (default `Server=.\SQLEXPRESS01;Database=ShowroomDb;Trusted_Connection=True;TrustServerCertificate=True`). Change `Server=` to your instance.
2. **Build:**
   ```bash
   dotnet build
   ```
3. **Migrate** (creates `ShowroomDb` with `Vehicles` and `Users`):
   ```bash
   dotnet ef database update --project src/Showroom.Api
   ```
   In Development the API also applies migrations at startup (`Database:Initialization = Migrate`).
4. **Run:**
   ```bash
   dotnet run --project src/Showroom.Api --launch-profile http
   ```
   Swagger: **http://localhost:5048/swagger**. Log in with `POST /api/auth/login`, click **Authorize**, paste the token.

**Development accounts** (seeded from `appsettings.Development.json`, development only):

| Role | Email | Password |
| --- | --- | --- |
| Admin | `admin@showroom.local` | `Admin@12345` |
| SalesRep | `sales@showroom.local` | `Sales@12345` |
| Customer | `customer@showroom.local` | `Customer@12345` |

> The JWT key and these passwords are **development values only**. In production they come from environment variables / user secrets / a key vault, never from source control.

## 5. Run the tests

```bash
dotnet test                                              # all 93 tests
dotnet test tests/Showroom.UnitTests                     # 51 unit tests
dotnet test tests/Showroom.IntegrationTests              # 42 integration tests (no SQL Server needed)
```

**Coverage report:**

```bash
dotnet tool restore
dotnet test ShowroomManagement.slnx --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory TestResults
dotnet reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"Html;TextSummary"
```

Open `coverage-report/index.html`. Summary: [`TEST_COVERAGE.md`](TEST_COVERAGE.md).

## 6. Testing approach

### Unit tests: `tests/Showroom.UnitTests` (xUnit + Moq)
- **AAA** in every test: *Arrange* the mock and input, *Act* with one call, *Assert* the result.
- **Mocking:** `Mock<IVehicleRepository>` replaces the database. `Setup(...).ReturnsAsync(...)` decides what "the database" returns; `Verify(..., Times.Once / Times.Never)` checks what the service asked it to do, e.g. invalid data must **never** reach `AddAsync`.
- `[Theory]` + `[InlineData]` run one test with many inputs (each missing field, each invalid price or year, each role).
- Covered: all vehicle operations and their failure cases (invalid data, non-existent id for get / update / delete / status, empty search), authentication (`AuthService` with a mocked user repository and the real password hasher), JWT contents (`TokenService`), and the real authorization policies (`Policies.AddShowroomPolicies`, shared with `Program.cs`).

### Integration tests: `tests/Showroom.IntegrationTests` (WebApplicationFactory)
- `ShowroomApiFactory : WebApplicationFactory<Program>` starts the **real API in memory**: same routing, JWT validation, policies, controllers, services and repositories.
- **Test database:** SQL Server is replaced by **SQLite in-memory** (one open connection per factory); the schema is created from the EF Core model and one user per role is seeded with test-only credentials.
- Clients log in through `POST /api/auth/login` and send the returned token, so authentication is tested end to end.
- Covered: every CRUD endpoint and status code (200, 201, 204, 400, 401, 403, 404), filtering, validation errors, unauthenticated and tampered-token requests, and role permissions (Customers cannot add, update or delete; SalesReps cannot delete).
- Tests in a class share one factory (`IClassFixture`), and each test creates its own data (unique model names), so tests are independent of order.

## 7. Key design decisions

- **Validation in the service, not in DTO attributes.** Unit tests call the service directly, without MVC; keeping the rules in `VehicleValidator` (used by the service) means the tested rules are exactly the rules the API applies. Request fields are nullable, so a missing field is reported instead of becoming `0`.
- **Repository interface for testability.** The service does not know about EF Core; `IVehicleRepository` is the seam that Moq replaces.
- **`ServiceResult<T>`**: the service returns Ok / Validation / NotFound; only the controller knows about HTTP status codes.
- **Secure by default:** a fallback policy requires an authenticated user on every endpoint without `[AllowAnonymous]`; named policies `ManageVehicles` (Admin, SalesRep) and `DeleteVehicles` (Admin).
- **Passwords** hashed with `PasswordHasher<User>` (PBKDF2 + salt); the same 401 for unknown email and wrong password.
