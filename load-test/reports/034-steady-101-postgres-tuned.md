# 034 — Steady 101/sec boundary probe with PostgreSQL tuning

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [031-postgres-io-tuning.md](031-postgres-io-tuning.md), [032-steady-105-postgres-tuned.md](032-steady-105-postgres-tuned.md), and [033-steady-102-postgres-tuned.md](033-steady-102-postgres-tuned.md)

## Change tested

The tuned PostgreSQL and application configuration was unchanged. The steady offered load was set to 101 iterations/sec, with 256 synthetic sender/recipient pairs, a 45-second ramp, a 5-minute steady phase, and a 30-second ramp-down.

## Results

| Metric | 100/sec | 101/sec | 102/sec | 105/sec |
|---|---:|---:|---:|---:|
| Completed iterations | 33,671 | 34,014 | 34,343 | 35,365 |
| Committed payments | 33,436 | 33,837 | 34,057 | 35,733 |
| HTTP failure rate | 0.23% | 0.17% | 0.27% | 0.13% |
| HTTP 429 responses | 235 | 177 | 286 | 144 |
| HTTP 500/503 responses | 0 | 0 | 0 | 0 |
| Payment p95 | 186 ms | 208 ms | 328 ms | 330 ms |
| Wallet read p95 | 20 ms | 22 ms | 26 ms | 27 ms |
| Operations status p95 | 34 ms | 38 ms | 42 ms | 50 ms |

The 101/sec run recorded 68,541 HTTP 200 responses, 34,349 HTTP 201 responses, and 177 HTTP 429 responses. Setup completed successfully for all 512 accounts.

## Threshold result

The run passed all configured thresholds:

- HTTP failure rate: `0.17% < 1%`
- Payment p95: `208 ms < 300 ms`
- Wallet read p95: `22 ms < 200 ms`
- Operations status p95: `38 ms < 200 ms`

## Interpretation

101 iterations/sec is currently the highest demonstrated steady rate that satisfies every operating target. The next step is a small read/write lane adjustment at this passing rate. Because reads have substantial latency headroom and the earlier 6/24 experiment regressed, 7 read / 23 write permits is a safer intermediate test than another two-permit shift.
