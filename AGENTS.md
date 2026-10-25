# Senzy engineering instructions

These instructions apply across the repository. More specific `AGENTS.md` files may add rules for their subtree. This file intentionally does not summarize `docs/PRODUCT_PLAN.md`; consult the product plan only when the task is explicitly about scope, sequencing, or delivery status.

## Read the relevant guide

Load only the guidance needed for the files being changed:

- Backend architecture, module ownership, persistence, or payments: `api-server/README.md` and `docs/BACKEND_ARCHITECTURE.md`.
- React web work: `apps/web/README.md`.
- API shapes, generated TypeScript, runtime validation, or the shared client: `packages/contracts/README.md`.
- Performance, reliability, capacity, recovery, or observability: `docs/OPERATING_TARGETS.md`.
- Authentication, authorization, sensitive data, provider boundaries, logging, or public exposure: `docs/THREAT_MODEL.md`.
- Local setup, migrations, Compose, and repository-wide commands: `README.md`.
- C# layout in `api-server/`: the **C# readability** section below.

`docs/PHASE_0.md` and `docs/PHASE_1.md` are verification records, not implementation instructions. Do not update them to claim checks that were not actually run.

## Architecture

Senzy is a pragmatic layered modular monolith: one ASP.NET Core deployable, one PostgreSQL system of record, and business-feature modules as the primary ownership boundary.

- Inside a non-trivial module, dependencies point API → Application → Domain. Infrastructure implements persistence and provider concerns and is wired at the composition root.
- Keep Minimal API endpoints focused on HTTP binding, transport validation, authorization, use-case invocation, and response mapping.
- Application handlers own workflow coordination, actor/resource ownership, idempotency, and transaction boundaries.
- Domain code owns state transitions and financial invariants and must not depend on ASP.NET Core, EF Core, HTTP DTOs, or provider SDKs.
- Small CRUD-style features may remain compact until real behavior warrants another layer. Do not create empty layers or pass-through abstractions.
- Do not add MediatR, generic repositories, a unit-of-work wrapper around EF Core, or an interface for every class without a concrete need.
- A shared `SenzyDbContext` provides transaction coordination; it does not make every module's entities writable by every feature.
- Cross-module work uses explicit application/domain contracts. Do not call another module's HTTP endpoint internally or mutate its tables ad hoc.
- Keep the modular monolith in one application project until compile-time isolation, independent reuse, or deployment provides a demonstrated benefit.

## Financial and persistence invariants

- PostgreSQL is authoritative for users and future payment, ledger, idempotency, and outbox data. Redis or brokers must never become authoritative for balances or payment state.
- Store monetary values as integer minor units with an explicit currency; never use floating point.
- A posted payment, its scoped idempotency result, balanced ledger entries, and outbox record must commit in one PostgreSQL transaction.
- Posted ledger entries are immutable. Corrections use linked compensating transactions.
- A retry with the same actor/operation/idempotency key and request fingerprint returns the committed result; changed input for the same key is rejected.
- Never acknowledge a payment as posted before its transaction commits. Treat an uncertain commit as unknown, not automatically failed.
- Run reviewed EF migrations explicitly as a deployment step; do not migrate independently from every API replica.
- External provider calls are not atomic with PostgreSQL. Model them as explicit pending workflows with durable retries and reconciliation.

## Identity, privacy, and sandbox boundaries

- Derive the actor from the authenticated principal. Never accept a caller-supplied user ID as proof of ownership.
- Browser mutations use the secure cookie, allowed-origin, and antiforgery flow. Mobile uses opaque bearer tokens and OS-backed refresh-token storage.
- Return non-enumerating authentication errors and consistent Problem Details with a stable application error code and correlation ID.
- Never log tokens, passwords, raw bank details, unrestricted request bodies, or private user data. Avoid user IDs in metric labels.
- Banking remains a Development-only in-memory stub. Reject raw routing/account fields and clearly mark all responses and UI as sandbox data.
- The repository is not approved for real money, real bank linking, or public deployment. Do not weaken environment guards or describe simulated records as settled funds.
- Revisit `docs/THREAT_MODEL.md` when changing authentication, ledger behavior, provider integration, logging/data retention, or deployment exposure; add abuse-case tests with the change.

## C# readability

Hand-written C# in `api-server/` should scan as structured code, not a packed essay. Keep 2-space indent to match `.editorconfig`. Do not reformat generated EF migrations.

- Put one fluent call per line (`MapGet` / `MapPost`, then `.WithName`, `.Produces`, `.ProducesProblem`).
- Put a blank line between route groups and between distinct steps inside a method (validate, lock, mutate, commit).
- Split long parameter lists, constructor arguments, and record payloads one item per line.
- Use braces on `if`, `try`, and `catch`. Do not write one-line `if (...) return` or `try { ... } catch { ... }` bodies.
- Split long boolean conditions across lines with the operator at the start of the continuation.
- Prefer a local name for a long compound condition over packing it into a single `if`.
- Leave generated migration files untouched.

## HTTP contracts and clients

- ASP.NET endpoint metadata and named request/response types are the HTTP source of truth.
- Give versioned endpoints stable operation IDs, explicit response metadata, and Problem Details error responses.
- After an endpoint change, run `pnpm contracts:generate`, inspect the OpenAPI and generated TypeScript diff, update runtime Zod schemas and consumers, then run `pnpm contracts:check`.
- Do not edit `packages/contracts/openapi.json` or `packages/contracts/src/generated/api.ts` by hand.
- Web and mobile use the shared client from `@senzy/contracts`; do not redefine transport DTOs locally.

## React and Expo organization

- In the web app, dependency direction is `main → app → features → api/shared`.
- A feature owns its presentation and local behavior. It must not import from `app` or reach into another feature's private files.
- Expose a module's supported surface through small, explicit `index.ts` files with named exports. Do not use `export *` barrels or an application-wide catch-all barrel.
- Promote code to `shared/` only when it is feature-neutral and genuinely reused.
- Keep cross-feature session/API orchestration in the app layer; keep form and screen behavior in the owning feature.
- For mobile work, also follow `apps/mobile/AGENTS.md`.

## Verification

Run checks proportionate to the changed area and report exactly what ran:

- Contracts or API surface: `pnpm contracts:check`.
- TypeScript plus API compilation: `pnpm check`.
- Backend behavior: `pnpm test`; PostgreSQL persistence tests require `SENZY_TEST_POSTGRES` and create isolated schemas. Never point them at production.
- Web: `pnpm --filter @senzy/web check` and `pnpm build:web`.
- Mobile: `pnpm lint:mobile` and `pnpm build:mobile`.
- EF model changes: create and review a migration, then confirm there are no pending model changes.
- Container/runtime changes: apply migrations explicitly, start Compose, and verify `/health/ready`.

Add focused tests for ownership, validation, failures, and concurrency wherever those risks change. Payment and ledger changes require real PostgreSQL integration tests for atomicity, balance, idempotency, retry, rollback, and concurrent spending—not only mocked or in-memory tests.
