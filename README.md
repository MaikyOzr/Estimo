Estimo — Quotes/Invoices MVP (.NET 8, Minimal API, CQS)

Small SaaS MVP for creating clients and quotes, exporting PDFs, and protecting data per user (JWT).
Tech: .NET 8, Minimal API, CQS-style handlers, EF Core + PostgreSQL, Serilog (JSON), QuestPDF.

Features

Register/Login (JWT, HMAC-SHA256)

Clients: create, get by id (owned by current user)

Quotes: create, get by id (validated against ownership), export PDF

Health endpoint, structured logs, request correlation (X-Request-ID)

Auto-migrations on startup

Endpoints (base URL depends on your run mode)

POST /auth/register { email, password, confPassword } → 201 { accessToken, userId }

POST /auth/login { email, password } → 200 { accessToken, userId }

POST /clients { name, vatNumber?, address? } (Bearer) → 201 { id, ... }

GET /clients/{id} (Bearer) → 200 { ... } or 404

POST /quotes { clientId, amount, vatPercent } (Bearer) → 201 { id, totalWithVat }

GET /quotes/{id} (Bearer) → 200 { ... } or 404

GET /quotes/{id}/pdf (Bearer) → 200 application/pdf

GET /health → 200

Ownership: all reads/writes are scoped to the current user (extracted from JWT claims).

Project Structure
src/
  Estimo.Api/            # Program.cs, Minimal API, DI, Serilog, JWT
  Estimo.Application/    # CQS handlers, contracts, services interfaces
  Estimo.Domain/         # Entities (User, Client, Quote)
  Estimo.Infrastructure/ # DbContext, EF config, AuthService, PDF service
docker/
  docker-compose.yml
tests/ (optional later)

Prerequisites

.NET 8 SDK

Docker (optional, for Compose or running Postgres)

PostgreSQL 15 (Docker or local)

Configuration

Set connection string and JWT settings.

AppSettings (dev)

src/Estimo.Api/appsettings.Development.json

{
  "ConnectionStrings": {
    "pg": "Host=localhost;Port=5432;Database=estimo;Username=estimo;Password=estimo"
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [{ "Name": "Console", "Args": { "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact" } }],
    "Enrich": [ "FromLogContext" ]
  },
  "Jwt": {
    "Issuer": "Estimo",
    "Audience": "Estimo",
    "Key": "dev_super_long_secret_key_change_me_2025_32+_chars_min"
  }
}


Key rule: JWT key must be ≥ 128 bits (prefer 32+ characters).
You can also set secrets via:

dotnet user-secrets init --project src/Estimo.Api
dotnet user-secrets set "Jwt:Key" "very_long_secret_..." --project src/Estimo.Api
dotnet user-secrets set "Jwt:Issuer" "Estimo" --project src/Estimo.Api
dotnet user-secrets set "Jwt:Audience" "Estimo" --project src/Estimo.Api

Database

Run Postgres in Docker (example):

docker run -d --name estimo-postgres \
  -e POSTGRES_USER=estimo -e POSTGRES_PASSWORD=estimo -e POSTGRES_DB=estimo \
  -p 5432:5432 -v estimo_pg:/var/lib/postgresql/data postgres:15


Migrations run automatically at app startup (Database.Migrate()).

Run Locally
dotnet restore
dotnet run --project src/Estimo.Api
# check console for URLs, e.g. http://localhost:5000 or https://localhost:5001


Open Swagger (dev): /swagger.

Run with Docker Compose

docker/docker-compose.yml (already set up to run db + api):

docker compose -f docker/docker-compose.yml up --build
# API: http://localhost:5000   Postgres: localhost:5432


Environment overrides (Compose):

ConnectionStrings__pg=Host=db;Database=estimo;Username=estimo;Password=estimo

Jwt__Issuer, Jwt__Audience, Jwt__Key

Demo Flow (Smoke Test)
# 1) Register
curl -s -X POST http://localhost:5000/auth/register \
 -H "Content-Type: application/json" \
 -d '{"email":"demo@demo.local","password":"demo123","confPassword":"demo123"}'

# 2) Login → capture token
TOKEN=$(curl -s -X POST http://localhost:5000/auth/login \
 -H "Content-Type: application/json" \
 -d '{"email":"demo@demo.local","password":"demo123"}' | jq -r .accessToken)

# 3) Create client
CID=$(curl -s -X POST http://localhost:5000/clients \
 -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
 -d '{"name":"ACME","vatNumber":"ES123","address":"Barcelona"}' | jq -r .id)

# 4) Create quote
QID=$(curl -s -X POST http://localhost:5000/quotes \
 -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
 -d "{\"clientId\":\"$CID\",\"amount\":100,\"vatPercent\":21}" | jq -r .id)

# 5) Get PDF
curl -s -o q.pdf -H "Authorization: Bearer $TOKEN" http://localhost:5000/quotes/$QID/pdf -D -

Logging & Health

Structured logs via Serilog (Compact JSON) + UseSerilogRequestLogging().

X-Request-ID is sanitized and echoed in responses.

Health endpoints: GET /health → 200.

PDF Notes (QuestPDF)

Local (Windows/macOS): no extra steps.

Linux Docker image: if you see DllNotFoundException: libSkiaSharp, either:

add NuGet SkiaSharp.NativeAssets.Linux.NoDependencies to Estimo.Infrastructure, or

install OS deps in Dockerfile:

RUN apt-get update && apt-get install -y \
    libfontconfig1 libfreetype6 libharfbuzz0b libx11-6 libxext6 libxrender1 libxcb1 libpng16-16 \
 && rm -rf /var/lib/apt/lists/*

Troubleshooting

401 Unauthorized: Missing/invalid Authorization: Bearer <token>.

IDX10653 (key too short): use 32+ char key (Jwt:Key).

Value cannot be null (s): Jwt section misplaced (must be root-level, not under Serilog).

Invalid non-ASCII or control character in header: ensure header names have no spaces; we sanitize X-Request-ID.

DB connection errors: local run → Host=localhost; in Compose → Host=db.
