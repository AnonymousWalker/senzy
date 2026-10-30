# 022 — Distributed account-pair experiment: 256 pairs

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [021-account-pair-distribution-128.md](021-account-pair-distribution-128.md)

## Change tested

- Same 30-connection pool and 8 read / 22 write permits
- Same 60-request queues and 100 → 125 → 150 iterations/sec ramp
- Account pairs increased from 128 to 256
- Maximum k6 virtual users remained 250
- k6 setup timeout increased to five minutes because creating 512 synthetic users exceeded the default 60-second setup limit

## Results

| Metric | 128 pairs | 256 pairs |
|---|---:|---:|
| Completed iterations | 18,208 | 18,208 |
| Committed payments | 17,277 | 17,654 |
| HTTP request rate | 238.3/sec | 196.7/sec |
| HTTP failure rate | 5.36% | 3.68% |
| HTTP 429 responses | 26,595 | 18,450 |
| HTTP 500/503 responses | 0 | 0 |
| Payment p95 | 662 ms | 604 ms |
| Wallet read p95 | 356 ms | 325 ms |
| Operations status p95 | 395 ms | 360 ms |
| API CPU snapshot | 25.65% | 30.68% |
| PostgreSQL CPU snapshot | 45.97% | 42.92% |

## Result

Increasing distribution from 128 to 256 pairs reduced HTTP failures by 31.3%, reduced 429s by 30.6%, improved payment p95 by 8.7%, and improved read p95 by roughly 9%. The improvement confirms that wallet-row contention is real, but the 150/sec mixed workload still misses the 1% failure, 300 ms payment, and 200 ms read targets.

The next step is to measure the passing boundary with 256 pairs and a lower offered-load cap before changing the database pool or concurrency split again.
