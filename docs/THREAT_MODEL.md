# Threat model

Scope: the web/mobile prototype, ASP.NET API, PostgreSQL, CI, and the simulated banking adapter. This document records security requirements and verification status; planned controls are not evidence of production readiness.

## Assets and trust boundaries

Protect identities, sessions, private activity, balances, immutable ledger records, idempotency results, audit data, and build/deployment credentials. Web and mobile clients are untrusted. The important boundaries are client-to-API, API-to-database, API-to-provider, outbox-to-consumer, and contributor-to-CI/deployment. A browser restriction is never authorization.

## Threats and controls

| Threat | Control and current status |
| --- | --- |
| A caller reads or changes another user's data | Derive the actor from the authenticated principal and enforce ownership in each use case. Cross-user ownership tests cover current payments, funding, notifications, audit, history, and wallet controls. Client-supplied IDs are not authentication. |
| Password guessing, account enumeration, CSRF, or stolen sessions | Use password hashing/lockout, non-enumerating errors, rate limits, short-lived credentials, protected mobile refresh storage, Secure/HTTP-only cookies, antiforgery, and allowed-origin checks. Recovery delivery and pilot MFA remain planned. |
| DoS exhausts API, database, or worker capacity | Bound inputs and command timeouts; use auth/payment limits plus bounded, process-local database read/write concurrency queues and a capped PostgreSQL pool. Readiness is separate from liveness. The latest Docker/k6 runs found a conservative single-instance boundary of 100 mixed iterations/sec (synthetic local data), not a production SLA. Edge/global limits, alerting, and recovery drills remain planned. |
| Duplicate sends, concurrent spending, or a freeze/limit race | Use scoped idempotency with request fingerprints, database uniqueness, deterministic wallet row locks, and policy evaluation under the lock. PostgreSQL tests cover retry, rollback, concurrency, and freeze/limit behavior. The one-query locked account load and capacity probes are optimizations, not substitutes for correctness tests. |
| Ledger manipulation or partial financial posting | Only Ledger creates balanced postings; posted entries are immutable; corrections use linked compensating transactions. Business state, idempotency, ledger, and outbox records commit together. Constrained deployment database roles remain pre-pilot work. |
| Sandbox data is mistaken for real banking or exposed publicly | Banking is an in-memory Development-only stub; raw routing/account fields are rejected; link tokens are user-bound, expiring, and single-use. Development routes are absent from production and Compose binds the API to localhost. Durable provider-transfer rows are reconciliation fixtures, not settlement evidence. |
| Forged or replayed provider callbacks | Before a real provider, verify signatures and timestamps, deduplicate provider IDs, and reconcile settlement. This is out of prototype scope. |
| Secrets or private data leak through source, logs, telemetry, or builds | Run secret scanning, ignore local settings, use scoped short-lived deployment credentials, and never log tokens, passwords, raw bank details, unrestricted request bodies, or user IDs in metric labels. |
| Dependency, CI, or deployment compromise | Keep frontend dependencies locked, pin tool versions, minimize workflow permissions, avoid exposing secrets to untrusted pull requests, review updates, and apply migrations explicitly rather than from every API replica. |
| Lost data, outbox delivery, or reconciliation failure hides a financial mismatch | PostgreSQL remains authoritative. Commit outbox records with financial work; use leases, retries, consumer deduplication, and inspectable failures. Reconciliation compares ledger, payment/funding state, and sandbox-provider records; unresolved variance is explicit. Backups/PITR, restore drills, transfer pause, and authorized replay remain pre-pilot work. |
| A module or operator bypasses financial policy or ownership | Keep Payments/Funding as transaction owners, Wallets as the policy and locking boundary, and Ledger as the posting boundary. Administrative unfreeze, discrepancy resolution, delivery replay, and sensitive audit access need explicit role/resource authorization, re-authentication where appropriate, and actor/action/resource/outcome records. Current self-service controls are not an administrative authorization design. |
| Replica-local state causes token reuse, session inconsistency, or rate-limit evasion | Share protected Data Protection keys, preserve security-stamp revocation, trust forwarded headers only from configured proxies, and use distributed limits where limits must span replicas. Keep the sandbox single-instance until durable provider state and token semantics are verified. These multi-instance controls are planned. |

## Review triggers

Revisit this document when changing authentication, authorization, ledger behavior, provider integration, logging or retention, worker/outbox behavior, rate limits, or deployment exposure. Add abuse-case tests with the change and record unresolved findings in the backlog. Do not treat this document or local load-test results as approval for real-money use, public deployment, or a security certification.
