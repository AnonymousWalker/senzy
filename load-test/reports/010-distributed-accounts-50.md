# 010 — Distributed account pairs at 50 iterations/sec

Date: 2026-10-29 UTC  
Branch: `load-test-backend`  
Compared with: [009-ceiling-probe-50.md](009-ceiling-probe-50.md)

## Optimization

The backend configuration was unchanged:

- 30-connection pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- 500 ms client backoff after any 429 response

The load fixture changed from one sender/recipient pair to eight distributed sender/recipient pairs. Each virtual user consistently used one pair, removing the artificial single-wallet hotspot while preserving the same payment workflow and 50-iteration/sec ramp.

## Results

| Metric | 009: one hot pair | 010: eight distributed pairs | Change |
|---|---:|---:|---:|
| Completed iterations | 4,844 | 4,930 | +1.8% |
| Committed payments | 4,328 | 4,930 | +13.9% |
| HTTP failure rate | 3.5% | 0% | -3.5 pp |
| HTTP 429 responses | 516 | 0 | -100% |
| Payment p95 | 2.93 s | 42.6 ms | -98.5% |
| Payment p99 | 3.57 s | 103.8 ms | -97.1% |
| Read p95 | 31 ms | 15.9 ms | -48.6% |
| Read p99 | 41 ms | 21.3 ms | -48.0% |
| Dropped iterations | 98 | 12 | -87.8% |
| PostgreSQL connections after run | 32 | 26 | -6 |

All 14,822 correctness checks passed. Both latency thresholds and the HTTP failure threshold passed. No 500/503 or 429 responses occurred.

## Result

The same hardware, pool, lane limits, and 50-iteration/sec offered load met the measured targets when payments were distributed across eight account pairs. This isolates the previous bottleneck: all payments in reports 007–009 contended on the same two wallet rows, so PostgreSQL row locking serialized the payment workflow.

This does not prove that hot-wallet contention is solved. It shows that the current system can meet the target for distributed accounts at this tested rate. Future capacity tests should include both distributed traffic and an explicit hot-account mix, such as the operating target’s 20% of sends concentrated on 1% of accounts.
