# 014 — Boundary probe through 150 iterations/sec

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [013-capacity-200.md](013-capacity-200.md)

## Test setup

- 30-connection PostgreSQL pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- 32 distributed sender/recipient pairs
- Maximum k6 virtual users: 250
- Ramp: 100 → 125 → 150 iterations/sec, then ramp down
- 500 ms client backoff after any 429 response

## Results

| Metric | Result |
|---|---:|
| Maximum offered rate | 150 iterations/sec |
| Maximum configured VUs | 250 |
| Completed iterations | 18,186 |
| Completed iteration rate | 94.7/sec |
| Committed payments | 16,429 |
| Dropped iterations | 233 |
| HTTP request rate | 284.7/sec |
| HTTP failure rate | 15.87% |
| HTTP 200 responses | 265,653 |
| HTTP 201 responses | 148,437 |
| HTTP 429 responses | 78,093 |
| HTTP 500/503 responses | 0 |
| Payment p95 | 885 ms |
| Payment p99 | 1,049 ms |
| Read p95 | 518 ms |
| Read p99 | 601 ms |
| PostgreSQL connections after run | 36 |
| PostgreSQL CPU snapshot after run | 40.98% |
| API CPU snapshot after run | 38.75% |

The run completed 46,009 checks and failed 8,677. The HTTP failure and read p95 thresholds failed; the payment p95 threshold passed. Failures were bounded 429 responses; no 500 or 503 responses were observed.

## Result

The current configuration does not sustain 150 mixed iterations/sec within the HTTP failure and read-latency targets. Payment latency remained within its p95 target, but the read lane saturated first: read p95 reached 518 ms and the run produced a 15.87% HTTP failure rate. Compared with the passing 100 iterations/sec probe, this confirms that the practical target boundary is above 100/sec but below 150/sec for the current 8-read/22-write admission split.

The next optimization should focus on read-lane capacity and queue behavior while preserving the write lane, followed by a repeat of this same 150/sec probe.
