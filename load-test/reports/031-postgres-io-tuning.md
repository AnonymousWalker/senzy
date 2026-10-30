# 031 — PostgreSQL memory and checkpoint I/O tuning

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [029-wallet-lock-round-trip.md](029-wallet-lock-round-trip.md)

## Change tested

The application code, pool, queues, account distribution, and offered load were unchanged from report 029. PostgreSQL was restarted with durability-preserving I/O settings:

- `shared_buffers=1GB` instead of the PostgreSQL default `128MB`
- `max_wal_size=4GB` instead of `1GB`
- `checkpoint_timeout=15min` instead of `5min`
- `wal_compression=on` instead of `off`
- `synchronous_commit=on` and `fsync=on` retained

The Docker workload was `PROFILE=steady100wide`: 256 synthetic sender/recipient pairs, 45-second ramp to 100 iterations/sec, 5-minute steady phase at 100 iterations/sec, and 30-second ramp-down. The API used the 30-connection pool with 8 read permits, 22 write permits, and 60-item read/write queues.

## Results

| Metric | Report 029 | PostgreSQL tuning | Change |
|---|---:|---:|---:|
| Completed iterations | 33,669 | 33,671 | +0.0% |
| Committed payments | 33,249 | 33,436 | +0.6% |
| HTTP failure rate | 0.41% | 0.23% | -43.8% |
| HTTP 429 responses | 3,780 | 235 | -93.8% |
| HTTP 500/503 responses | 0 | 0 | unchanged |
| Payment p95 | 376 ms | 186 ms | -50.4% |
| Wallet read p95 | 34 ms | 20 ms | -40.3% |
| Operations status p95 | 56 ms | 34 ms | -40.0% |
| API CPU snapshot | 26.37% | 25.35% | -3.9% |
| PostgreSQL CPU snapshot | 68.59% | 59.45% | -13.3% |

The tuned run recorded 67,855 HTTP 200 responses, 33,948 HTTP 201 responses, and 235 HTTP 429 responses. Setup completed successfully for all 512 accounts: 512 registrations and 512 logins passed.

## Threshold result

This iteration passed the configured operating thresholds:

- HTTP failure rate: `0.23% < 1%`
- Payment p95: `186 ms < 300 ms`
- Wallet read p95: `20 ms < 200 ms`
- Operations status p95: `34 ms < 200 ms`

## Interpretation

This is the first sustained 100 iterations/sec run in the current optimization sequence that passes the payment latency target after the wallet-lock round-trip reduction. The large reduction in 429s and payment tail latency, together with lower PostgreSQL CPU, supports the earlier diagnosis that checkpoint/WAL I/O overhead was a major bottleneck on this hardware.

The result is a local Docker observation, not a production capacity claim. A repeat run is still warranted before treating these settings as the stable baseline, because the comparison is one run per configuration and the prior run used the old PostgreSQL settings.
