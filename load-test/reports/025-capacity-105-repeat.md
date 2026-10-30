# 025 — Repeat passing capacity probe through 105 iterations/sec

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [024-capacity-105-wide.md](024-capacity-105-wide.md)

## Test setup

- 256 distributed sender/recipient pairs
- 30-connection PostgreSQL pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- Maximum k6 virtual users: 250
- Ramp: 75 → 100 → 105 iterations/sec, then ramp down

## Results

| Metric | First 105/sec run | Repeat |
|---|---:|---:|
| Completed iterations | 13,788 | 13,779 |
| Committed payments | 13,732 | 13,557 |
| HTTP failure rate | 0.13% | 0.52% |
| HTTP 429 responses | 504 | 1,998 |
| HTTP 500/503 responses | 0 | 0 |
| Payment p95 | 284 ms | 294 ms |
| Wallet read p95 | 35 ms | 29 ms |
| Operations status p95 | 55 ms | 45 ms |

Both runs passed the operating thresholds: HTTP failures below 1%, payment p95 below 300 ms, and read p95 below 200 ms.

## Result

105 mixed iterations/sec is reproducible under the current 30-connection pool, 8/22 concurrency split, and 256-pair distributed workload. It remains close to the payment-latency boundary, so the next validation uses a longer steady-state stage rather than increasing offered load immediately.
