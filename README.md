# Senzy

A web-and-mobile peer-to-peer payments foundation. This is an original implementation and does not contain Venmo branding or proprietary code. All banking is simulated; payments and real-money movement are not implemented.

## Workspace

- `apps/mobile` — Expo / React Native client
- `apps/web` — React + TypeScript web preview
- `api-server` — ASP.NET Core API with health, readiness, OpenAPI, and sandbox banking routes
- `packages/contracts` — shared request and response validation
- `infra/compose.yaml` — optional local PostgreSQL service
- `docs/PRODUCT_PLAN.md` — staged delivery plan and safety gates

## Prerequisites

- Node.js 22.13 or newer
- pnpm 11 or newer
- .NET SDK 10
- Expo Go on a physical phone, or an Android emulator
- Docker Desktop only if you want the bundled local PostgreSQL service

## Start

```powershell
pnpm install
pnpm dev:api
```

In a second terminal:

```powershell
pnpm dev:web
```

The web preview runs at `http://localhost:5173` and proxies `/api` to the ASP.NET API at `http://localhost:4001`. To start mobile too, run `pnpm dev:mobile` in another terminal. The API liveness endpoint is `http://localhost:4001/health`; `/health/ready` reports 503 until PostgreSQL is available. In development, OpenAPI is at `http://localhost:4001/openapi/v1.json`.

## Stub banking provider

The ASP.NET API includes a development-only sandbox banking adapter. It does not call a bank or accept routing or account numbers. Its link sessions and connected accounts are kept in memory and reset whenever the API restarts. These routes have no authentication yet and must not be enabled in a public deployment.

Available endpoints:

- `GET /v1/banking/institutions`
- `POST /v1/banking/link-sessions`
- `POST /v1/banking/accounts`
- `GET /v1/banking/accounts/:userId`

Banking responses are marked with `provider: "stub-banking"` and `sandbox: true`. `IBankingProvider` is the swap point for a future provider integration; real bank linking is out of scope now.

## Database

With Docker Desktop installed:

```powershell
docker compose -f infra/compose.yaml up -d
```

The API uses the development PostgreSQL connection in `api-server/appsettings.Development.json`; override it with `ConnectionStrings__Postgres` in a non-development environment. Database schema and migration tooling will be added with the .NET ledger implementation. The web shell and liveness route run without a database. No payment endpoint exists yet. Money-moving endpoints must require PostgreSQL and transactional, double-entry ledger writes.

## Checks

```powershell
pnpm check
pnpm test
```

`pnpm check` also builds the .NET API. `pnpm test` runs tests in workspace packages that define a test script; there are currently no automated test suites after removal of the legacy API.

This codebase is a development foundation, not a production-ready financial service. See the product plan for compliance, fraud, security, reconciliation, and operational requirements that must be completed before real funds are handled.
