# Senzy modular-monolith refactor plan

This is the execution checklist for the approved design in [BACKEND_ARCHITECTURE.md](BACKEND_ARCHITECTURE.md). Each step must preserve the existing `/v1` contracts and keep one ASP.NET Core deployment, one PostgreSQL system of record, and synchronous in-process module calls.

## Twelve major steps

1. **Extract Wallets persistence adapters** — isolate account lookup, locking, wallet controls, and movement persistence behind Wallets-owned interfaces.
2. **Extract Ledger persistence adapters** — isolate ledger accounts, postings, entries, and balance projections behind Ledger-owned interfaces.
3. **Move Wallets and Ledger implementations** — move their concrete services out of `Senzy.Api` while preserving the shared transaction.
4. **Extract Payments** — move payment workflows into `Senzy.Modules.Payments` with idempotency and commit ownership intact.
5. **Extract Funding** — move funding workflows into `Senzy.Modules.Funding` with provider calls and financial transactions correctly separated.
6. **Enforce module dependencies** — reject forbidden project references, circular dependencies, and direct cross-module persistence writes.
7. **Harden outbox delivery** — complete consumer-specific retry, lease recovery, deduplication, replay, retention, and observability.
8. **Finish logical ownership** — keep Operations as a compatibility surface while removing alternate write paths from Reporting and Reconciliation.
9. **Extract shared infrastructure** — move EF persistence, migrations, provider adapters, and outbox mechanics into `Senzy.Infrastructure`.
10. **Harden replica operation** — complete worker coordination, shared keys, trusted proxies, distributed rate limiting, shutdown, and health reporting.
11. **Validate scale and recovery** — measure pool usage, lock contention, throughput, crash recovery, database restart, backup restore, and reconciliation.
12. **Complete acceptance and rollout checks** — run builds, contract checks, HTTP tests, PostgreSQL integration tests, migration review, and readiness validation.

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
- [x] Add Wallets-owned persistence adapters for account lookup, locking, controls, balance reads, and movement persistence.
- [x] Add Ledger-owned persistence adapters for accounts, postings, entries, and balance projections.
- [ ] Move Wallets and Ledger implementations out of `Senzy.Api`.
- [ ] Verify one PostgreSQL transaction across Payments/Funding, Wallets, Ledger, idempotency, and outbox.

## Payments and Funding assemblies

- [x] Extract Payments application workflows into `Senzy.Modules.Payments`.
- [x] Extract Funding application workflows into `Senzy.Modules.Funding`.
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
- [x] Extract `Senzy.Infrastructure` for EF persistence, migrations, and the sandbox banking provider; outbox mechanics remain host-owned until the Notifications handler contract moves.
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

## Current extraction-wave status

- **Step 1:** Partially complete. Wallet policy and wallet account lookup/locking are behind `IWalletPolicyStore` and `IWalletMovementStore`; movement persistence remains coupled to Ledger's shared transaction adapter.
- **Step 2:** Complete for the current slice. Ledger posting uses `ILedgerPostingStore` and the EF adapter remains in infrastructure.
- **Step 3:** Partially complete. Ledger implementation and Wallets movement coordination are in their module assemblies; thin host adapters remain for EF transaction integration and HTTP error translation.
- **Infrastructure unblock:** `Senzy.Infrastructure` now exposes `ITransactionContext`; the host provides the EF/PostgreSQL implementation. Payments and Funding can depend on the transaction contract without referencing `Senzy.Api`.
- **Infrastructure extraction:** EF persistence models, `SenzyDbContext`, migrations, transaction adapter, Wallets/Ledger stores, and the sandbox banking provider now compile in `Senzy.Infrastructure`; API remains the composition root and retains only host-specific Outbox mechanics.
- **Provider adapter extraction:** The sandbox banking contract and provider now compile in `Senzy.Infrastructure`; the API retains only the `/v1` route composition surface.
- **Step 4:** Complete for the current assembly slice. Payment application workflow, state transitions, idempotency coordination, and response result types are in `Senzy.Modules.Payments`; API routes map HTTP DTOs to module commands/results.
- **Step 5:** Complete for the current assembly slice. Funding application workflow, idempotency coordination, provider boundary, and response result types are in `Senzy.Modules.Funding`; API routes map HTTP DTOs to module commands/results.
