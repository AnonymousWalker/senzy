# 023 — Capacity probe through 110 iterations/sec with 256 pairs

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [022-account-pair-distribution-256.md](022-account-pair-distribution-256.md)

## Test setup

- 256 distributed sender/recipient pairs
- 30-connection PostgreSQL pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- Maximum k6 virtual users: 250
- Ramp: 75 → 100 → 110 iterations/sec, then ramp down

## Results

| Metric | Result |
|---|---:|
| Completed iterations | 13,977 |
| Committed payments | 13,574 |
| HTTP request rate | 151.4/sec |
| HTTP failure rate | 0.94% |
| HTTP 429 responses | 3,627 |
| HTTP 500/503 responses | 0 |
| Payment p95 | 342 ms |
| Payment p99 | 1,604 ms |
| Wallet read p95 | 33 ms |
| Operations status p95 | 51 ms |
| Dropped iterations | 130 |
| API CPU snapshot | 22.79% |
| PostgreSQL CPU snapshot | 37.74% |
| PostgreSQL connections after run | 36 |

The HTTP failure and read-latency thresholds passed. The payment p95 threshold failed narrowly.

## Result

With 256 account pairs, 110 iterations/sec is close to the sustainable target: reads remain healthy and HTTP failures are below 1%, but payment p95 is 42 ms above the 300 ms operating target. The passing boundary is therefore below 110/sec under this workload and configuration.

The next probe tests 105 iterations/sec to determine whether the payment p95 target can be met without changing the pool or concurrency split.
