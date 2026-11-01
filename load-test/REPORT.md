# Senzy load-test report index

The detailed per-iteration reports have been consolidated into four topic reports. Each report preserves the major implementation changes, measurements, rejected experiments, and final conclusions.

| Report | Scope | Result |
|---|---|---|
| [01-pool-and-admission-control.md](reports/01-pool-and-admission-control.md) | Shared PostgreSQL data source, connection pool, and read/write admission control | Removed connection exhaustion and established the retained 30-connection, 8/22 lane configuration |
| [02-load-model-and-contention-findings.md](reports/02-load-model-and-contention-findings.md) | Distributed accounts, isolated lanes, steady-state profiles, and PostgreSQL wait sampling | Separated fixture hotspots from system capacity and identified PostgreSQL I/O as the sustained bottleneck |
| [03-payment-and-postgres-optimization.md](reports/03-payment-and-postgres-optimization.md) | One-query wallet locking, PostgreSQL I/O tuning, and rejected lane/queue variants | Reduced steady 100/sec payment p95 to 186 ms; retained the query and database optimizations |
| [04-capacity-validation.md](reports/04-capacity-validation.md) | Final steady-state boundary under the retained configuration | 100–101 mixed iterations/sec passed; 102/sec exceeded the payment target |

All runs used Docker Compose, synthetic data, and profile-specific k6 VU caps. These are local observations, not production capacity claims.
