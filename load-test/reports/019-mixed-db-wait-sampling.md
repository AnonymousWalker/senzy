# 019 — Mixed-workload PostgreSQL wait sampling

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [016-read-path-diagnostics.md](016-read-path-diagnostics.md)

## Test setup

- Same mixed 100 → 125 → 150 iterations/sec probe
- 30-connection PostgreSQL pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- 32 distributed sender/recipient pairs
- PostgreSQL activity sampled every two seconds during the run
- 80 samples collected

## Results

Note: the initial sampler’s broad waiting-backend column included PostgreSQL client waits, so it was not a valid measure of server-side database pressure. The lock and ungranted-lock columns remain valid. A corrected sampler run is recorded separately in [020-corrected-db-wait-sampling.md](020-corrected-db-wait-sampling.md).

| Metric | Average | Maximum |
|---|---:|---:|
| Active client backends | 5.83 | 27 |
| Waiting client backends, including client waits | 26.39 | 30 |
| Lock-waiting backends | 0.84 | 6 |
| I/O-waiting backends | 0.40 | 6 |
| Ungranted locks | 0.79 | 6 |
| Longest active query observed | — | 1.109 sec |

Application results were consistent with the prior mixed probe:

| Metric | Result |
|---|---:|
| Completed iterations | 18,180 |
| Completed iteration rate | 93.9/sec |
| Committed payments | 15,443 |
| HTTP failure rate | 20.41% |
| HTTP 429 responses | 100,413 |
| Payment p95 | 976 ms |
| Wallet read p95 | 524 ms |
| Operations status p95 | 590 ms |
| API CPU snapshot | 34.05% |
| PostgreSQL CPU snapshot | 50.74% |

## Result

The database was not CPU-saturated, but it had a large waiting population: an average of 26 waiting client backends and a peak of 30. Lock waits and ungranted locks were present but not dominant in aggregate. This indicates that the application’s bounded database lanes and shared transaction workload are the immediate bottleneck, with some lock contention contributing to the tail.

The next optimization should reduce transaction hold time and lock overlap—especially the payment path’s work before commit—then repeat the mixed probe. Increasing the pool or read permits without reducing that wait will likely move the queue rather than increase useful throughput.
