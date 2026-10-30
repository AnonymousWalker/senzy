# 017 — Read-lane isolation through 150 iterations/sec

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [016-read-path-diagnostics.md](016-read-path-diagnostics.md)

## Test setup

- Read-only workload: wallet and operations-status requests, no payments
- 30-connection PostgreSQL pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- 32 distributed sender/recipient pairs
- Maximum k6 virtual users: 250
- Ramp: 100 → 125 → 150 iterations/sec, then ramp down

## Results

| Metric | Result |
|---|---:|
| Completed iterations | 18,408 |
| Completed iteration rate | 95.7/sec |
| HTTP request rate | 192.1/sec |
| HTTP failure rate | 0% |
| HTTP 429/500/503 responses | 0 |
| Wallet read p95 | 8 ms |
| Operations status p95 | 13 ms |
| Combined read p95 | 12 ms |
| Dropped iterations | 12 |
| API CPU snapshot | 0.55% |
| PostgreSQL CPU snapshot | 16.83% |
| PostgreSQL connections after run | 20 |

All 36,944 correctness checks passed. Read latency and HTTP failure thresholds passed.

## Result

The read endpoints are healthy in isolation at the same offered-load ramp. The mixed-workload read latency of 526–592 ms is therefore caused by interaction with the payment workload and shared database/concurrency resources, not by an intrinsically slow read query at this dataset size.

This rules out increasing the read permit count as the first optimization. The next isolation run tests the write lane alone.
