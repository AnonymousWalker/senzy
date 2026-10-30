# 009 — Ceiling probe capped at 50 iterations/sec

Date: 2026-10-29 UTC  
Branch: `load-test-backend`  
Compared with: [008-ceiling-probe-75.md](008-ceiling-probe-75.md)

## Test setup

The 30-connection pool and 8-read/22-write lanes were retained. The controlled profile was capped at 50 iterations/sec:

- 25 iterations/sec for 30 seconds
- 40 iterations/sec for 45 seconds
- 50 iterations/sec for 60 seconds
- Ramp down for 15 seconds
- 500 ms client backoff after any 429 response

## Results

| Metric | Result |
|---|---:|
| Completed iterations | 4,844 |
| Committed payments | 4,328 |
| HTTP requests | 14,537 |
| HTTP 200 responses | 9,691 |
| HTTP 201 responses | 4,330 |
| HTTP 429 responses | 516 |
| HTTP failure rate | 3.5% |
| Dropped iterations | 98 |
| Payment p95 | 2.93 s |
| Payment p99 | 3.57 s |
| Read p95 | 31 ms |
| Read p99 | 41 ms |
| PostgreSQL connections after run | 32 |
| PostgreSQL CPU snapshot after run | 2.72% |

Read latency passed its target. Payment latency and HTTP failure rate still failed the targets; no 500/503 responses were observed.

## Result

Reducing the cap from 75 to 50 iterations/sec reduced HTTP 429s from 1,337 to 516 and dropped iterations from 628 to 98, but it did not bring the system below the 1% failure target or the 300 ms payment p95 target.

The current 8/22 lane configuration therefore does not meet the payment objective even at the 50-iteration/sec probe. The next optimization should address payment execution time itself—especially transaction and lock contention—rather than only changing queue sizes or arrival rate.
