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

All runs used Docker Compose, one API container, one PostgreSQL 17 container, synthetic data, and a 100-VU k6 cap. These are local observations, not production capacity claims.
