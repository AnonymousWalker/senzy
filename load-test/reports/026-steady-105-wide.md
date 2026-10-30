# 026 — Steady-state validation at 105 iterations/sec

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [025-capacity-105-repeat.md](025-capacity-105-repeat.md)

## Test setup

- 256 distributed sender/recipient pairs
- 30-connection PostgreSQL pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- Maximum k6 virtual users: 250
- 45-second ramp to 105 iterations/sec
- 5-minute steady stage at 105 iterations/sec
- 30-second ramp down

## Results

| Metric | Result |
|---|---:|
| Completed iterations | 35,291 |
| Committed payments | 34,629 |
| HTTP request rate | 222.2/sec |
| HTTP failure rate | 0.67% |
| HTTP 429 responses | 6,471 |
| HTTP 500/503 responses | 0 |
| Payment p95 | 605 ms |
| Payment p99 | 1,114 ms |
| Wallet read p95 | 153 ms |
| Operations status p95 | 187 ms |
| Dropped iterations | 191 |
| API CPU snapshot | 25.16% |
| PostgreSQL CPU snapshot | 48.90% |
| PostgreSQL connections after run | 36 |

The HTTP failure and read-latency thresholds passed. The payment p95 threshold failed after the sustained stage.

## Result

105 iterations/sec is not a sustainable payment-latency target for the five-minute steady workload, despite passing two shorter ramp probes. The payment tail degraded from 294 ms in the repeat ramp to 605 ms under sustained load, indicating accumulating queue, lock, or transaction pressure over time.

The next validation lowers the steady state to 100 iterations/sec. A capacity point should not be considered sustainable unless the longer steady stage also passes.
