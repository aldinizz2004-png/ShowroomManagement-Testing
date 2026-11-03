# Test Coverage Summary

Showroom Management API · Tasks 1 & 2 · measured with **coverlet** (XPlat Code Coverage) and **ReportGenerator**, November 2026. The same test suites run in GitHub Actions on every push and pull request.

## Test results

| Suite | Tool | Tests | Result |
| --- | --- | --- | --- |
| Unit tests (`tests/Showroom.UnitTests`) | xUnit + Moq | 111 | **111 passed, 0 failed** |
| Integration tests (`tests/Showroom.IntegrationTests`) | xUnit + WebApplicationFactory + SQLite in-memory | 79 | **79 passed, 0 failed** |
| **Total** | | **190** | **190 passed** |

## Coverage (API project `Showroom.Api`, migrations excluded)

| Scope | Line coverage | Branch coverage |
| --- | --- | --- |
| Unit + integration tests (combined) | **99.4 %** (366 / 368 lines) | **92 %** (92 / 100 branches) |
| Unit tests only | 44.2 % | 61 % |

Unit tests target the business logic only; controllers and repositories are covered by the integration tests through real HTTP requests.

| Class | Unit tests only | Combined |
| --- | --- | --- |
| `Services.ReservationService` | **100 %** | 100 % |
| `Services.CustomerService` | **100 %** | 100 % |
| `Services.CustomerValidator` | **100 %** | 100 % |
| `Services.VehicleService` | **100 %** | 100 % |
| `Services.VehicleValidator` | 96.8 % | 96.8 % |
| `Services.AuthService` | **100 %** | 100 % |
| `Auth.TokenService` | **100 %** | 100 % |
| `Auth.Policies` | **100 %** | 100 % |
| Controllers (Vehicles, Customers, Reservations, Auth, base) | not a unit-test target | 100 % |
| Repositories (Vehicle, Customer, User) | mocked in unit tests | 100 % |
| `Repositories.ReservationRepository` | mocked in unit tests | 94.7 % |

## What the tests cover

**Vehicles (Task 1):** create / update / delete / get / filter, invalid data (negative price, missing fields, invalid year, mileage, brand), non-existent ids, empty searches, status rules, deleting a vehicle with reservations (400), customers see only available vehicles.

**Customers:** create with normalized email, missing fields, invalid email and phone, duplicate email (create and update), not found, search, staff-only access (403 for customers, 401 without token), registration creates or links a customer profile.

**Reservations:** reserve an available vehicle (vehicle becomes Reserved), reserved / sold / non-existent vehicle (400), expected purchase date in the past or beyond 90 days (400) with boundary checks at 0 and 90 days, customer reserving for someone else (403), staff must give a customer id, customers only view, list and cancel their own reservations (403 otherwise), cancel returns the vehicle to Available, cancelling twice (400), completing a sale (vehicle Sold, staff only), not found (404), 401 on every endpoint without a token.

**Authentication and authorization:** login for each role, wrong password (401), token claims and expiry, registration, all four policies and the fallback policy, tampered token (401).

## How to reproduce

```bash
dotnet tool restore
dotnet test ShowroomManagement.slnx --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory TestResults
dotnet reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"Html;TextSummary"
```
