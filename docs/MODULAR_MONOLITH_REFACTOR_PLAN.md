# Senzy modular-monolith refactor plan

This is the execution checklist for the approved design in [BACKEND_ARCHITECTURE.md](BACKEND_ARCHITECTURE.md). Each step must preserve the existing `/v1` contracts and keep one ASP.NET Core deployment, one PostgreSQL system of record, and synchronous in-process module calls.

## Baseline and enforcement

- [x] Characterize endpoint responses, lock ordering, idempotency behavior, and ownership rules.
- [x] Add initial architecture tests for selected scale-critical contracts.
- [ ] Reject forbidden project references and circular module dependencies at build time.
- [ ] Add direct cross-module persistence-write checks for every owned record set.
- [ ] Add the full PostgreSQL concurrency and rollback test harness.

## Financial boundaries

- [x] Keep Payments and Funding as transaction owners.
- [x] Extract Ledger and Wallets contract assemblies.
- [x] Move Ledger invariant planning into `Senzy.Modules.Ledger`.
- [x] Move Wallet freeze and daily-limit decisions into `Senzy.Modules.Wallets`.
- [ ] Add Wallets-owned persistence adapters for account lookup, locking, controls, and movement persistence.
- [ ] Add Ledger-owned persistence adapters for accounts, postings, entries, and balance projections.
- [ ] Move Wallets and Ledger implementations out of `Senzy.Api`.
- [ ] Verify one PostgreSQL transaction across Payments/Funding, Wallets, Ledger, idempotency, and outbox.

## Payments and Funding assemblies

- [ ] Extract Payments application workflows into `Senzy.Modules.Payments`.
- [ ] Extract Funding application workflows into `Senzy.Modules.Funding`.
- [ ] Keep idempotency, business state, outbox creation, and commit ownership inside the initiating module.
- [ ] Remove direct access from Payments and Funding to Wallets/Ledger persistence records.

## Outbox and asynchronous work

- [x] Add immutable versioned event envelopes.
- [x] Add per-consumer delivery state, leases, retries, and uniqueness constraints.
- [x] Move notification handling into Notifications.
- [ ] Test independent consumer delivery, duplicate delivery, lease expiry, backoff, retention, replay, and deduplication.
- [ ] Add delivery lag, retry, lease, and failed-message observability.

## Logical ownership and infrastructure

- [x] Keep Operations as the compatibility endpoint surface.
- [x] Establish logical Reporting and Reconciliation ownership.
- [ ] Remove remaining alternate cross-module write paths.
- [ ] Extract `Senzy.Infrastructure` for EF persistence, migrations, provider adapters, and outbox mechanics.
- [ ] Keep module implementations and persistence mappings internal to their assemblies.
- [ ] Add module registration methods at the composition root.

## Replica and worker hardening

- [x] Add API-only, worker-only, and combined worker roles.
- [x] Coordinate reconciliation with a PostgreSQL advisory lease.
- [ ] Coordinate all scheduled runs with durable lease and unique-run semantics.
- [ ] Share Data Protection keys across replicas and verify session movement.
- [ ] Configure trusted proxy handling.
- [ ] Define distributed or edge rate limiting for limits that must span replicas.
- [ ] Add graceful worker shutdown and health reporting.
- [ ] Measure connection-pool usage, lock waits, hot-wallet contention, and outbox freshness.
- [ ] Make sandbox banking state durable or enforce its single-instance development-only boundary.

## Recovery and acceptance

- [ ] Test uncertain commit recovery and duplicate idempotency replay.
- [ ] Test worker crash, lease takeover, database restart, and duplicate outbox delivery.
- [ ] Exercise backup/restore and post-restore reconciliation.
- [ ] Validate rolling deployment and migration compatibility.
- [ ] Run the full build, contract checks, HTTP tests, PostgreSQL integration tests, migration review, and `/health/ready` validation.
- [ ] Record measured operating results separately from aspirational targets.

## Execution order

1. Complete Wallets and Ledger persistence adapters.
2. Move Wallets and Ledger implementations into their assemblies.
3. Extract Payments and Funding assemblies.
4. Complete architecture dependency enforcement and remove direct persistence writes.
5. Harden outbox consumers and observability.
6. Finish replica, worker, and sandbox-state hardening.
7. Run concurrency, recovery, restore, and acceptance validation.
