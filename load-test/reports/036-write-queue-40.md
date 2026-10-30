# 036 — Write queue reduction to 40 at steady 101/sec

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [034-steady-101-postgres-tuned.md](034-steady-101-postgres-tuned.md)

## Change tested

The preferred 8 read / 22 write concurrency split was retained. Only the write queue was reduced from 60 to 40 items at steady 101 iterations/sec. PostgreSQL tuning, the 30-connection pool, 256 account pairs, and the 5-minute steady workload were unchanged.

## Results

| Metric | Queue 60 | Queue 40 | Change |
|---|---:|---:|---:|
| Completed iterations | 34,014 | 34,027 | +0.0% |
| Committed payments | 33,837 | 33,656 | -0.5% |
| HTTP failure rate | 0.17% | 0.36% | +111.8% |
| HTTP 429 responses | 177 | 371 | +109.6% |
| HTTP 500/503 responses | 0 | 0 | unchanged |
| Payment p95 | 208 ms | 249 ms | +19.5% |
| Wallet read p95 | 22 ms | 27 ms | +22.7% |
| Operations status p95 | 38 ms | 47 ms | +23.7% |

The queue-40 run recorded 68,567 HTTP 200 responses, 34,168 HTTP 201 responses, and 371 HTTP 429 responses. Setup completed successfully for all 512 accounts.

## Threshold result

The run technically passed the configured thresholds, but it was rejected as an optimization because all primary metrics regressed:

- HTTP failure rate: `0.36% < 1%`
- Payment p95: `249 ms < 300 ms`
- Wallet read p95: `27 ms < 200 ms`
- Operations status p95: `47 ms < 200 ms`

## Interpretation

Reducing the write queue causes earlier rejection without reducing payment tail latency. The queue was restored to 60 items. The preferred configuration remains 8 read / 22 write permits with 60-item queues, together with the PostgreSQL I/O tuning and one-query wallet account lock.
