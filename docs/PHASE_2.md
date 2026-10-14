# Phase 2 verification

Phase 2 delivers simulated USD wallets, payment sends and requests, full reversals, and a transactional double-entry ledger across the ASP.NET Core API, React web app, and Expo mobile app. It never moves real money.

## Delivered

- A balanced $1,000 sandbox opening transaction for every new user, plus migration backfill for existing users.
- Integer minor-unit USD amounts, explicit payment and request state machines, and full compensating reversals that preserve the original entries.
- Authenticated wallet, payment, payment-request, acceptance, reversal, and private history endpoints.
- One PostgreSQL transaction for payment state, scoped idempotency result, balance projections, balanced ledger entries, and the durable outbox record.
- Transaction-scoped PostgreSQL advisory locks for same-command serialization and deterministic wallet-row locks to prevent overspending and deadlocks.
- A generated OpenAPI contract, Zod runtime schemas, and shared cookie/bearer client methods with reusable idempotency keys.
- Responsive React and Expo flows for balance display, send, request, accept, reverse, and payment history.
- Domain tests for posting invariants and state transitions, and PostgreSQL integration tests for concurrency, retry, rollback, request ownership, and reversal.

## Verification

The following checks passed locally on 2026-10-13:

```sh
pnpm contracts:check
pnpm check
pnpm lint:mobile
pnpm build:web
EXPO_NO_TELEMETRY=1 CI=1 pnpm build:mobile
dotnet tool run dotnet-ef migrations has-pending-model-changes --project api-server/Senzy.Api.csproj --no-build
SENZY_TEST_POSTGRES='Host=localhost;Port=5433;Database=senzy;Username=senzy;Password=senzy' pnpm test
docker compose -f infra/compose.yaml build api
docker compose -f infra/compose.yaml up -d --wait postgres
docker compose -f infra/compose.yaml run --rm --no-deps api --migrate
docker compose -f infra/compose.yaml up -d --wait api
```

All 29 API/domain/integration tests passed against PostgreSQL. Both Phase 2 migrations applied to the persistent Compose database, the EF model reported no pending changes, and the rebuilt API and database both became healthy. The test suite proves that identical concurrent commands create one posting, different concurrent commands cannot overspend a wallet, changed idempotent replays are rejected, reversals use compensating entries, and injected pre-commit failures leave no payment, ledger, idempotency, or outbox residue.

A browser and physical-device visual smoke test remain manual follow-ups. The outbox is durable and atomic with payment posting, but Phase 3 still needs its delivery worker, idempotent event handlers, reconciliation, simulated cash-in/cash-out, notifications, limits, freezes, and operations views.

## Phase 3 entry point

Add simulated funding through the banking abstraction, then implement reconciliation and durable outbox delivery before introducing any broker. Keep external-provider work outside wallet locks and preserve PostgreSQL as the authority for balances and payment state.
