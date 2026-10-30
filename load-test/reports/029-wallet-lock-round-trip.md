# 029 — Payment wallet-lock round-trip reduction

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [028-steady-100-db-wait-sampling.md](028-steady-100-db-wait-sampling.md)

## Change tested

The payment wallet movement path now selects and locks both ledger accounts in one deterministic `SELECT ... FOR UPDATE` query. Previously it first selected both account IDs, then issued a second query to lock and load them. The 30-connection pool, 8/22 lanes, queues, pair distribution, and load profile were unchanged.

## Results

| Metric | Prior | Optimized |
|---|---:|---:|
| Completed iterations | 33,585 | 33,669 |
| Committed payments | 33,310 | 33,249 |
| HTTP failure rate | 0.43% | 0.41% |
| HTTP 429 responses | 3,942 | 3,780 |
| HTTP 500/503 responses | 0 | 0 |
| Payment p95 | 444 ms | 376 ms |
| Wallet read p95 | 40 ms | 34 ms |
| Operations status p95 | 63 ms | 56 ms |
| API CPU snapshot | 28.54% | 26.37% |
| PostgreSQL CPU snapshot | 61.08% | 68.59% |

## Result

The round-trip reduction improved steady payment p95 by 15.3% and reduced 429s by 4.1%, while reads remained healthy. It did not meet the 300 ms payment target, so it is a useful improvement but not sufficient by itself.

The next experiment keeps the pool and total database concurrency at 30 while shifting two permits from reads to writes: 6 read / 24 write.
