# 016 — Read-path diagnostics at the 150 iterations/sec boundary

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [014-capacity-150.md](014-capacity-150.md)

## Test setup

- 30-connection PostgreSQL pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- 32 distributed sender/recipient pairs
- Maximum k6 virtual users: 250
- Ramp: 100 → 125 → 150 iterations/sec, then ramp down
- Separate wallet-read and operations-status latency metrics
- Operating thresholds enforced: payment p95 ≤300 ms, each read p95 ≤200 ms, HTTP failures <1%

## Results

| Metric | Result |
|---|---:|
| Completed iterations | 18,178 |
| Completed iteration rate | 91.8/sec |
| Committed payments | 15,590 |
| Dropped iterations | 242 |
| HTTP request rate | 276.2/sec |
| HTTP failure rate | 20.74% |
| HTTP 429 responses | 102,051 |
| HTTP 500/503 responses | 0 |
| Payment p95 | 973 ms |
| Combined read p95 | 562 ms |
| Wallet read p95 | 526 ms |
| Operations status p95 | 592 ms |
| PostgreSQL CPU snapshot | 49.64% |
| API CPU snapshot | 39.15% |
| PostgreSQL connections after run | 36 |

All operating thresholds failed at this load level. The failures were bounded 429 responses; no 500 or 503 responses occurred.

## Findings

The operations-status endpoint is about 12.5% slower than the wallet endpoint at p95, but both are slow in the same run. This points to shared read-lane queueing or database contention as the primary issue rather than one isolated status query.

Spot `EXPLAIN (ANALYZE, BUFFERS)` checks on the current local dataset showed indexed execution for the wallet-control, daily-usage, and latest-reconciliation queries, with isolated execution times below 1 ms. These checks are not a substitute for a large-dataset or concurrent measurement, but they do not justify adding indexes or changing the pool yet.

## Decision

Do not change the 30/8/22 configuration based on this run. The next experiment should isolate read-only and payment-only workloads while recording limiter queue wait, database pool wait, lock wait, and query duration. That will distinguish admission queue saturation from database contention before another concurrency change is attempted.
