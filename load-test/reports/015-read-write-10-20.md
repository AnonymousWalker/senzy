# 015 — Read/write lane split experiment: 10/20

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [014-capacity-150.md](014-capacity-150.md)

## Change tested

- PostgreSQL pool unchanged at 30 connections
- Read permits changed from 8 to 10
- Write permits changed from 22 to 20
- Read and write queues unchanged at 60 each
- Same 32 distributed sender/recipient pairs
- Same 100 → 125 → 150 iterations/sec ramp
- Maximum k6 virtual users: 250

## Results

| Metric | Prior 8/22 | 10/20 experiment |
|---|---:|---:|
| Completed iterations | 18,186 | 18,184 |
| Completed iteration rate | 94.7/sec | 94.7/sec |
| Committed payments | 16,429 | 15,612 |
| Dropped iterations | 233 | 235 |
| HTTP request rate | 284.7/sec | 284.7/sec |
| HTTP failure rate | 15.87% | 19.74% |
| HTTP 429 responses | 78,093 | 97,128 |
| HTTP 500/503 responses | 0 | 0 |
| Payment p95 | 885 ms | 958 ms |
| Read p95 | 518 ms | 555 ms |
| PostgreSQL CPU snapshot | 40.98% | 45.00% |
| API CPU snapshot | 38.75% | 37.46% |

## Result

This lane reallocation was not an improvement. Adding two read permits while removing two write permits increased 429 responses by 24.4%, worsened read p95 by 7.2%, worsened payment p95 by 8.2%, and reduced committed payments by 5.0%.

The experiment configuration was reverted. The working configuration remains 8 read permits / 22 write permits with a 30-connection pool and 60-request queues. The next tuning should avoid reducing write capacity and should investigate read-query cost or a more targeted read workload before changing the lane split again.
