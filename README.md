# CircuitBay — High-Performance .NET 10 E-Commerce Storefront API

CircuitBay is a production-grade **.NET 10 ASP.NET Core Web API** designed for an electronics storefront backend. It implements Clean Architecture, Redis-backed response caching, SQL Server persistence with Entity Framework Core, JWT authentication, background job processing with Hangfire, containerized integration testing with Testcontainers, and full CI/CD GitHub Actions automation.

---

## 🌟 Key Features

* **Authentication & Identity**: JWT access token issuing, secure refresh token rotation, password hashing using PBKDF2 with SHA-256, and token-based password reset workflows.
* **Catalog & Search**: Multi-criteria product filtering, brand/category dynamic facets, and Redis version-invalidated caching.
* **Inventory Management**: Multi-warehouse stock tracking with optimistic concurrency controls (`RowVersion`) for high-concurrency checkout protection.
* **Shopping Cart & Coupons**: Persistent cart line operations, dynamic coupon code validation (percentage or fixed discount), and automatic abandoned cart maintenance.
* **Checkout Engine**: Dynamic shipping tax calculations, inventory reservation/commitment workflows, and audit trail generation.
* **Reviews & Wishlists**: Verified purchase review restrictions, rating recalculation engines, and customer wishlist management.
* **Background Jobs**: Scheduled maintenance tasks powered by Hangfire (cart cleanups, expired coupon deactivation, order packing queue transitions, refresh token purges).
* **Observability & Health Checks**: Custom Correlation ID middleware (`X-Correlation-Id`), structured exception handling, and deep `/healthz` + `/healthz/ready` diagnostic endpoints.

---

## 🛠️ Technology Stack

| Component | Technology |
| :--- | :--- |
| **Framework** | .NET 10 (ASP.NET Core Web API) |
| **Database & ORM** | Microsoft SQL Server 2022, Entity Framework Core 10 |
| **Caching** | Redis / Distributed Memory Cache |
| **Background Tasks** | Hangfire with SQL Server Storage |
| **Authentication** | JWT Bearer Authentication & PBKDF2 Password Hashing |
| **Containers** | Docker, Docker Compose, Testcontainers.MsSql, Testcontainers.Redis |
| **Testing** | xUnit, WebApplicationFactory, Testcontainers |
| **CI/CD** | GitHub Actions |

---

## 🏗️ Architecture & Project Structure

The codebase strictly adheres to **Clean Architecture** principles:

```text
CircuitBay/
├── src/
│   ├── CircuitBay.Domain/        # Domain entities, Value Objects, Enums, and core business rules
│   ├── CircuitBay.Contracts/     # Data Transfer Objects (DTOs), API Request/Response schemas
│   ├── CircuitBay.Application/   # Service orchestration, abstraction interfaces, app exceptions
│   ├── CircuitBay.Infrastructure/# EF Core DbContext, Migrations, Seed Data, Repositories, Redis Cache
│   └── CircuitBay.Api/           # Controllers, JWT Security, Middlewares, Hangfire Wiring
├── tests/
│   └── CircuitBay.Tests/         # Unit tests and Testcontainers integration test suite
├── .github/workflows/            # GitHub Actions CI/CD workflows (Build, Test, Docker Validation)
└── docker-compose.yml            # Multi-container local deployment stack
```

---

## ⚙️ Configuration & Environment Variables

Create a `.env` file in the repository root (see `.env.example` as a template):

```bash
# SQL Server Configuration
MSSQL_SA_PASSWORD=Strong_dev_password_2026!
ConnectionStrings__CircuitBay="Server=localhost,1433;Database=CircuitBay;User Id=sa;Password=Strong_dev_password_2026!;TrustServerCertificate=True;MultipleActiveResultSets=True"

# Redis Caching Configuration
ConnectionStrings__Redis="localhost:6379"

# JWT Authentication
Jwt__Issuer="CircuitBay"
Jwt__Audience="CircuitBay.Api"
Jwt__SigningKey="local-development-signing-key-for-circuitbay-2026"
Jwt__Minutes=90
```

---

## 🚀 Quick Start (Docker Compose)

The fastest way to boot the entire stack (API, SQL Server 2022, and Redis 7) is via Docker Compose:

```powershell
docker compose up --build
```

Once running:
* **API Base URL**: `http://localhost:8080`
* **Swagger UI Documentation**: `http://localhost:8080/swagger`
* **Hangfire Dashboard**: `http://localhost:8080/ops/jobs` *(Requires Admin authentication)*
* **Health Endpoint**: `http://localhost:8080/healthz/ready`

### Seeded Credentials for Testing

| Role | Email | Password |
| :--- | :--- | :--- |
| **Store Owner (Admin)** | `owner@circuitbay.local` | `OwnerPass!2026` |
| **Shopper** | `mira.chen@example.com` | `ShopperPass!2026` |

---

## 💻 Local CLI Commands

To compile, run tests, or manage database migrations locally:

```powershell
# Restore NuGet dependencies
dotnet restore CircuitBay.slnx

# Compile solution
dotnet build CircuitBay.slnx

# Run complete test suite (Unit & Testcontainers Integration Tests)
dotnet test CircuitBay.slnx

# Apply EF Core migrations manually
dotnet ef database update --project src/CircuitBay.Infrastructure --startup-project src/CircuitBay.Api
```

---

## 🔄 API Usage Flow

1. `POST /api/auth/login` — Authenticate and receive JWT access & refresh tokens.
2. `GET /api/catalog/products?query=dac&inStockOnly=true` — Browse catalog items.
3. `PUT /api/cart/lines` — Add or update items in customer shopping cart.
4. `PUT /api/cart/coupon` — Apply promotional discount code.
5. `POST /api/checkout` — Execute order creation and stock commit.
6. `POST /api/reviews` — Leave verified buyer product review.

---

## 🤖 CI/CD Automation Workflows

This repository includes continuous integration workflows located under `.github/workflows/`:
1. **Build (`build.yml`)**: Compiles solution with strict `TreatWarningsAsErrors=true`.
2. **Test (`test.yml`)**: Spawns isolated SQL Server & Redis containers via `Testcontainers` and executes integration tests.
3. **Docker Smoke Test (`docker-validation.yml`)**: Validates container image build, service readiness polling, and Swagger spec verification.

---

## 🖼️ Application Screenshots

*(Screenshots placeholder: Add Swagger UI and Hangfire Dashboard screenshots here when publishing)*

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
