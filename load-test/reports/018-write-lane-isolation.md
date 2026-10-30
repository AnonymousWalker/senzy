# 018 — Write-lane isolation through 150 iterations/sec

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [017-read-lane-isolation.md](017-read-lane-isolation.md)

## Test setup

- Payment-only workload; no wallet/status reads
- 30-connection PostgreSQL pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- 32 distributed sender/recipient pairs
- Maximum k6 virtual users: 250
- Ramp: 100 → 125 → 150 iterations/sec, then ramp down

## Results

| Metric | Result |
|---|---:|
| Completed iterations | 18,323 |
| Completed iteration rate | 95.5/sec |
| Committed payments | 18,303 |
| HTTP request rate | 96.1/sec |
| HTTP failure rate | 0.11% |
| HTTP 429 responses | 180 |
| HTTP 500/503 responses | 0 |
| Payment p95 | 230 ms |
| Payment p99 | 475 ms |
| Dropped iterations | 96 |
| API CPU snapshot | 25.84% |
| PostgreSQL CPU snapshot | 39.77% |
| PostgreSQL connections after run | 34 |

The payment p95 and HTTP failure thresholds passed. There were 20 failed checks, corresponding to expected bounded 429 responses; no server errors occurred.

## Result

The write lane can meet the 300 ms payment p95 target at this ramp when it is not competing with reads. Combined with the passing read-only run, this shows that the mixed-workload degradation is caused by shared PostgreSQL resources or transaction/lock interaction, not by either lane independently.

The next experiment will sample PostgreSQL activity and lock state during the mixed run rather than relying only on the post-run connection count.
