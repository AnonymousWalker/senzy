# 030 — Write-heavy lane split experiment: 6/24

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [029-wallet-lock-round-trip.md](029-wallet-lock-round-trip.md)

## Change tested

- Read permits changed from 8 to 6
- Write permits changed from 22 to 24
- Total permits remained 30
- PostgreSQL pool remained 30 connections
- Same one-query wallet-lock optimization, 256 pairs, and five-minute steady 100/sec workload

## Results

| Metric | 8/22 | 6/24 |
|---|---:|---:|
| Completed iterations | 33,669 | 33,606 |
| Committed payments | 33,249 | 33,290 |
| HTTP failure rate | 0.41% | 0.45% |
| HTTP 429 responses | 3,780 | 4,131 |
| HTTP 500/503 responses | 0 | 0 |
| Payment p95 | 376 ms | 413 ms |
| Wallet read p95 | 34 ms | 44 ms |
| Operations status p95 | 56 ms | 62 ms |
| API CPU snapshot | 26.37% | 19.53% |
| PostgreSQL CPU snapshot | 68.59% | 65.08% |

## Result

The write-heavy 6/24 split was a regression. Payment p95 worsened by 9.8%, HTTP 429s increased by 9.3%, and both read p95 values also worsened. The configuration was reverted to 8 read / 22 write permits.

The result confirms that simply granting more write permits does not remove the underlying PostgreSQL write-I/O bottleneck.
