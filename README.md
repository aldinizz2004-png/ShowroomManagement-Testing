# Showroom Management API: Tasks 1 & 2

ProGineer Backend Practical Assignment · **Izzaldeen** · ASP.NET Core (.NET 10) · EF Core 10 · SQL Server · JWT + roles · xUnit · Moq · WebApplicationFactory · GitHub Actions · Docker

Backend REST API for a Palestinian automotive showroom selling **Chery, OMODA and JAECOO**.

- **Task 1:** vehicle inventory, unit tests and integration tests.
- **Task 2:** customer and reservation management, resource-based access control, Git workflow with feature branches and pull requests, and a CI/CD pipeline that tests every change and deploys `main` to a staging environment.

**Results:** 190 tests (111 unit + 79 integration), all passing · line coverage 99.4 %, branch coverage 92 % · see [`TEST_COVERAGE.md`](TEST_COVERAGE.md)

---

## 1. Solution structure

```
ShowroomManagement/
├── .github/workflows/ci.yml           CI (build + tests) and CD (deploy to staging)
├── Dockerfile                         API image (multi-stage, non-root)
├── ShowroomManagement.slnx
├── src/Showroom.Api/
│   ├── Controllers/     Vehicles, Customers, Reservations, Auth (+ ApiControllerBase: result -> HTTP status)
│   ├── Services/        business rules: VehicleService, CustomerService, ReservationService, AuthService, validators
│   ├── Repositories/    EF Core data access behind interfaces (mocked in unit tests)
│   ├── Auth/            Roles, Policies, CurrentUser, JwtOptions, TokenService
│   ├── Data/            AppDbContext (constraints, indexes, relations), DbInitializer (migrate + seed staff)
│   ├── DTOs/ Models/ Common/
│   └── Migrations/      InitialCreate, AddUsers, AddCustomers, AddReservations
├── tests/Showroom.UnitTests/          xUnit + Moq (services, auth, policies)
└── tests/Showroom.IntegrationTests/   WebApplicationFactory + SQLite in-memory (all endpoints)
```

Layers: `Controller → Service → IRepository → Repository → DbContext`. Services hold every business rule and depend only on repository interfaces, so unit tests replace them with Moq fakes.

## 2. Roles and access

| Module | Admin | SalesRep | Customer | No token |
| --- | --- | --- | --- | --- |
| Vehicles: list / view | all vehicles | all vehicles | **available vehicles only** | 401 |
| Vehicles: add / update / change status | ✓ | ✓ | 403 | 401 |
| Vehicles: delete | ✓ | 403 | 403 | 401 |
| Customers: add / update / view / list | ✓ | ✓ | 403 | 401 |
| Reservations: create | for any customer | for any customer | **for themselves only** | 401 |
| Reservations: view / list / cancel | all | all | **own reservations only** (403 otherwise) | 401 |
| Reservations: complete sale | ✓ | ✓ | 403 | 401 |

- **Role-based** rules are named policies (`ManageVehicles`, `DeleteVehicles`, `ManageCustomers`, `ManageReservations`) plus a fallback policy that requires a signed-in user everywhere.
- **Resource-based** rules (a customer may only touch their own reservations) are checked in `ReservationService` after loading the reservation: the reservation's customer must be linked to the user id in the JWT (`CurrentUser`). Customer lists are always filtered to the caller's own profile, even if another `customerId` is requested.

## 3. Modules

### Vehicles (Task 1)
Brand (`Chery`, `OMODA`, `JAECOO`), model, manufacturing year (1990 to next year), price in NIS (> 0), color, mileage (≥ 0), availability (`Available`, `Reserved`, `Sold`; a sold vehicle cannot change status). Filters: `brand`, `model` (contains), `status`. A vehicle that has reservations cannot be deleted (400); mark it as sold instead.

### Customers (Task 2)
Full name, email (unique), phone (7 to 20 digits, optional leading `+`), address. A customer profile can be linked to one user account:
- Staff add walk-in customers without an account.
- `POST /api/auth/register` creates a **Customer** account **and** its profile; if a walk-in profile with the same email exists, the new account is linked to it.

### Reservations (Task 2)
Customer, vehicle, reservation date (UTC, set by the server), expected purchase date, status (`Active`, `Cancelled`, `Completed`).

| Rule | Result |
| --- | --- |
| Vehicle must be `Available` | otherwise 400 |
| Expected purchase date from today up to 90 days | otherwise 400 |
| Creating a reservation | vehicle becomes `Reserved` |
| Cancelling (only `Active`) | vehicle becomes `Available` again |
| Completing a sale (staff, only `Active`) | vehicle becomes `Sold` |
| One active reservation per vehicle | also enforced by a filtered unique index (`IX_Reservations_OneActivePerVehicle`) |

## 4. Endpoints

| Method | Route | Success | Errors |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | 201 | 400 |
| POST | `/api/auth/login` | 200 + JWT | 400, 401 |
| GET | `/api/vehicles?brand=&model=&status=` | 200 | 401 |
| GET | `/api/vehicles/{id}` | 200 | 400, 401, 404 |
| POST | `/api/vehicles` | 201 | 400, 401, 403 |
| PUT | `/api/vehicles/{id}` | 200 | 400, 401, 403, 404 |
| PATCH | `/api/vehicles/{id}/status` | 200 | 400, 401, 403, 404 |
| DELETE | `/api/vehicles/{id}` | 204 | 400, 401, 403, 404 |
| GET | `/api/customers?search=` | 200 | 401, 403 |
| GET | `/api/customers/{id}` | 200 | 400, 401, 403, 404 |
| POST | `/api/customers` | 201 | 400, 401, 403 |
| PUT | `/api/customers/{id}` | 200 | 400, 401, 403, 404 |
| GET | `/api/reservations?customerId=&status=` | 200 | 401 |
| GET | `/api/reservations/{id}` | 200 | 400, 401, 403, 404 |
| POST | `/api/reservations` | 201 | 400, 401, 403 |
| PATCH | `/api/reservations/{id}/cancel` | 200 | 400, 401, 403, 404 |
| PATCH | `/api/reservations/{id}/complete` | 200 | 400, 401, 403, 404 |
| GET | `/health` | 200 `Healthy` (anonymous, checks the database) | 503 |

Example:

```http
POST /api/reservations
Authorization: Bearer <customer token>
Content-Type: application/json

{ "vehicleId": 12, "expectedPurchaseDate": "2026-11-20" }
```

## 5. Build, migrate and run

**Prerequisites:** .NET 10 SDK, SQL Server, EF Core CLI (`dotnet tool install --global dotnet-ef`).

1. Connection string: `src/Showroom.Api/appsettings.json` → `ConnectionStrings:Default` (default `Server=.\SQLEXPRESS01;Database=ShowroomDb;Trusted_Connection=True;TrustServerCertificate=True`).
2. Build: `dotnet build`
3. Migrate: `dotnet ef database update --project src/Showroom.Api` (in Development the API also migrates at startup).
4. Run: `dotnet run --project src/Showroom.Api --launch-profile http` → Swagger at **http://localhost:5048/swagger**.

Development accounts (from `appsettings.Development.json`, development only):

| Role | Email | Password |
| --- | --- | --- |
| Admin | `admin@showroom.local` | `Admin@12345` |
| SalesRep | `sales@showroom.local` | `Sales@12345` |
| Customer | `customer@showroom.local` | `Customer@12345` |

> The JWT key and these passwords are development values only. Staging secrets live in GitHub environment secrets; production would use a secret store.

## 6. Run the tests

```bash
dotnet test                                    # all 190 tests
dotnet test tests/Showroom.UnitTests           # 111 unit tests
dotnet test tests/Showroom.IntegrationTests    # 79 integration tests (no SQL Server needed)
```

Coverage report:

```bash
dotnet tool restore
dotnet test ShowroomManagement.slnx --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory TestResults
dotnet reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"Html;TextSummary"
```

**Unit tests** (xUnit + Moq, AAA): every service is tested with mocked repositories, including failure cases (invalid data, non-existent ids, unavailable vehicles, invalid dates, other customers' reservations, cancel/complete rules). `ReservationService` receives a `TimeProvider`, so tests fix "today" and check the 0-day and 90-day boundaries. Policies are tested with the real `Policies.AddShowroomPolicies`.

**Integration tests** (`WebApplicationFactory` + SQLite in-memory): the real API with JWT login per role. Covered: CRUD for vehicles and customers, the full reservation flow (reserve → vehicle Reserved → cancel → Available; complete → Sold), unavailable vehicles, invalid reservations, 401 without token, 403 for wrong role or another customer's reservation, 404, and `/health`.

## 7. Git workflow

- `main` is protected by a ruleset: changes only through **pull requests**, and the **Build & Test** check must pass before merging. Force pushes and branch deletion are blocked.
- One branch per feature, small commits with clear messages, one pull request per branch, branch deleted after merge.

| PR | Branch | Content |
| --- | --- | --- |
| #1 | `feature/ci-pipeline` | CI workflow; code review comment addressed (NuGet cache) and resolved |
| #2 | `feature/customers` | Customer module, registration linking, customer vehicle visibility |
| #3 | `feature/reservations` | Reservation module with ownership checks |
| #4 | `feature/staging-deploy` | `/health`, Dockerfile, deployment to staging |
| #5, #6 | `docs/readme-task2`, `docs/readme-title` | documentation; simulated merge conflict resolved |

## 8. CI/CD pipeline (`.github/workflows/ci.yml`)

```
push / pull request ──► Build & Test ──(main only, after success)──► Deploy to Staging
```

**Build & Test** (every push and every PR to `main`): restore (NuGet cache), build in Release, run unit tests, run integration tests, upload `.trx` results. Any failing test fails the job and blocks the merge.

**Deploy to Staging** (only on `main`, only after Build & Test passes, GitHub environment `staging`):
1. Build the Docker image and push it to **GitHub Container Registry**: `ghcr.io/<owner>/<repo>/showroom-api:staging` and `:sha-<commit>`.
2. Start SQL Server 2022 and the new image; the API applies the EF Core migrations.
3. **Smoke test:** `/health` returns `Healthy`, and a protected endpoint without a token returns `401`.
4. Write a deployment summary to the run page.

**Secrets:** `STAGING_DB_PASSWORD` and `STAGING_JWT_KEY` are **environment secrets** of `staging`; they never appear in the code or the image. The `staging` environment only accepts deployments from `main`.

Run the staging image locally:

```bash
docker build -t showroom-api .
docker run -p 8080:8080 \
  -e "ConnectionStrings__Default=Server=<host>,1433;Database=ShowroomStaging;User Id=sa;Password=<password>;TrustServerCertificate=True" \
  -e "Jwt__Key=<at least 32 characters>" -e Database__Initialization=Migrate \
  showroom-api
```

## 9. Key design decisions

- **Validation in services**, not DTO attributes, so unit tests check exactly the rules the API applies.
- **Repository interfaces** as the seam for mocking; services never use EF Core directly.
- **`ServiceResult<T>`** (Ok / Validation / NotFound / Forbidden): services never know about HTTP; `ApiControllerBase` maps results to 200/400/403/404.
- **Ownership checks in the service** (`CurrentUser`), because "is this reservation yours?" can only be answered after loading the data.
- **One transaction per change:** creating, cancelling or completing a reservation updates the reservation and the vehicle status in a single `SaveChanges`.
- **Customer profile separate from the login account**, so staff can register walk-in customers who have no account.
