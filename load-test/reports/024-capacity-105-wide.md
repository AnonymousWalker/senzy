# 024 — Passing capacity probe through 105 iterations/sec

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [023-capacity-110-wide.md](023-capacity-110-wide.md)

## Test setup

- 256 distributed sender/recipient pairs
- 30-connection PostgreSQL pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- Maximum k6 virtual users: 250
- Ramp: 75 → 100 → 105 iterations/sec, then ramp down

## Results

| Metric | Result |
|---|---:|
| Completed iterations | 13,788 |
| Committed payments | 13,732 |
| HTTP request rate | 148.0/sec |
| HTTP failure rate | 0.13% |
| HTTP 429 responses | 504 |
| HTTP 500/503 responses | 0 |
| Payment p95 | 284 ms |
| Payment p99 | 840 ms |
| Wallet read p95 | 35 ms |
| Operations status p95 | 55 ms |
| Dropped iterations | 131 |
| API CPU snapshot | 30.66% |
| PostgreSQL CPU snapshot | 61.14% |
| PostgreSQL connections after run | 36 |

All operating thresholds passed: HTTP failures below 1%, payment p95 below 300 ms, and both read p95 values below 200 ms. There were no 500 or 503 responses.

## Result

105 iterations/sec is the first passing point immediately below the failed 110/sec probe. The observed boundary is currently between 105 and 110 mixed iterations/sec with 256 distributed account pairs, the 30-connection pool, and the 8/22 concurrency split.

A repeat 105/sec run is needed before treating this as a stable capacity point.
