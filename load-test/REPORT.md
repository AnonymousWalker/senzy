# Senzy load-test report index

Reports are kept separately so each optimization has its own before/after record.

| Report | Scope | Result |
|---|---|---|
| [001-baseline.md](reports/001-baseline.md) | Initial pool/rate-limit behavior | Connection exhaustion under stress; baseline passed at 50 mixed iterations/sec |
| [002-connection-pool-queue.md](reports/002-connection-pool-queue.md) | Shared Npgsql pool and bounded database queue | Removed 500/503 connection failures; latency still exceeded SLO at the highest stress level |
| [003-pool-size-50.md](reports/003-pool-size-50.md) | 50-connection pool with the same 30/60 queue | No improvement; payment tail latency worsened and throughput fell |
| [004-pool-and-concurrency-50.md](reports/004-pool-and-concurrency-50.md) | 50-connection pool with 50/60 database concurrency queue | Correct paired trial; still worse than 30/30/60 under stress |
| [005-separate-read-write-lanes.md](reports/005-separate-read-write-lanes.md) | 30-connection pool with 10 read and 20 write permits | Read latency improved sharply; write queue rejected 26.9% of requests |
| [006-tuned-read-write-lanes.md](reports/006-tuned-read-write-lanes.md) | 30-connection pool with 8 read and 22 write permits, 60/60 queues | More committed payments and fewer 429s; overload targets still fail |
| [007-controlled-offered-load.md](reports/007-controlled-offered-load.md) | Same 8/22 lanes with controlled arrival rate and 429 backoff | No 500/503s; reads healthy, 100/sec still caused 10% 429s |
| [008-ceiling-probe-75.md](reports/008-ceiling-probe-75.md) | Same 8/22 lanes with controlled ramp capped at 75/sec | Read target passes; payment p95 3.03s and 7.7% 429s |
| [009-ceiling-probe-50.md](reports/009-ceiling-probe-50.md) | Same 8/22 lanes with controlled ramp capped at 50/sec | 3.5% 429s and 2.93s payment p95; reads pass |
| [010-distributed-accounts-50.md](reports/010-distributed-accounts-50.md) | Same 8/22 lanes and 50/sec ramp with eight account pairs | All thresholds pass; hot-wallet contention isolated as prior bottleneck |
| [011-high-concurrency.md](reports/011-high-concurrency.md) | Distributed accounts, up to 500 VUs and 800 iterations/sec | 500-VU cap reached; 75.8% failures under bounded backpressure |
| [012-sustainable-capacity.md](reports/012-sustainable-capacity.md) | Distributed accounts, up to 200 VUs and 50/75/100 iterations/sec | All thresholds pass through 100 iterations/sec |
| [013-capacity-200.md](reports/013-capacity-200.md) | Distributed accounts, up to 300 VUs and 100/125/150/200 iterations/sec | 200/sec failed: 33.26% bounded 429s; payment p95 1.03s; read p95 640ms |
| [014-capacity-150.md](reports/014-capacity-150.md) | Distributed accounts, up to 250 VUs and 100/125/150 iterations/sec | 150/sec failed: 15.87% bounded 429s; payment p95 885ms; read p95 518ms |
| [015-read-write-10-20.md](reports/015-read-write-10-20.md) | 10 read / 20 write permits at the same 150/sec boundary probe | Rejected: 19.74% bounded 429s; read p95 555ms; committed payments decreased |
| [016-read-path-diagnostics.md](reports/016-read-path-diagnostics.md) | Separate wallet/status metrics at 150/sec with operating thresholds | Both reads queue under load; status p95 592ms vs wallet p95 526ms; no 500/503s |
| [017-read-lane-isolation.md](reports/017-read-lane-isolation.md) | Read-only wallet/status workload through 150/sec | Isolated reads passed: wallet p95 8ms; status p95 13ms; 0 failures |
| [018-write-lane-isolation.md](reports/018-write-lane-isolation.md) | Payment-only workload through 150/sec | Isolated writes passed: payment p95 230ms; 0.11% failures; 18,303 committed |
| [019-mixed-db-wait-sampling.md](reports/019-mixed-db-wait-sampling.md) | Mixed 150/sec probe with PostgreSQL activity/lock sampling | Avg 26.4 waiting backends; peak 6 lock waits; confirms shared queue/transaction contention |
| [020-corrected-db-wait-sampling.md](reports/020-corrected-db-wait-sampling.md) | Corrected mixed probe excluding PostgreSQL client waits | Avg 1.77 server waits; peak 57 ungranted locks and 8 lock waits; payment p95 794ms |
| [021-account-pair-distribution-128.md](reports/021-account-pair-distribution-128.md) | Mixed 150/sec probe with 128 account pairs | Improvement: 5.36% failures; payment p95 662ms; wallet p95 356ms |
| [022-account-pair-distribution-256.md](reports/022-account-pair-distribution-256.md) | Mixed 150/sec probe with 256 account pairs | Improvement: 3.68% failures; payment p95 604ms; wallet p95 325ms |
| [023-capacity-110-wide.md](reports/023-capacity-110-wide.md) | Mixed 75/100/110/sec probe with 256 account pairs | Near pass: 0.94% failures; payment p95 342ms; reads pass |
| [024-capacity-105-wide.md](reports/024-capacity-105-wide.md) | Mixed 75/100/105/sec probe with 256 account pairs | First pass near boundary: 0.13% failures; payment p95 284ms; reads pass |
| [025-capacity-105-repeat.md](reports/025-capacity-105-repeat.md) | Repeat mixed 75/100/105/sec probe with 256 account pairs | Reproduced pass: 0.52% failures; payment p95 294ms; reads pass |
| [026-steady-105-wide.md](reports/026-steady-105-wide.md) | Five-minute steady-state validation at 105/sec with 256 pairs | Not sustainable: payment p95 605ms despite 0.67% failures and passing reads |
| [027-steady-100-wide.md](reports/027-steady-100-wide.md) | Five-minute steady-state validation at 100/sec with 256 pairs | Not sustainable: payment p95 472ms despite 0.20% failures and passing reads |
| [028-steady-100-db-wait-sampling.md](reports/028-steady-100-db-wait-sampling.md) | Five-minute steady 100/sec probe with corrected PostgreSQL wait sampling | No lock waits; I/O waits peaked at 23; payment p95 444ms |
| [029-wallet-lock-round-trip.md](reports/029-wallet-lock-round-trip.md) | One-query deterministic wallet account lock during five-minute steady 100/sec probe | Improvement: payment p95 376ms vs 444ms; still above target |
| [030-lane-split-6-24.md](reports/030-lane-split-6-24.md) | 6 read / 24 write permits with the optimized wallet-lock query | Rejected: payment p95 worsened to 413ms; 8/22 restored |
| [031-postgres-io-tuning.md](reports/031-postgres-io-tuning.md) | 1GB shared buffers, larger WAL, longer checkpoints, and WAL compression at steady 100/sec | Passed: payment p95 186ms, 0.23% failures, 235 429s; first sustained 100/sec pass in this sequence |
| [032-steady-105-postgres-tuned.md](reports/032-steady-105-postgres-tuned.md) | Same tuned database and application at steady 105/sec | Boundary exceeded: payment p95 330ms; reads pass and HTTP failures remain at 0.13% |
| [033-steady-102-postgres-tuned.md](reports/033-steady-102-postgres-tuned.md) | Same tuned database and application at steady 102/sec | Boundary exceeded: payment p95 328ms; reads pass and HTTP failures remain at 0.27% |
| [034-steady-101-postgres-tuned.md](reports/034-steady-101-postgres-tuned.md) | Same tuned database and application at steady 101/sec | Passed: payment p95 208ms, 0.17% failures, 177 429s; current demonstrated SLO ceiling |
| [035-lane-split-7-23.md](reports/035-lane-split-7-23.md) | One permit moved from reads to writes at steady 101/sec | Passed but rejected: payment p95 worsened to 241ms despite fewer 429s; 8/22 preferred |
| [036-write-queue-40.md](reports/036-write-queue-40.md) | Write queue reduced from 60 to 40 at steady 101/sec | Passed but rejected: payment p95 worsened to 249ms and 429s rose to 371; queue 60 restored |
| [037-latest-session-summary.md](reports/037-latest-session-summary.md) | Consolidated summary of retained changes and reports 031–036 | Accepted PostgreSQL and wallet-lock optimizations; conservative capacity target is 100 mixed iterations/sec |

All runs used Docker Compose, one API container, one PostgreSQL 17 container, synthetic data, and profile-specific k6 VU caps. These are local observations, not production capacity claims.
