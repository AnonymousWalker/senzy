# 005 — Separate read and write database lanes

Date: 2026-10-29 UTC  
Branch: `load-test-backend`  
Compared with: [002-connection-pool-queue.md](002-connection-pool-queue.md)

## Optimization

- Kept `Maximum Pool Size=30`
- Replaced the shared 30-permit database limiter with two bounded lanes:
  - Reads: 10 active permits, 30 queued requests
  - Writes: 20 active permits, 40 queued requests
- Both queues use oldest-first processing
- Applied the read lane to wallet, operation status, transaction history, and list endpoints
- Applied the write lane to payments, payment requests, funding mutations, wallet freezes, and reconciliation

The total active database permit budget remains 30. This trial changes admission fairness rather than increasing PostgreSQL capacity.

## Stress profile

The profile was 50 → 100 → 200 → 400 iterations/sec with 100 maximum VUs over 1m50s.

| Metric | 002: shared 30/60 queue | 005: read 10 / write 20 lanes | Change |
|---|---:|---:|---:|
| Completed iterations | 5,727 | 11,969 | +109.0% |
| Committed payments | 5,727 | 2,313 | -59.6% |
| HTTP failure rate | 0% | 26.9% | +26.9 pp |
| Payment p95 | 1.66 s | 3.50 s | +110.8% |
| Payment p99 | 2.09 s | 5.30 s | +153.6% |
| Read p95 | 558 ms | 120 ms | -78.5% |
| Read p99 | 661 ms | 145 ms | -78.1% |
| Dropped iterations | — | 6,420 | recorded for this run |
| PostgreSQL connections after run | 36 | 36 | unchanged |
| PostgreSQL CPU snapshot after run | 39.11% | 1.80% | -37.31 pp |

The run recorded 9,656 failed correctness checks, corresponding to requests rejected during overload; API logs recorded 9,661 HTTP 429 responses. The committed-payment counter recorded 2,313 successful payment commits. Because the HTTP failure threshold was exceeded, this is not an acceptable production configuration as-is.

## Result

The lane split protected read latency very effectively: read p95 fell from 558 ms to 120 ms and read p99 from 661 ms to 145 ms. It allowed more iteration attempts to finish because read requests were no longer waiting behind the write queue, but most of the additional attempts did not result in committed payments.

However, the write lane’s queue capacity was too small for the offered stress load. Payment tail latency remained above target, and the system rejected 26.9% of HTTP requests with rate limiting. The result demonstrates that separate lanes are promising for read isolation, but the permit and queue allocation must be tuned against the desired overload behavior.

The next trial should retain the 30-connection pool and separate lanes, but allocate more write queue capacity or apply a lower, explicit write admission rate while preserving the read reservation. Compare 10/20, 12/18, and 15/15 lane splits at controlled offered rates before another maximum-load run.
