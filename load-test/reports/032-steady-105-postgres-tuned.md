# 032 — Steady 105/sec with PostgreSQL tuning

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [031-postgres-io-tuning.md](031-postgres-io-tuning.md)

## Change tested

The PostgreSQL tuning from report 031 was retained. Only the offered steady load changed from 100 to 105 iterations/sec. The workload used 256 synthetic sender/recipient pairs, a 45-second ramp, a 5-minute steady phase, and a 30-second ramp-down.

The API remained on the 30-connection pool with 8 read permits, 22 write permits, and 60-item read/write queues. PostgreSQL remained configured with `shared_buffers=1GB`, `max_wal_size=4GB`, `checkpoint_timeout=15min`, `wal_compression=on`, `synchronous_commit=on`, and `fsync=on`.

## Results

| Metric | Steady 100/sec | Steady 105/sec | Change |
|---|---:|---:|---:|
| Completed iterations | 33,671 | 35,365 | +5.0% |
| Committed payments | 33,436 | 35,733 | +6.9% |
| HTTP failure rate | 0.23% | 0.13% | improved |
| HTTP 429 responses | 235 | 144 | -38.7% |
| HTTP 500/503 responses | 0 | 0 | unchanged |
| Payment p95 | 186 ms | 330 ms | +77.3% |
| Wallet read p95 | 20 ms | 27 ms | +35.0% |
| Operations status p95 | 34 ms | 50 ms | +47.1% |
| API CPU snapshot | 25.35% | 18.32% | snapshot only |
| PostgreSQL CPU snapshot | 59.45% | 64.18% | snapshot only |

The 105/sec run recorded 71,243 HTTP 200 responses, 35,733 HTTP 201 responses, and 144 HTTP 429 responses. Setup completed successfully for all 512 accounts.

## Threshold result

The run passed the failure-rate and read-latency thresholds but failed the payment tail-latency threshold:

- HTTP failure rate: `0.13% < 1%` — passed
- Payment p95: `330 ms < 300 ms` — failed
- Wallet read p95: `27 ms < 200 ms` — passed
- Operations status p95: `50 ms < 200 ms` — passed

## Interpretation

With the database tuning in place, 105/sec produces more completed work and fewer 429s than 100/sec, but payment p95 crosses the 300 ms operating target. This places the sustained target boundary between 100 and 105 iterations/sec under the current hardware and configuration. The increase is concentrated in the payment path; reads remain comfortably below target.

The CPU snapshots are included for continuity but were taken after the run and are not peak measurements. The next useful step is a repeat at 105/sec or a narrow 101–104/sec boundary probe; increasing load further would not be an optimization because the payment SLO is already exceeded at 105/sec.
