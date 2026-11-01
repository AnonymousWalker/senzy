# 01 — Pool and database admission control

## Problem observed

The initial stress profile exhausted PostgreSQL connections: the baseline produced 56 HTTP 500/503 responses and `53300: too many clients already`. Payment p95 reached 6.87 seconds.

## Backend changes

- Create one shared singleton `NpgsqlDataSource` and pass it to EF Core.
- Configure the PostgreSQL pool with a minimum of 5 and maximum of 30 connections, 5-second acquisition timeout, 30-second command timeout, and 30-second keepalive.
- Add separate oldest-first bounded concurrency limiters for database reads and writes.
- Apply the read lane to wallet, status, history, list, and funding-read endpoints.
- Apply the write lane to payments, reversals, funding mutations, wallet changes, and reconciliation.
- Make benchmark payment/auth rate limits configurable without changing production defaults.

The retained lane configuration is 8 read permits, 22 write permits, and 60 queued requests per lane, with a 30-connection pool.

## Evidence

| Metric | Unbounded baseline | Pool + bounded admission |
|---|---:|---:|
| Completed iterations | 4,767 | 5,727 |
| HTTP 500/503 responses | 56 | 0 |
| Payment p95 | 6.87 s | 1.66 s |
| Read p95 | 27.6 ms | 558 ms |
| PostgreSQL connections after run | 69 | 36 |

Bounded admission removed connection exhaustion, but at extreme offered load it traded hard failures for queueing. The separate read/write lanes protected reads, while overly aggressive pool/permit increases did not help:

- Pool 50 with concurrency 30: payment p95 3.86 seconds.
- Pool 50 with concurrency 50: payment p95 3.68 seconds.
- The 10/20 lane split protected reads but rejected 26.9% of requests.
- The 8/22 split committed 24% more payments than 10/20 and reduced 429s.

## Conclusion

The important improvement was controlled database admission, not maximizing pool size. The final 8/22 and 60/60 configuration is retained because it balances payment throughput, read isolation, and bounded overload behavior.
