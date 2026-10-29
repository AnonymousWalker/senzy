# 002 — Connection pool and database queue optimization

Date: 2026-10-29 UTC  
Branch: `load-test-backend`  
Compared with: [001-baseline.md](001-baseline.md)

## Optimization

- One shared `NpgsqlDataSource` for EF Core and health checks
- `Minimum Pool Size=5`
- `Maximum Pool Size=30`
- `Timeout=5`
- `Command Timeout=30`
- `Keepalive=30`
- Database concurrency limit: 30 requests per API process
- Oldest-first queue: 60 requests

The database queue was applied to wallet, payment, funding, and operations routes. The original baseline report was preserved; this file records only the optimization trial.

## Stress profile

The profile was unchanged: 50 → 100 → 200 → 400 iterations/sec with 100 maximum VUs over 1m50s.

| Metric | Original stress | Optimized stress |
|---|---:|---:|
| Completed iterations | 4,767 | 5,727 |
| Committed payments | 4,767 | 5,727 |
| HTTP 500/503 responses | 56 | 0 |
| Payment p95 | 6.87 s | 1.66 s |
| Payment p99 | 11.24 s | 2.09 s |
| Read p95 | 27.6 ms | 558 ms |
| Read p99 | 41.9 ms | 661 ms |
| PostgreSQL connections after run | 69 | 36 |
| PostgreSQL CPU snapshot after run | 1.44% | 39.11% |

## Result

The optimization removed connection exhaustion and preserved all 5,727 payment/read correctness checks. It did not make the 400-iteration/sec profile meet the latency objectives: bounded queuing converted failures into controlled waiting, so payment and read p95 thresholds still failed at the highest offered load.

The next tuning candidates are queue/permit combinations of 20/40, 30/60, and 40/80. Each should be evaluated against payment latency, read latency, database CPU, connection count, and dropped iterations rather than maximizing pool size.
