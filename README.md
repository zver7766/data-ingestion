# data-ingestion

# How to run: 

## Prerequisites

- Docker Desktop (recommended)
- Or .NET 8 SDK + a reachable SQL Server instance

The app reads the connection string from `ConnectionStrings:DefaultConnection` and supports overriding it via environment variable `ConnectionStrings__DefaultConnection` (double underscore).

On startup the API runs EF Core migrations (`db.Database.Migrate()`), so the DB schema is created/updated automatically.

## Option A: Run with Docker Compose (API + SQL Server)

From the repo root:

```bash
docker compose up --build
```

This will:

- Start SQL Server 2022 on `localhost:1433`
- Start the API on `http://localhost:8080`
- Provide DB credentials to the API via `ConnectionStrings__DefaultConnection` (see `docker-compose.yml`)

Swagger:

- `http://localhost:8080/swagger`

Health check:

- `http://localhost:8080/health`

Stop:

```bash
docker compose down
```

Reset DB volume (data loss):

```bash
docker compose down -v
```

## Option B: Run locally (dotnet)

1) Set the connection string (example uses the SQL Server from compose).

macOS/Linux (bash/zsh):

```bash
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=DataIngestionDb;User Id=sa;Password=your_password;TrustServerCertificate=True;Encrypt=False;"
```

Windows (PowerShell):

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=DataIngestionDb;User Id=sa;Password=your_password;TrustServerCertificate=True;Encrypt=False;"
```

2) Run the API:

```bash
dotnet run --project DataIngestion/DataIngestion.csproj
```

Swagger:

- Use the URL printed by `dotnet run` (usually `https://localhost:<port>/swagger` or `http://localhost:<port>/swagger`)

Health check:

- `/health`

## Run tests

Local:

```bash
dotnet test DataIngestion.Tests/DataIngestion.Tests.csproj -c Release
```

Docker image build (tests run during build):

```bash
docker build -f DataIngestion/Dockerfile -t dataingestion:test .
```

# Architecture description:


### Overview

The project is an ASP.NET Core Web API that ingests customer transactions into SQL Server and exposes endpoints for:

- **Ingestion**: single transaction JSON + batch CSV upload
- **Customers**: create customers + paginated transaction history with filters
- **Stats**: aggregate summary metrics over ingested data

### Main components

- **Controllers** (`DataIngestion/Controllers/*Controller.cs`)
  - Thin HTTP layer: routing, request binding, status-code mapping.
  - Returns consistent errors via `ProblemDetails` (see `DataIngestion/Infrastructure/ApiProblems.cs`).

- **Contracts** (`DataIngestion/Contracts/**`)
  - Request/response DTOs used by controllers and Swagger.

- **Services** (`DataIngestion/Services/**`)
  - Business logic is kept out of controllers.
  - **Ingest**
    - `TransactionValidationService`: validates + normalizes input (UTC datetime, currency uppercased, channel lowercased, precision rules).
    - `TransactionDuplicateChecker`: checks duplicates using `(CustomerId, Amount, Currency, SourceChannel)` and a small `TransactionDate` time window.
    - `TransactionIngestionService`: orchestrates validation → customer existence → dedup → persistence.
    - `BatchIngestionService`: streams CSV, processes rows in chunks, prefetches customer ids + dedup candidates per chunk, inserts in one `SaveChangesAsync` per chunk.
  - **Customers**
    - `CustomerTransactionsService`: builds a filtered `IQueryable`, applies pagination and ordering for fast reads.
  - **Stats**
    - `StatsSummaryService`: runs sequential aggregate queries using a single `DbContext` and returns a summary response.

- **Data layer (EF Core)** (`DataIngestion/Data/*`)
  - `AppDbContext` defines `Customers` and `IngestionEvents` sets and key indexes.
  - The ingestion event stores the normalized transaction fields:
    - `CustomerId`, `TransactionDate`, `Amount` (`decimal(18,6)`), `Currency`, `SourceChannel`

### Performance notes

- **Batch ingestion** is optimized for large uploads by:
  - streaming reads (no full-file load)
  - chunking (`IngestionConstants.BatchChunkSize`)
  - minimizing DB round-trips per chunk (set-based queries + one insert batch)
- **Customer transaction queries** are efficient by:
  - using `AsNoTracking()`
  - applying filters server-side
  - paginating via `Skip/Take` with deterministic ordering

### Error handling

All endpoints return consistent JSON errors as `ProblemDetails` with an additional `code` field (e.g. `validation_error`, `customer_not_found`, `duplicate_transaction`) for client-friendly handling.

# Trade-offs you considered, and what you’d do differently with more time section:

1. **Why used MSSQL?** After reading and brainstormed test task(with some basic models, some interaction inside) decided to use MSSQL because i decided to make transaction structured and have chunk logic for increasing performance of my app.


2. **Private password policy.** I know that store credential in appsetting (which will be pushed to production) or anywhere in code it`s very bad approach. Better them to store in user secrets or in Google Cloud Secret Manager. But I wanted just to show how I think without sensitive data protection overhead.


3. **CI/CD** If I had more time, I consider to add CI/CD pipeline (Something with Terraform and Google Cloud Builds).


4. **I`ve use more interfaces (not only just for unit tests)** But i decided as for MVP it will be sufficient.


5. Make StatsController to have some filtering too as CustomerController (maybe some generic logic).


6. Also add logging, metrics e.g. Prometheus/Grafana with alerting, some advanced health check events.


7. More deep down into optimization of most high-loaded methods and DeduplicationChecker logic.



# **“AI Usage” section:**

1. Which tools did you use and for what?

- For this project I used Cursor with GPT 5.5 and Sonnet 4.6.

2. What did you accept as-is, modify, or write from scratch?

- Most of unit tests was accepted as-is after review, just configure what library and what nuggets to use.
- Some modifying was done in AppDbContext/ApiProblems.
- From scratch was done some IngestionLogic.

3. Did the AI get anything wrong? How did you catch it?

- Some unit-tests which i wanted to see needed to deeper explanation for AI tools. 
- Also there was some migration failure which looped AI agent and i manually adjusted some objects properties and migrations how it should be like.
