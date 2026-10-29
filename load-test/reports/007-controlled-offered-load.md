# 007 — Controlled offered load with client backoff

Date: 2026-10-29 UTC  
Branch: `load-test-backend`  
Compared with: [006-tuned-read-write-lanes.md](006-tuned-read-write-lanes.md)

## Test setup

The 30-connection pool and 8-read/22-write lane configuration from report 006 were retained. The load generator was changed from the 400-iteration/sec stress ramp to a controlled ramp:

- 25 iterations/sec for 30 seconds
- 50 iterations/sec for 45 seconds
- 75 iterations/sec for 45 seconds
- 100 iterations/sec for 45 seconds
- Ramp down for 15 seconds
- Client sleeps 500 ms after any 429 response; otherwise sleeps 100 ms

## Results

| Metric | Result |
|---|---:|
| Completed iterations | 7,372 |
| Committed payments | 5,151 |
| HTTP requests | 22,121 |
| HTTP 200 responses | 14,747 |
| HTTP 201 responses | 5,153 |
| HTTP 429 responses | 2,221 |
| HTTP failure rate | 10.0% |
| Dropped iterations | 2,220 |
| Payment p95 | 3.33 s |
| Payment p99 | 4.06 s |
| Read p95 | 43 ms |
| Read p99 | 62 ms |
| PostgreSQL connections after run | 36 |
| PostgreSQL CPU snapshot after run | 2.42% |

The run produced no 500/503 responses. Read latency passed its p95 threshold; payment latency and overall HTTP failure thresholds did not. The 2,221 failed checks corresponded to the 2,221 HTTP 429 responses.

## Result

Client backoff and a lower offered-load ramp removed server errors and kept reads well within target, but the 100-iteration/sec stage still exceeded the write lane’s sustainable admission rate. The 10% failure rate is concentrated in overload rejection rather than database connection exhaustion.

This gives a clearer capacity boundary than the full stress test: the current 30-connection, 8/22 lane configuration is stable in error type and read latency, but not yet healthy at the top of this controlled ramp. The next measurement should cap the ramp at 75 iterations/sec, or run separate payment-only and read-only profiles, to identify the highest rate that keeps HTTP failures below 1%.
