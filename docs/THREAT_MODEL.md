# Initial threat model

Scope: the web/mobile prototype, ASP.NET API, PostgreSQL, CI, and the simulated banking adapter. This is an initial design review; controls marked planned must be implemented and verified in their delivery phase.

## Assets and trust boundaries

Protect identities, sessions, private activity, balances, immutable ledger records, idempotency outcomes, audit data, and build/deployment credentials. Web and mobile are untrusted clients. Boundaries are client-to-API, API-to-database, API-to-provider, outbox-to-consumer, and contributor-to-CI/deployment. A browser UI restriction is never authorization.

## Threats and controls

| Threat | Control and verification | Phase/status |
| --- | --- | --- |
| Client claims another user's identity or reads their accounts | Derive actor from authentication; check resource ownership in each use case; cross-user authorization tests. | Phase 1 planned. Stub IDs are not authentication. |
| Password guessing, account enumeration, stolen sessions | Identity hashing/lockout, rate limits, non-enumerating recovery, short-lived credentials, protected mobile storage, tested logout/recovery; MFA before external pilot. | Phase 1 and pilot controls planned. |
| CSRF or browser token theft | HTTP-only Secure cookies, antiforgery validation, origin policy; avoid unsafe HTML and token storage in local storage. | Phase 1 planned. |
| Duplicate sends or concurrent overspending | Durable scoped idempotency, request fingerprint checks, database uniqueness and locking; retry/concurrency/failure tests. | Phase 2 planned. |
| Ledger manipulation or partial posting | Balanced immutable entries; one posting transaction; constrained database role; linked reversals and audit trail. | Phase 2 planned. |
| Sandbox accidentally exposed publicly | Development-only route registration, production-route absence tests, deployment environment review. Development instances stay local/private. | Route guard and tests in Phase 0; deployment review ongoing. |
| Raw bank credentials or token replay | Reject unknown raw bank fields; bind link tokens to user, enforce expiry and one-time use; test malformed input and concurrent token reuse. | Stub control exists; baseline tests in Phase 0. |
| Forged/replayed provider callbacks | Verify signatures and timestamps, deduplicate provider IDs, reconcile settlement. Never trust callback status without validation. | Required before a real provider; out of prototype scope. |
| Secret leakage through source, logs, or builds | Secret scanning in CI; local settings ignored; no credentials in telemetry; short-lived scoped deployment credentials. | CI scanning in Phase 0; deployment controls later. |
| Dependency or CI compromise | Frozen frontend lockfile, pinned tool versions, minimal workflow permissions, no untrusted PR access to secrets, reviewed updates. | Phase 0 baseline; continuous review. |
| Denial of service or database outage | Bounded inputs/timeouts, rate limits, explicit backpressure, readiness distinct from liveness, measured pool limits. | Health checks in Phase 0; remaining controls Phase 1/4. |
| Lost data or silent settlement mismatch | PITR/backups, recovery drills, daily reconciliation, pause transfers on unresolved inconsistency. | Phase 3–5 planned. |

## Review triggers

Revisit at authentication implementation, ledger design, external banking integration, public deployment, and any change to logging or data retention. The feature owner adds relevant abuse-case tests. Record unresolved findings in the product backlog; do not treat this document as proof that planned controls exist.

The sandbox cannot move money. No real bank linking, jurisdiction-specific launch approval, or production security certification is implied by Phase 0 completion.
