# Phase 1 verification

Phase 1 delivers an authenticated, non-money prototype across the ASP.NET Core API, React web app, and Expo mobile app. It does not implement payments, balances, ledger entries, or real bank connectivity.

## Delivered

- PostgreSQL-backed ASP.NET Core Identity and an EF Core initial migration.
- Secure HTTP-only cookie sessions for the web client, including antiforgery and origin checks.
- Opaque bearer access/refresh sessions for mobile, with native refresh-token storage through Expo SecureStore.
- Security-stamp validation on every authenticated request so logout revokes other cookie and bearer sessions.
- Registration, sign-in, private profile reads/updates, unique normalized handles, sign-out, and a clearly marked fixture activity feed.
- Authenticated sandbox banking ownership; callers cannot select another user's ID.
- Consistent Problem Details with stable error codes and correlation IDs, structured request logging, rate limits, feature configuration, and allowlisted client telemetry.
- One generated OpenAPI contract, runtime Zod validation, and a shared cookie/bearer TypeScript API client used by web and mobile.
- Accessible responsive web screens and Expo Router authentication, activity, and profile screens with loading, retry, error, and empty states.
- Persistent Data Protection keys in the Compose `api-keys` volume and explicit database migration execution.

## Verification

The following checks passed locally on 2026-10-08:

```sh
pnpm contracts:check
pnpm check
SENZY_TEST_POSTGRES='Host=localhost;Port=5433;Database=senzy;Username=senzy;Password=senzy' pnpm test
pnpm lint:mobile
pnpm build:web
EXPO_NO_TELEMETRY=1 CI=1 pnpm build:mobile
docker compose -f infra/compose.yaml up -d --wait postgres
docker compose -f infra/compose.yaml run --rm --no-deps api --migrate
docker compose -f infra/compose.yaml up -d --build --wait
```

The PostgreSQL suite exercises migration creation, registration persistence, handle uniqueness under concurrency, cookie CSRF/origin enforcement, bearer refresh and revocation, lockout, cross-user isolation, authenticated banking ownership, feature flags, telemetry validation, and consistent errors. The Compose API and PostgreSQL services became healthy with the initial migration recorded.

Browser automation could not run because the available Codex browser session had reached its usage limit. The production web build and automated HTTP flows passed, but a visual browser smoke test and a physical-device Expo smoke test remain manual follow-ups before calling the UI release-tested. Hosted GitHub Actions also remains pending the first push of these changes.

## Phase 2 entry point

Implement simulated balances, payment requests/sends, the payment state machine, scoped idempotency, balanced double-entry postings, and an outbox record in one PostgreSQL transaction. Prove the invariants with concurrency, retry, rollback, and failure-injection tests before exposing money-like flows.
