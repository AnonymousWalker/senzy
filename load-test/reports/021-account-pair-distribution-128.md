# 021 — Distributed account-pair experiment: 128 pairs

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [020-corrected-db-wait-sampling.md](020-corrected-db-wait-sampling.md)

## Change tested

- Same 30-connection pool and 8 read / 22 write permits
- Same 60-request queues and 100 → 125 → 150 iterations/sec ramp
- Account pairs increased from 32 to 128
- Maximum k6 virtual users remained 250

## Results

| Metric | 32 pairs | 128 pairs |
|---|---:|---:|
| Completed iterations | 18,194 | 18,208 |
| Completed iteration rate | 94.0/sec | 78.7/sec |
| Committed payments | 16,628 | 17,277 |
| HTTP request rate | — | 238.3/sec |
| HTTP failure rate | 10.48% | 5.36% |
| HTTP 429 responses | 51,624 | 26,595 |
| HTTP 500/503 responses | 0 | 0 |
| Payment p95 | 794 ms | 662 ms |
| Wallet read p95 | 420 ms | 356 ms |
| Operations status p95 | 473 ms | 395 ms |
| API CPU snapshot | 31.82% | 25.65% |
| PostgreSQL CPU snapshot | 56.90% | 45.97% |

## Result

Distributing traffic across four times as many wallet pairs reduced 429s by 48.5%, improved payment p95 by 16.6%, improved wallet-read p95 by 15.3%, and increased committed payments by 3.9%. This confirms that repeated sender/recipient wallet-row contention materially contributed to the mixed-workload failure rate.

The wider distribution still missed the operating targets, so contention is only part of the bottleneck. The next probe increases distribution again to approximately one pair per active VU.
