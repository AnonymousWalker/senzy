# 001 — Baseline load test

Date: 2026-10-29 UTC  
Branch: `load-test-backend`  
Environment: Docker Compose, one API container, one PostgreSQL 17 container, synthetic data only

## Workload

Each iteration performed:

- `GET /v1/wallet/`
- `GET /v1/operations/status`
- `POST /v1/payments/` with a unique idempotency key and a 1-cent simulated USD payment

The test created two synthetic users, used bearer authentication, and ran k6 in Docker. The benchmark-only Compose override raised the process-local auth/payment rate limits to 100,000 requests/minute.

## Clean baseline run

Profile: ramped from 2 to 50 iterations/sec over 2m45s; 3,849 completed iterations.

| Metric | Result | Target/status |
|---|---:|---|
| Total HTTP requests | 11,552 | 3 requests/iteration |
| Committed payments | 3,849 | 23.3/sec averaged over the run |
| HTTP failures | 0 | Passed `<1%` threshold |
| Payment p95 / p99 | 22.5 ms / 83.9 ms | Passed `<1 s` p99 target |
| Read p95 / p99 | 9.9 ms / 13.7 ms | Passed `<500 ms` p99 target |
| Wallet checks | 3,849 passed | 0 failed |
| Status checks | 3,849 passed | 0 failed |
| Payment checks | 3,849 passed | 0 failed |

All baseline requests returned 200 or 201. PostgreSQL and the API remained healthy.

## Stress run

Profile: 50 → 100 → 200 → 400 iterations/sec, with 100 maximum VUs, over 1m50s.

| Metric | Result |
|---|---:|
| Total HTTP requests | 14,378 |
| Completed iterations | 4,767 |
| Dropped iterations | 13,599 |
| Committed payments | 4,767 |
| Payment p95 / p99 | 6.87 s / 11.24 s |
| Read p95 / p99 | 27.6 ms / 41.9 ms |
| HTTP failures | 83 requests, 0.58% |
| API responses | 56 total 500/503 responses |

The failing requests were associated with PostgreSQL error `53300: sorry, too many clients already`. PostgreSQL reported `max_connections=100` and 69 connections after the run, mostly idle pooled connections. The API and database containers were not CPU-bound in the post-run snapshot, so connection exhaustion was the first observed bottleneck.

## Baseline conclusion

The safe observed point was 50 mixed iterations/sec. This is not evidence that the application can sustain only that rate; the test generator was capped at 100 VUs and the database pool/max-connection configuration had not been sized deliberately.
