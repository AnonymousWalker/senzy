# 037 — Latest optimization session summary

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Scope: Reports 031–036 and the associated implementation changes

## Executive summary

The session retained two optimizations:

1. The payment wallet-movement path now loads and locks both ledger accounts in one deterministic `SELECT ... FOR UPDATE` query.
2. PostgreSQL now uses larger shared buffers, a larger WAL budget, longer checkpoint intervals, and WAL compression while retaining `fsync=on` and `synchronous_commit=on`.

At the resulting configuration, 100–101 mixed iterations/sec passed all configured thresholds. Since each mixed iteration performs two reads and one payment, this corresponds to approximately 300–303 business requests/sec: 200–202 reads/sec and 100–101 writes/sec.

## Retained implementation changes

### Payment database round-trip reduction

`WalletMovementStore` previously selected ledger-account IDs and then issued a second query to lock and load the accounts. It now performs one deterministic query ordered by account ID with `FOR UPDATE`, preserving lock ordering and transaction durability while removing one database round trip per payment.

### PostgreSQL I/O configuration

The Docker PostgreSQL service now starts with:

```text
shared_buffers=1GB
max_wal_size=4GB
checkpoint_timeout=15min
wal_compression=on
synchronous_commit=on
fsync=on
```

### Load-test and reporting improvements

- Added long steady-state profiles at 100, 101, 102, and 105 iterations/sec.
- Kept 256 distributed account pairs to avoid a single hot wallet pair dominating results.
- Added separate wallet and operations-status latency metrics.
- Added corrected PostgreSQL wait sampling that excludes normal client socket waits.
- Added one report per iteration and indexed them in `load-test/REPORT.md`.

## Iteration results

| Report | Experiment | Payment p95 | Failure rate | HTTP 429s | Result |
|---|---|---:|---:|---:|---|
| 031 | PostgreSQL tuning at steady 100/sec | 186 ms | 0.23% | 235 | Accepted; all thresholds passed |
| 032 | Steady 105/sec | 330 ms | 0.13% | 144 | Rejected; payment target failed |
| 033 | Steady 102/sec | 328 ms | 0.27% | 286 | Rejected; payment target failed |
| 034 | Steady 101/sec | 208 ms | 0.17% | 177 | Accepted; all thresholds passed |
| 035 | 7 read / 23 write permits at 101/sec | 241 ms | 0.13% | 138 | Rejected; latency worsened |
| 036 | Write queue reduced from 60 to 40 at 101/sec | 249 ms | 0.36% | 371 | Rejected; latency and rejection worsened |

For all iterations, read p95 remained well below the 200 ms target and no HTTP 500/503 responses were observed.

## Final retained configuration

- PostgreSQL pool: 30 connections
- Read concurrency: 8
- Write concurrency: 22
- Read queue: 60
- Write queue: 60
- PostgreSQL I/O tuning listed above
- One-query deterministic wallet-account lock

The 7/23 lane split and 40-item write queue were reverted because they reduced neither payment tail latency nor overall system efficiency. The final Docker stack was recreated with the retained 8/22 and 60/60 settings and `/health/ready` returned `{"status":"ready"}`.

## Capacity conclusion

The current hardware and configuration demonstrated a sustained SLO-compliant boundary between 101 and 102 mixed iterations/sec. A conservative operating target is 100 iterations/sec, or approximately 300 mixed business requests/sec: 200 reads/sec and 100 payment writes/sec. This is a local Docker benchmark result, not a production capacity guarantee.

Detailed evidence remains in [031-postgres-io-tuning.md](031-postgres-io-tuning.md), [034-steady-101-postgres-tuned.md](034-steady-101-postgres-tuned.md), [035-lane-split-7-23.md](035-lane-split-7-23.md), and [036-write-queue-40.md](036-write-queue-40.md).
