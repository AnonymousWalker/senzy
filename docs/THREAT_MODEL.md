# Initial threat model

Scope: the web/mobile prototype, ASP.NET API, PostgreSQL, CI, and the simulated banking adapter. This is an initial design review; controls marked planned must be implemented and verified in their delivery phase.

## Assets and trust boundaries

Protect identities, sessions, private activity, balances, immutable ledger records, idempotency outcomes, audit data, and build/deployment credentials. Web and mobile are untrusted clients. Boundaries are client-to-API, API-to-database, API-to-provider, outbox-to-consumer, and contributor-to-CI/deployment. A browser UI restriction is never authorization.

## Threats and controls

| Threat | Control and verification | Phase/status |
| --- | --- | --- |
| Client claims another user's identity or reads their accounts | Derive actor from authentication; check resource ownership in each use case; cross-user authorization tests. | Implemented and tested for current Phase 1–2 resources. Stub IDs are not authentication. |
| Password guessing, account enumeration, stolen sessions | Identity hashing/lockout, rate limits, non-enumerating recovery, short-lived credentials, protected mobile storage, tested logout/recovery; MFA before external pilot. | Phase 1 controls implemented; recovery delivery and pilot MFA remain planned. |
| CSRF or browser token theft | HTTP-only Secure cookies, antiforgery validation, origin policy; avoid unsafe HTML and token storage in local storage. | Implemented and tested for the current cookie client. |
| Duplicate sends or concurrent overspending | Durable scoped idempotency, request fingerprint checks, database uniqueness and locking; retry/concurrency/failure tests. | Implemented and tested against PostgreSQL in Phase 2. |
| Ledger manipulation or partial posting | Balanced immutable entries; one posting transaction; constrained database role; linked reversals and audit trail. | Posting, rollback, and reversals implemented and tested; constrained deployment database roles remain pre-pilot work. |
| Sandbox accidentally exposed publicly | Development-only route registration, production-route absence tests, deployment environment review. Development instances stay local/private. | Route guard and tests in Phase 0; deployment review ongoing. |
| Raw bank credentials or token replay | Reject unknown raw bank fields; bind link tokens to user, enforce expiry and one-time use; test malformed input and concurrent token reuse. | Stub control exists; baseline tests in Phase 0. |
| Forged/replayed provider callbacks | Verify signatures and timestamps, deduplicate provider IDs, reconcile settlement. Never trust callback status without validation. | Required before a real provider; out of prototype scope. |
| Secret leakage through source, logs, or builds | Secret scanning in CI; local settings ignored; no credentials in telemetry; short-lived scoped deployment credentials. | CI scanning in Phase 0; deployment controls later. |
| Dependency or CI compromise | Frozen frontend lockfile, pinned tool versions, minimal workflow permissions, no untrusted PR access to secrets, reviewed updates. | Phase 0 baseline; continuous review. |
| Denial of service or database outage | Bounded inputs/timeouts, rate limits, explicit backpressure, readiness distinct from liveness, measured pool limits. | Health checks in Phase 0; remaining controls Phase 1/4. |
| Lost data or silent settlement mismatch | Daily reconciliation compares ledger projections, payment/funding state, and durable sandbox-provider records; unresolved variance is explicit. PITR/backups, recovery drills, and an automated transfer pause remain pre-pilot work. | Phase 3 reconciliation implemented and tested; infrastructure recovery controls remain planned. |
| Duplicate or lost event delivery | Commit an outbox record with the financial transaction; lease rows with `SKIP LOCKED`, retry with backoff, and make handlers idempotent with a database uniqueness constraint. | Implemented and tested in Phase 3 for in-process notification delivery. |
| Cross-user funding, notification, or audit access | Derive the actor from authentication, validate stub-account ownership, and filter histories, notifications, controls, and funding records by the actor. Keep funding routes Development-only. | Implemented and tested in Phase 3. |
| Unauthorized outbound movement after a freeze or above a limit | Lock the wallet before policy evaluation, persist freeze state, and enforce the daily outbound limit for sends, accepted requests, reversals, and cash-out. | Implemented and tested in Phase 3; limits remain prototype policy, not a compliance control. |

## Review triggers

Revisit at authentication implementation, ledger design, external banking integration, public deployment, and any change to logging or data retention. The feature owner adds relevant abuse-case tests. Record unresolved findings in the product backlog; do not treat this document as proof that planned controls exist.

The sandbox cannot move money. Durable `SandboxProviderTransfer` rows are reconciliation fixtures, not bank confirmations or settled funds. No real bank linking, jurisdiction-specific launch approval, or production security certification is implied by completing the prototype phases.
