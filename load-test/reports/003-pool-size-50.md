# 003 — 50-connection pool with bounded database queue

Date: 2026-10-29 UTC  
Branch: `load-test-backend`  
Compared with: [002-connection-pool-queue.md](002-connection-pool-queue.md)

## Optimization

- `Minimum Pool Size=5`
- `Maximum Pool Size=50`
- `Timeout=5`
- `Command Timeout=30`
- `Keepalive=30`
- Database concurrency limit unchanged at 30 requests per API process
- Oldest-first database queue unchanged at 60 requests

Only the pool maximum changed from report 002. The stress profile and Docker Compose setup were unchanged.

## Stress profile

The profile was 50 → 100 → 200 → 400 iterations/sec with 100 maximum VUs over 1m50s.

| Metric | 002: pool 30 | 003: pool 50 | Change |
|---|---:|---:|---:|
| Completed iterations | 5,727 | 4,120 | -28.1% |
| Committed payments | 5,727 | 4,120 | -28.1% |
| HTTP 500/503 responses | 0 | 0 | unchanged |
| HTTP failure rate | 0% | 0.008% | +0.008 pp |
| Payment p95 | 1.66 s | 3.86 s | +132.5% |
| Payment p99 | 2.09 s | 5.63 s | +169.4% |
| Read p95 | 558 ms | 555 ms | -0.6% |
| Read p99 | 661 ms | 643 ms | -2.7% |
| Dropped iterations | — | 14,270 | recorded for this run |
| PostgreSQL connections after run | 36 | 56 | +20 |
| PostgreSQL CPU snapshot after run | 39.11% | 30.35% | -8.76 pp |

All 12,364 correctness checks passed. The single failed HTTP request was not a 500/503 response and did not cause a correctness-check failure.

## Result

Increasing the pool maximum to 50 was not an improvement under this workload. Because the database concurrency limiter remained at 30, the extra 20 pool slots did not increase the number of database operations allowed to execute concurrently. The run completed fewer iterations and had substantially worse payment tail latency, while read latency was effectively unchanged.

The 50-connection setting should not replace report 002's 30-connection setting based on this trial. The next useful experiment is to change the concurrency limit and queue together with the pool, while keeping total database concurrency below PostgreSQL and host capacity.
