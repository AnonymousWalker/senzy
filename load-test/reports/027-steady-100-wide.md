# 027 — Steady-state validation at 100 iterations/sec

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [026-steady-105-wide.md](026-steady-105-wide.md)

## Test setup

- 256 distributed sender/recipient pairs
- 30-connection PostgreSQL pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- Maximum k6 virtual users: 250
- 45-second ramp to 100 iterations/sec
- 5-minute steady stage at 100 iterations/sec
- 30-second ramp down

## Results

| Metric | Result |
|---|---:|
| Completed iterations | 33,643 |
| Committed payments | 33,439 |
| HTTP request rate | 211.1/sec |
| HTTP failure rate | 0.20% |
| HTTP 429 responses | 1,836 |
| HTTP 500/503 responses | 0 |
| Payment p95 | 472 ms |
| Payment p99 | 867 ms |
| Wallet read p95 | 79 ms |
| Operations status p95 | 115 ms |
| Dropped iterations | 151 |
| API CPU snapshot | 26.16% |
| PostgreSQL CPU snapshot | 55.85% |
| PostgreSQL connections after run | 36 |

The HTTP failure and read-latency thresholds passed. The payment p95 threshold failed.

## Result

100 iterations/sec also failed the five-minute payment-latency target, despite very low HTTP failure rate and healthy reads. This shows that the earlier short 100/sec pass does not represent a sustained payment capacity point. The next step is to sample lock and server-wait behavior during this longer run, with particular attention to payment transaction hold time.
