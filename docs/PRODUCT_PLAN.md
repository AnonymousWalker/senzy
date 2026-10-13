# Senzy product and engineering plan

## Product boundary

Build an original peer-to-peer payments product for web and mobile. Core features include identity, profiles, payment handles, requests, transfers, activity, notifications, and funding-source setup. Do not copy Venmo branding, visual assets, source code, or protected trade dress.

The first release uses simulated balances and the stubbed external banking API; users do not link real bank accounts. Real-money movement requires a separate legal, compliance, security, reconciliation, and operational launch decision.

## Target architecture

```text
React + TypeScript web       Expo / React Native mobile
          \                     /
             ASP.NET Core Web API
                      |
                Payment Service
                      |
                    Ledger
                      |
                 PostgreSQL

Later: Redis for non-authoritative caching and rate limiting
Later: Kafka for committed events published from a PostgreSQL outbox
Optional: AI / risk analysis outside the ledger commit
```

- Both clients call the same versioned ASP.NET Core API. Publish an OpenAPI contract and generate or validate TypeScript clients from it.
- The Payment Service owns payment commands and state transitions. The Ledger owns balanced entries. Clients never write to the ledger or a provider directly.
- PostgreSQL is the system of record for payments, idempotency keys, ledger entries, and outbox records.
- Redis must never be authoritative for balances, payment status, or idempotency. Add it after a measured caching or rate-limit need appears.
- Kafka must never sit between a payment and its required ledger commit. Publish events after commit through the outbox; consumers must be idempotent.
- Keep the stub banking provider behind an interface. Its generated accounts are display-only test data, not verified funding sources.

### Core processing flow

```text
Payment request from web or mobile
  -> ASP.NET Core Web API: authenticate, authorize, validate, apply limits
  -> Payment Service: enforce idempotency and state transition
  -> Ledger: write balanced debit and credit entries
  -> PostgreSQL: commit payment, entries, and outbox record atomically
  -> Return the committed result to the client
  -> Later, publish outbox events to Kafka for notifications, analytics, and risk
```

The API may return a pending result when an external workflow is required. It must not report a payment as posted before the PostgreSQL transaction commits. A retry with the same scoped idempotency key returns the original result.

## Current state

Phases 0 and 1 are implemented locally. The ASP.NET Core API now provides PostgreSQL-backed Identity, browser-cookie and mobile-bearer sessions, CSRF/origin protection, session revocation, profiles with unique payment handles, private fixture activity, feature configuration, allowlisted client telemetry, consistent Problem Details, correlation IDs, rate limits, and authenticated ownership for sandbox banking data. EF Core owns the reviewed initial migration and explicit migration command.

The React and Expo clients use the same shared API client and generated OpenAPI contract. Both support registration, sign-in, profile editing, activity, sign-out, loading/error/empty states, and client error reporting. The fixture feed and all banking data remain explicitly simulated; payments, balances, ledger writes, and real-money movement are still unimplemented. Phase 2 is next.

### Repository layout

```text
api-server/          ASP.NET Core backend (Senzy.Api.csproj)
apps/web/            React + TypeScript web client
apps/mobile/         Expo / React Native mobile client
packages/contracts/  TypeScript contract definitions
infra/               Local infrastructure, including PostgreSQL
docs/                Product and engineering documentation
```

The root `package.json` provides workspace commands; pnpm manages the frontend and TypeScript packages, while .NET manages backend dependencies through `api-server/Senzy.Api.csproj`. `pnpm dev:api` starts the backend on port 4001, `pnpm dev:web` starts the web client on port 5173, and `pnpm dev:mobile` starts Expo. `pnpm check` checks the TypeScript workspace and builds the backend. `pnpm test` includes .NET integration tests. `pnpm contracts:generate` exports OpenAPI and generates TypeScript types; `pnpm contracts:check` verifies committed artifacts and runtime response samples.

## Non-functional requirements

- **Volume:** design and load-test for millions of transactions per day. Use 10 million/day as an initial planning workload (about 116 transactions/second on average) and test higher bursts; revise peak targets from measured traffic.
- **Correctness:** zero unbalanced posted transactions, duplicate postings, or acknowledged but uncommitted payments. Enforce invariants in application logic and PostgreSQL transaction boundaries.
- **Consistency:** payment state, idempotency result, ledger entries, and outbox record commit together. Read definitive payment and balance data from PostgreSQL.
- **Scalability:** keep API instances stateless for horizontal scaling. Measure database throughput and contention before adding caching, partitioning, replicas, or event infrastructure.
- **Reliability:** define p95/p99 latency, availability, recovery time, and reconciliation freshness targets before pilot. Test retries, failover, and backup restoration.
- **Observability:** propagate a correlation ID across requests, payment, ledger, outbox, and consumers. Record audit events, metrics, traces, and reconciliation results without leaking sensitive data.

## Delivery phases

### Phase 0 — foundation and decisions (implemented and locally verified)

- Keep the mobile shell, web preview, API health and readiness checks, and banking stub reproducible in the current layout.
- Record decisions for authentication, ASP.NET Core service boundaries, payment states, ledger posting, OpenAPI contracts, and the banking stub.
- Add CI for TypeScript, .NET, tests, mobile bundling, and secret scanning as each component arrives.
- Define the initial workload, peak factor, service-level objectives, and threat model.

Exit: architecture decisions and the contract workflow are agreed; the existing prototype remains reproducible and CI runs the required checks.

Evidence: [architecture decisions](ARCHITECTURE_DECISIONS.md), [contract workflow](../packages/contracts/README.md), [operating targets](OPERATING_TARGETS.md), [threat model](THREAT_MODEL.md), and [verification record](PHASE_0.md). Required checks have passed locally. The [GitHub Actions workflow](../.github/workflows/ci.yml) is configured; its first hosted run remains pending commit/push. Phase 1 is also complete; Phase 2 is next.

### Phase 1 — authenticated web and mobile prototype (implemented and locally verified)

- Completed: React + TypeScript preview in `apps/web` beside `apps/mobile`, and the ASP.NET Core API scaffold in `api-server`.
- Completed: liveness, PostgreSQL readiness, development-only OpenAPI, and development-only stub banking routes that reject raw bank account and routing number fields.
- Add authentication and authorization, consistent error responses, request tracing, and PostgreSQL schema and migration tooling to the API scaffold.
- Connect both clients to the same API contract and add automated contract and API tests.
- Implement sign-up, profiles, payment handles, and a fixture-backed read-only activity feed.
- Add accessible web and mobile navigation, analytics taxonomy, crash reporting, and feature flags.

Exit: both clients use the ASP.NET Core API for an authenticated non-money prototype; bank setup remains entirely stubbed.

Evidence: [Phase 1 verification](PHASE_1.md), [API architecture](../api-server/README.md), [shared contract workflow](../packages/contracts/README.md), the reviewed EF migration, PostgreSQL integration tests, and web/mobile production exports. A physical-device and browser visual smoke test remains a manual follow-up; automated contract, build, API, and container checks passed locally.

### Phase 2 — simulated payments and ledger

- Implement send and request flows with test balances, integer minor-unit amounts, and explicit currency.
- Define pending, posted, failed, and reversed payment states and legal transitions.
- In one PostgreSQL transaction, persist the payment state, scoped idempotency result, balanced ledger entries, and outbox record.
- Use database locking or concurrency control to prevent overspending and conflicting updates.
- Keep posted entries immutable; use linked reversal transactions for corrections.
- Add property, concurrency, retry, failure-injection, and integration tests for ledger invariants.

Exit: retries, concurrent sends, failures, and reversals cannot create duplicate or unbalanced postings.

### Phase 3 — simulated funding and operations

- Use the stub banking API for account selection and simulated cash-in/cash-out, clearly marked as sandbox data.
- Add transaction history, private-by-default activity, notifications, limits, freezes, and support/audit views.
- Reconcile payment states, ledger entries, and simulated provider records daily.
- Add a durable outbox worker and idempotent event handlers before introducing a broker.

Exit: end-to-end simulated payments reconcile with zero unexplained variance.

### Phase 4 — measured scale, Redis, and Kafka

- Load-test the API and PostgreSQL at the agreed daily volume and burst profile; inspect contention, connection pooling, indexes, and query plans.
- Scale stateless API instances horizontally. Add partitioning, read replicas, or archival only when measurements justify them.
- Introduce Redis for measured read-cache or rate-limit needs, with expiry and safe fallback when Redis is unavailable.
- Introduce Kafka for committed outbox events when worker throughput or consumer separation requires it. Track lag, retries, replay, and dead-letter handling.
- Keep notifications, analytics, and optional AI/risk consumers outside the ledger commit. If risk decisions become synchronous, define a strict timeout and explicit fail-open or fail-closed policy.

Exit: load, failure, and recovery tests meet the defined SLOs without violating payment or ledger invariants.

### Phase 5 — production controls and limited launch

- Obtain qualified legal and compliance guidance for KYC/CIP, sanctions, AML, licensing or sponsor-bank arrangements, PCI scope, privacy, disputes, and record retention as applicable.
- Complete independent security testing, incident response exercises, backup restore tests, and runbooks.
- Pilot internally, then roll out to capped cohorts with conservative limits and manual review paths.
- Monitor availability, latency, fraud, support volume, and daily reconciliation before expansion.

Exit: explicit production approval, stable cohort metrics, and reconciled financial records. Real funds remain out of scope until this gate is met.

## Ledger and payment rules

1. Store money as integer minor units plus an ISO currency; never use floating point.
2. Every posted payment creates balanced debit and credit entries in one PostgreSQL transaction.
3. Posted entries are immutable. Corrections use linked compensating transactions.
4. Scope and persist every payment command's idempotency key; a replay returns the original outcome.
5. Commit payment state, ledger entries, and outbox record atomically. Publish events only after commit.
6. Available balance is policy-controlled and distinct from settled ledger balance.
7. Consumers, webhooks, and outbox delivery may retry; processing must be idempotent.
8. Reconcile internal accounts against each simulated or real external settlement source every day.

## Near-term backlog

1. Completed in Phase 0: record ASP.NET Core service boundaries, authentication, and API contract decisions.
2. Completed in Phase 0: establish generated OpenAPI-to-TypeScript contracts shared by both workspaces; connect mobile screens as Phase 1 features arrive.
3. Completed in Phase 1: add PostgreSQL schema and explicit migration tooling under `api-server`.
4. Completed locally in Phase 0: automated health, readiness, and sandbox banking tests, including validation and concurrent one-time link-token behavior; checks and tests are wired into CI, awaiting the first hosted run.
5. Completed in Phase 1: add authentication, profiles, fixture-backed activity, telemetry, and feature flags to web and mobile.
6. Implement the payment state machine and transactional ledger posting.
7. Add concurrency, idempotency, and failure-injection tests.
8. Add simulated funding, reconciliation, and an outbox worker.
9. Establish the load-test workload and baseline before adding Redis or Kafka.

## Success measures

- **Product:** activation, verified-user rate, first request/send completion, weekly transacting users.
- **Correctness:** zero unbalanced postings, duplicate postings, and unexplained reconciliation variance.
- **Reliability:** successful posting rate, p95/p99 command latency, availability, recovery time, outbox lag, and reconciliation freshness.
- **Scale:** sustained transactions/day, burst transactions/second, database contention, and cost per transaction at the agreed workload.
- **Risk and operations:** fraud loss, account takeover, manual review precision, dispute rate, and support contacts per transaction when real-money operation is in scope.
