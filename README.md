# CircuitBay

CircuitBay is a .NET 10 ASP.NET Core Web API for an electronics storefront with catalog search, Redis-backed catalog caching, inventory, carts, coupons, checkout, wishlist, reviews, audit logging, Hangfire maintenance jobs, and admin operations.

The code is split into domain, contracts, application services, infrastructure, API, and tests. EF Core targets SQL Server, JWT handles authentication, and startup applies migrations plus seed data.

## Projects

- `src/CircuitBay.Domain` - aggregate models and business rules.
- `src/CircuitBay.Contracts` - API request/response DTOs.
- `src/CircuitBay.Application` - service orchestration and app exceptions.
- `src/CircuitBay.Infrastructure` - EF Core SQL Server context, repository, seed data, password hashing, migrations.
- `src/CircuitBay.Api` - controllers, JWT setup, middleware, Hangfire wiring.
- `tests/CircuitBay.Tests` - unit tests for auth and commerce rules.

## Local Development Notes
Docker Desktop may require additional startup time on first SQL Server container initialization.

## Run Locally

```powershell
docker compose up --build
```

API base URL:

```text
http://localhost:8080
```

Seeded users:

```text
owner@circuitbay.local / OwnerPass!2026
mira.chen@example.com / ShopperPass!2026
```

## Useful Commands

```powershell
dotnet restore CircuitBay.slnx
dotnet build CircuitBay.slnx
dotnet test CircuitBay.slnx
dotnet ef database update --project src/CircuitBay.Infrastructure --startup-project src/CircuitBay.Api
```

## Configuration

Use environment variables for deployed settings:

```text
ConnectionStrings__CircuitBay
ConnectionStrings__Redis
Jwt__Issuer
Jwt__Audience
Jwt__SigningKey
Jwt__Minutes
```

## API Flow

1. `POST /api/auth/login`
2. `GET /api/catalog/products?query=dac&inStockOnly=true`
3. `PUT /api/cart/lines`
4. `PUT /api/cart/coupon`
5. `POST /api/checkout`
6. `POST /api/reviews`

Admin inventory and dashboard endpoints require `Admin` or `InventoryManager`.
Coupon and audit endpoints require `Admin`.
Hangfire dashboard is available at `/ops/jobs` for authenticated admins.

## CI/CD & Repository Automation

This repository includes a focused GitHub Actions automation suite under `.github/workflows/` designed to keep the backend runtime-stable and secure:

1. **Build Verification (`build.yml`)**
   - Automatically restores and compiles all solution projects on every commit and pull request targeting `main` or `dev`.
   - Utilizes SDK-integrated NuGet package caching via `actions/setup-dotnet` for fast execution.
   - Enforces strict quality standards with `TreatWarningsAsErrors=true`, while excluding non-blocking NuGet audit warnings (`NU1901-NU1904`) to prevent transient security alerts from halting the pipeline.

2. **Automated Test Suite (`test.yml`)**
   - Executes the complete test suite (both unit rules and heavy database/caching integration tests).
   - Testcontainers automatically spins up and tears down fresh Dockerized SQL Server and Redis environments for isolation.
   - Generates and uploads XML `.trx` test results as build artifacts on failure or success for diagnostic transparency.

3. **Docker Compose Smoke Test (`docker-validation.yml`)**
   - Builds the production-grade application Docker image and boots the multi-container stack via `docker compose`.
   - Loops with aggressive polling to verify readiness check health parameters (`/healthz/ready`) after migrations and seed data apply.
   - Verifies API endpoint accessibility by validating and fetching the Swagger JSON specification metadata.
   - Employs a failure fallback block to dump all service logs (`api`, `sql`, `redis`) for instant container-level troubleshooting.

