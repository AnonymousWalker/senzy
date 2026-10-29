# 004 — 50-connection pool with 50/60 database queue

Date: 2026-10-29 UTC  
Branch: `load-test-backend`  
Compared with: [002-connection-pool-queue.md](002-connection-pool-queue.md)

## Optimization

- `Minimum Pool Size=5`
- `Maximum Pool Size=50`
- `Timeout=5`
- `Command Timeout=30`
- `Keepalive=30`
- Database concurrency limit increased from 30 to 50 requests per API process
- Oldest-first queue retained at 60 requests

This is the corrected paired trial: the pool maximum and database concurrency limit were increased together. The stress profile and Docker Compose setup were otherwise unchanged.

## Stress profile

The profile was 50 → 100 → 200 → 400 iterations/sec with 100 maximum VUs over 1m50s.

| Metric | 002: pool 30 / limit 30 | 004: pool 50 / limit 50 | Change |
|---|---:|---:|---:|
| Completed iterations | 5,727 | 4,327 | -24.4% |
| Committed payments | 5,727 | 4,327 | -24.4% |
| HTTP 500/503 responses | 0 | 0 | unchanged |
| HTTP failure rate | 0% | 0.008% | +0.008 pp |
| Payment p95 | 1.66 s | 3.68 s | +121.8% |
| Payment p99 | 2.09 s | 5.24 s | +150.7% |
| Read p95 | 558 ms | 581 ms | +4.1% |
| Read p99 | 661 ms | 695 ms | +5.1% |
| Dropped iterations | — | 14,062 | recorded for this run |
| PostgreSQL connections after run | 36 | 56 | +20 |
| PostgreSQL CPU snapshot after run | 39.11% | 27.86% | -11.25 pp |

All 12,985 correctness checks passed. The single failed HTTP request was not a 500/503 response and did not cause a correctness-check failure.

## Result

Pairing the pool increase with the concurrency increase was the correct experiment, but 50 concurrent database-bound requests was still too aggressive for this workload and hardware. It allowed more work to enter PostgreSQL at once, but the resulting contention increased payment tail latency and reduced the number of complete iterations under the fixed 100-VU load-generator cap.

The 30-connection pool and 30-request concurrency limit remains the better observed setting from these trials. The next useful tuning point is below 50—such as 40/40 with a bounded queue—followed by a repeat at a lower, controlled offered rate to separate capacity from latency saturation.
