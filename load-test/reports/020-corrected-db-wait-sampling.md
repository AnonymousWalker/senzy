# 020 — Corrected mixed-workload PostgreSQL wait sampling

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [019-mixed-db-wait-sampling.md](019-mixed-db-wait-sampling.md)

## Test setup

- Same mixed 100 → 125 → 150 iterations/sec probe
- 30-connection PostgreSQL pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- 32 distributed sender/recipient pairs
- Corrected PostgreSQL sampler: server-side waits exclude `ClientRead` and `ClientWrite`
- 83 samples collected at two-second intervals

## PostgreSQL observations

| Metric | Average | Maximum |
|---|---:|---:|
| Active client backends | 5.41 | 25 |
| Server-side waiting backends | 1.77 | 23 |
| Lock-waiting backends | 0.72 | 8 |
| I/O-waiting backends | 0.39 | 6 |
| Ungranted locks | 0.69 | 57 |
| Longest active query observed | — | 1.002 sec |

## Application results

| Metric | Result |
|---|---:|
| Completed iterations | 18,194 |
| Completed iteration rate | 94.0/sec |
| Committed payments | 16,628 |
| HTTP failure rate | 10.48% |
| HTTP 429 responses | 51,624 |
| HTTP 500/503 responses | 0 |
| Payment p95 | 794 ms |
| Wallet read p95 | 420 ms |
| Operations status p95 | 473 ms |
| API CPU snapshot | 31.82% |
| PostgreSQL CPU snapshot | 56.90% |

## Result

The corrected sampling shows that average server-side waits are modest, but lock pressure can spike sharply: up to 57 ungranted locks and 8 lock-waiting backends. The mixed workload still misses all operating latency and failure targets.

The next experiment increases the synthetic account-pair count without changing the pool, lanes, or offered-load ramp. This isolates row-lock contention caused by multiple high-concurrency users sharing the same sender/recipient wallets.
