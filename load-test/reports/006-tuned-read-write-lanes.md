# 006 — Tuned read/write database lanes

Date: 2026-10-29 UTC  
Branch: `load-test-backend`  
Compared with: [005-separate-read-write-lanes.md](005-separate-read-write-lanes.md)

## Optimization

- Kept `Maximum Pool Size=30`
- Read lane: 8 active permits, 60 queued requests
- Write lane: 22 active permits, 60 queued requests
- Both queues use oldest-first processing

Compared with report 005, two active permits moved from reads to writes and both queue limits increased to 60.

## Stress profile

The profile was 50 → 100 → 200 → 400 iterations/sec with 100 maximum VUs over 1m50s.

| Metric | 005: read 10 / write 20 | 006: read 8 / write 22 | Change |
|---|---:|---:|---:|
| Completed iterations | 11,969 | 9,901 | -17.3% |
| Committed payments | 2,313 | 2,869 | +24.0% |
| HTTP failure rate | 26.9% | 23.7% | -3.2 pp |
| HTTP 429 responses | 9,661 | 7,041 | -27.1% |
| Payment p95 | 3.50 s | 3.92 s | +12.1% |
| Payment p99 | 5.30 s | 5.00 s | -5.7% |
| Read p95 | 120 ms | 51 ms | -57.5% |
| Read p99 | 145 ms | 67 ms | -53.8% |
| Dropped iterations | 6,420 | 8,489 | +32.2% |
| PostgreSQL connections after run | 36 | 36 | unchanged |
| PostgreSQL CPU snapshot after run | 1.80% | 0.09% | -1.71 pp |

The run recorded 7,032 failed correctness checks and 2,869 committed payments. The HTTP failure threshold and payment latency threshold still failed; the read latency threshold passed.

## Result

Moving permits toward writes improved the number of committed payments by 24% and reduced observed HTTP 429s by 27%, but it did not make the stress profile healthy. Payment p95 remained about 3.9 seconds and 23.7% of HTTP requests still failed under the 400-iteration/sec stage. Read latency improved substantially because eight dedicated read permits were still sufficient for this workload.

This is a better tradeoff than report 005 for payment completion, but it remains an overload configuration rather than a production-ready setting. The next tuning step should reduce offered load or add explicit client-side backoff; increasing queues further would mostly trade 429s for longer payment latency.
