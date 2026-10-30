# 033 — Steady 102/sec boundary probe with PostgreSQL tuning

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [031-postgres-io-tuning.md](031-postgres-io-tuning.md) and [032-steady-105-postgres-tuned.md](032-steady-105-postgres-tuned.md)

## Change tested

The tuned PostgreSQL and application configuration was unchanged. Only the steady offered load was set to 102 iterations/sec. The workload used 256 synthetic sender/recipient pairs, a 45-second ramp, a 5-minute steady phase, and a 30-second ramp-down.

## Results

| Metric | 100/sec | 102/sec | 105/sec |
|---|---:|---:|---:|
| Completed iterations | 33,671 | 34,343 | 35,365 |
| Committed payments | 33,436 | 34,057 | 35,733 |
| HTTP failure rate | 0.23% | 0.27% | 0.13% |
| HTTP 429 responses | 235 | 286 | 144 |
| HTTP 500/503 responses | 0 | 0 | 0 |
| Payment p95 | 186 ms | 328 ms | 330 ms |
| Wallet read p95 | 20 ms | 26 ms | 27 ms |
| Operations status p95 | 34 ms | 42 ms | 50 ms |

The 102/sec run recorded 69,199 HTTP 200 responses, 34,569 HTTP 201 responses, and 286 HTTP 429 responses. Setup completed successfully for all 512 accounts.

## Threshold result

- HTTP failure rate: `0.27% < 1%` — passed
- Payment p95: `328 ms < 300 ms` — failed
- Wallet read p95: `26 ms < 200 ms` — passed
- Operations status p95: `42 ms < 200 ms` — passed

## Interpretation

The payment tail-latency cliff appears immediately above 100 iterations/sec: both 102/sec and 105/sec land near 330 ms p95, while 100/sec measured 186 ms. The reads remain healthy, so the limiting resource is still concentrated in the write/payment path rather than the shared read lane.

This iteration makes 100 iterations/sec the highest currently demonstrated sustained rate that satisfies all configured thresholds. A final 101/sec probe can check whether the cliff begins exactly at 101, but it is unlikely to support a higher SLO-compliant operating point without another write-path optimization.
