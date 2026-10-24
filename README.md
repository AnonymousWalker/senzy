# Senzy

A web-and-mobile peer-to-peer payments foundation. This is an original implementation and does not contain Venmo branding or proprietary code. Payments, balances, and banking are simulated; real-money movement is not implemented.

## Workspace

- `apps/mobile` — authenticated Expo / React Native client
- `apps/web` — authenticated React + TypeScript client
- `api-server` — ASP.NET Core API with Identity, profiles, simulated payments/funding, a double-entry ledger, durable outbox delivery, reconciliation, health, OpenAPI, and sandbox banking
- `packages/contracts` — generated OpenAPI types, runtime validation, and the shared API client
- `tests/Senzy.Api.Tests` — HTTP, authentication, ownership, migration, and PostgreSQL integration tests
- `infra/compose.yaml` — local PostgreSQL and containerized API
- `docs/PRODUCT_PLAN.md` — staged delivery plan and safety gates

## Prerequisites

- Node.js 24.15.0 (pinned in `.nvmrc`; use `nvm use` if you use NVM)
- pnpm 11.1.3 (pinned in `package.json`; enable Corepack with `corepack enable`)
- .NET SDK 10
- Expo Go on a physical phone, or an Android emulator
- Docker Engine with Compose v2, or Docker Desktop, for the containerized database/API

## Start

```powershell
pnpm install --frozen-lockfile
docker compose -f infra/compose.yaml up -d postgres
pnpm db:migrate
pnpm dev:api
```

In a second terminal:

```powershell
pnpm dev:web
```

The web preview runs at `http://localhost:5173` and proxies `/api` to the ASP.NET API at `http://localhost:4001`. To start mobile too, run `pnpm dev:mobile` in another terminal. The API liveness endpoint is `http://localhost:4001/health`; `/health/ready` reports 503 until PostgreSQL is available. In development, OpenAPI is at `http://localhost:4001/openapi/v1.json`.

## Stub banking provider

The ASP.NET API includes a development-only sandbox banking adapter. It does not call a bank or accept routing or account numbers. Its link sessions and connected accounts are kept in memory and reset whenever the API restarts. Account operations require authentication and derive ownership from the session, but the adapter must not be enabled in a public deployment.

Available endpoints:

- `GET /v1/banking/institutions`
- `POST /v1/banking/link-sessions`
- `POST /v1/banking/accounts`
- `GET /v1/banking/accounts`

Banking responses are marked with `provider: "stub-banking"` and `sandbox: true`. `IBankingProvider` is the swap point for a future provider integration; real bank linking is out of scope now.

## Database and API with Docker Compose

From the repository root on this Linux host:

```sh
mkdir -p /home/tony/data/senzy
docker compose -f infra/compose.yaml build api
docker compose -f infra/compose.yaml up -d --wait postgres
docker compose -f infra/compose.yaml run --rm --no-deps api --migrate
docker compose -f infra/compose.yaml up -d --wait
docker compose -f infra/compose.yaml ps
```

PostgreSQL 17 stores its data in `/home/tony/data/senzy`, bind-mounted at `/var/lib/postgresql/data`. Docker initializes the directory's PostgreSQL permissions on first start. Run the migration command once initially and again whenever a release includes a new migration; API replicas do not migrate automatically. The API waits for PostgreSQL to be healthy and connects using the Compose service name `postgres`. Its health check uses `/health/ready`.

The API is available at `http://localhost:4001`, and PostgreSQL at `localhost:5433` (`senzy` database, username, and local-development password). Host port 5433 avoids the existing database on port 5432; PostgreSQL still listens on 5432 inside Compose. Both published ports bind to loopback. This Compose configuration runs the API in Development so sandbox routes and OpenAPI remain available; it is intended for local development. The API process runs as a non-root user in a multi-stage .NET 10 image.

Run `pnpm dev:web` to use the containerized API; do not also run `pnpm dev:api` on the same port. After backend source changes, rerun the Compose command with `--build`; the container does not hot-reload. To use host-side .NET hot reload instead, stop the API container with `docker compose -f infra/compose.yaml stop api`, start only the database with `docker compose -f infra/compose.yaml up -d postgres`, then run `pnpm dev:api`.

```sh
docker compose -f infra/compose.yaml logs -f api postgres
docker compose -f infra/compose.yaml down
```

`down` removes containers and the Compose network but preserves the database files in `/home/tony/data/senzy` and the named `api-keys` volume used for Identity token and cookie protection. Use `down -v` only when you intentionally want to remove that key volume and invalidate protected sessions. The previous database Compose named volume, if present, is not migrated or deleted by this setup. Use a PostgreSQL dump/restore if you need to transfer existing data. Edit the bind mount source in `infra/compose.yaml` when using another host.

The API uses the development PostgreSQL connection in `api-server/appsettings.Development.json`; override it with `ConnectionStrings__Postgres` in another environment. Run `pnpm db:migrate` for a host-side database or `dotnet tool restore && dotnet ef migrations add <Name> --project api-server` when creating a reviewed migration. Simulated payment and funding endpoints require PostgreSQL and atomically commit the command record, idempotency result, double-entry ledger posting, balance projection, and outbox record. Background workers deliver notifications and run daily reconciliation after migrations have been applied.

## Checks

```powershell
pnpm check
pnpm contracts:check
pnpm test
pnpm lint:mobile
pnpm build:web
pnpm build:mobile
```

`pnpm check` checks all TypeScript packages and builds the .NET API. `pnpm test` runs available workspace tests and the .NET integration suite. The database-success test is explicitly skipped locally unless `SENZY_TEST_POSTGRES` is set to a test database connection string; CI requires it and starts PostgreSQL automatically. Other tests cover unavailable/unconfigured databases without needing PostgreSQL.

For the bundled local database, set `SENZY_TEST_POSTGRES` to `Host=localhost;Port=5433;Database=senzy;Username=senzy;Password=senzy` before running tests. Each persistence test creates and drops an isolated PostgreSQL schema; do not point this variable at a production database.

`pnpm contracts:generate` updates committed OpenAPI and TypeScript artifacts after API changes. `pnpm contracts:check` validates runtime response samples and detects generated-file drift. See the [contract workflow](packages/contracts/README.md).

`pnpm build:mobile` exports Android, iOS, and web JavaScript/assets locally; it does not invoke EAS or produce native store binaries. CI runs these checks and a pinned Gitleaks history scan on pull requests and pushes to `main`.

## Architecture and planning

- [Repository engineering instructions](AGENTS.md)
- [API organization](api-server/README.md)
- [Web client organization](apps/web/README.md)
- [Architecture decisions](docs/ARCHITECTURE_DECISIONS.md)
- [Accepted modular-monolith refactor guide](docs/MODULAR_MONOLITH_REFACTOR.md) — target ownership, transaction boundaries, migration steps, and verification criteria; not yet implemented
- [Workload and reliability targets](docs/OPERATING_TARGETS.md)
- [Initial threat model](docs/THREAT_MODEL.md)
- [Phase 0 verification](docs/PHASE_0.md)
- [Phase 1 verification](docs/PHASE_1.md)
- [Phase 2 verification](docs/PHASE_2.md)

This codebase is a development foundation, not a production-ready financial service. The durable provider rows and reconciliation results are sandbox fixtures, not bank settlement evidence. See the product plan for compliance, fraud, security, recovery, and operational requirements that must be completed before real funds are handled.
