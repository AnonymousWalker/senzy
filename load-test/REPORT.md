# Senzy load-test report index

Reports are kept separately so each optimization has its own before/after record.

| Report | Scope | Result |
|---|---|---|
| [001-baseline.md](reports/001-baseline.md) | Initial pool/rate-limit behavior | Connection exhaustion under stress; baseline passed at 50 mixed iterations/sec |
| [002-connection-pool-queue.md](reports/002-connection-pool-queue.md) | Shared Npgsql pool and bounded database queue | Removed 500/503 connection failures; latency still exceeded SLO at the highest stress level |

All runs used Docker Compose, one API container, one PostgreSQL 17 container, synthetic data, and a 100-VU k6 cap. These are local observations, not production capacity claims.
