# 028 — Steady 100/sec database-wait sampling

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [027-steady-100-wide.md](027-steady-100-wide.md)

## Test setup

- 256 distributed sender/recipient pairs
- 30-connection PostgreSQL pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- 45-second ramp to 100 iterations/sec
- 5-minute steady stage at 100 iterations/sec
- PostgreSQL activity sampled every two seconds; 208 samples collected

## Results

| Metric | Result |
|---|---:|
| Completed iterations | 33,585 |
| Committed payments | 33,310 |
| HTTP failure rate | 0.43% |
| HTTP 429 responses | 3,942 |
| HTTP 500/503 responses | 0 |
| Payment p95 | 444 ms |
| Wallet read p95 | 40 ms |
| Operations status p95 | 63 ms |
| Average server-side waits | 2.04 backends |
| Average lock waits | 0 |
| Average I/O waits | 1.00 backend |
| Peak server-side waits | 23 backends |
| Peak I/O waits | 23 backends |
| Peak ungranted locks | 0 |
| API CPU snapshot | 28.54% |
| PostgreSQL CPU snapshot | 61.08% |

## Result

The sustained bottleneck is not wallet-row locking: no lock waits or ungranted locks were observed in the corrected sample. PostgreSQL I/O waits dominated the observed server-side waits while payment p95 remained above target. Reads stayed healthy.

The next optimization removes one round trip from the payment wallet-lock operation by selecting and locking both accounts in one deterministic `FOR UPDATE` query. It does not weaken transaction durability, financial invariants, or lock ordering.
