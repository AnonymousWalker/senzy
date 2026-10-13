# Phase 0 verification record

Status: implementation complete and checks verified locally. The GitHub Actions workflow is ready for its first hosted run after commit/push. No hosted CI result is claimed here.

## Deliverables

| Requirement | Evidence |
| --- | --- |
| Architecture and feature boundaries | [API README](../api-server/README.md) and [architecture decisions](ARCHITECTURE_DECISIONS.md). |
| Authentication, payment state, ledger, and stub decisions | ADR 002 and ADR 004–006; implementation stays in its planned phase. |
| Shared contract workflow | [Contract README](../packages/contracts/README.md), exported OpenAPI, generated TypeScript, runtime sample validation, and drift check. |
| Reproducible toolchain | Node `.nvmrc`, pinned pnpm, frozen lockfile, .NET `global.json`, and root commands. |
| Baseline tests and CI | [CI workflow](../.github/workflows/ci.yml), HTTP tests, real PostgreSQL readiness, mobile exports, and Gitleaks. |
| Workload and service objectives | [Operating targets](OPERATING_TARGETS.md), including 10M commands/day, peak/burst profile, latency, availability, recovery, and reconciliation. |
| Threat model | [Threat model](THREAT_MODEL.md), with current versus future controls distinguished. |

## Local verification

- Frozen-lockfile install succeeds with Node 24.15.0 and pnpm 11.1.3.
- `pnpm check`: all TypeScript packages and the .NET API build pass.
- `pnpm contracts:check`: deterministic OpenAPI/TypeScript artifacts match; health, institutions, link-session, and connected-account samples pass runtime validation.
- `pnpm test` with `CI=true` and `SENZY_TEST_POSTGRES`: 11 tests pass, including a real PostgreSQL 17 readiness check. Negative database cases, production route isolation, raw banking-field rejection, invalid IDs, user-bound tokens, and concurrent token consumption are covered.
- `pnpm lint:mobile`: mobile source lint passes.
- `pnpm build:web`: production bundle succeeds.
- `pnpm build:mobile`: Expo exports Android, iOS, and web JavaScript/assets successfully. This does not establish native-device behavior or produce store binaries.
- Gitleaks v8.24.2: no findings in the source snapshot or existing Git history.

Tests use an in-memory HTTP host; the PostgreSQL success test uses a disposable local container. No schema or ledger is created. Existing dependency peer warnings remain in the Expo/React toolchain; checks and exports pass, but a native-device smoke test is still needed before shipping mobile features.

## Remaining work

The first hosted workflow run requires pushing the changes. Branch protection and required-check settings are repository administration decisions and have not been changed.

Phase 1 starts with PostgreSQL migrations, Identity authentication/authorization, consistent API errors, tracing, profiles, payment handles, and a private fixture-backed activity feed. The mobile shell has access to shared contracts but does not yet call authenticated API endpoints. Payment and ledger implementation remain Phase 2. Load, recovery, and native-device tests are not claimed by this phase.
