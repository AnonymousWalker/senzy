# 008 — Ceiling probe capped at 75 iterations/sec

Date: 2026-10-29 UTC  
Branch: `load-test-backend`  
Compared with: [007-controlled-offered-load.md](007-controlled-offered-load.md)

## Test setup

The 30-connection pool and 8-read/22-write lanes from report 006 were retained. The controlled profile was capped at 75 iterations/sec:

- 25 iterations/sec for 30 seconds
- 50 iterations/sec for 45 seconds
- 75 iterations/sec for 60 seconds
- Ramp down for 15 seconds
- 500 ms client backoff after any 429 response

## Results

| Metric | Result |
|---|---:|
| Completed iterations | 5,776 |
| Committed payments | 4,439 |
| HTTP requests | 17,333 |
| HTTP 200 responses | 11,555 |
| HTTP 201 responses | 4,441 |
| HTTP 429 responses | 1,337 |
| HTTP failure rate | 7.7% |
| Dropped iterations | 628 |
| Payment p95 | 3.03 s |
| Payment p99 | 3.68 s |
| Read p95 | 34 ms |
| Read p99 | 46 ms |
| PostgreSQL connections after run | 35 |
| PostgreSQL CPU snapshot after run | 6.80% |

The read latency threshold passed. The payment latency and HTTP failure thresholds failed. No 500/503 responses were observed; failures were HTTP 429 overload rejections.

## Result

The capped 75-iterations/sec profile is still above the sustainable write capacity for the current implementation. Read isolation remains effective, but payment p95 is about ten times the 300 ms target and 7.7% of requests were rejected.

The current measured target ceiling is therefore below 75 mixed iterations/sec. A 50-iterations/sec cap is the next probe needed to determine whether the payment p95 target and the under-1% failure target can be met without another architectural change.
