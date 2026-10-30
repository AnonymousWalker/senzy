# 011 — High-concurrency distributed-account probe

Date: 2026-10-29 UTC
Branch: `load-test-backend`
Compared with: [010-distributed-accounts-50.md](010-distributed-accounts-50.md)

## Test setup

- 30-connection PostgreSQL pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- 32 distributed sender/recipient pairs
- k6 ramp: 100 → 200 → 400 → 800 iterations/sec
- Maximum k6 virtual users: 500
- 500 ms client backoff after any 429 response

The run reached the 500-VU generator cap during the high-load stages.

## Results

| Metric | Result |
|---|---:|
| Maximum configured VUs | 500 |
| Completed iterations | 65,670 |
| Committed payments | 18,945 |
| Dropped iterations | 8,610 |
| HTTP failure rate | 75.8% |
| Payment p95 | 1.15 s |
| Payment p99 | 1.98 s |
| Read p95 | 697 ms |
| Read p99 | 1.14 s |
| PostgreSQL connections after run | 36 |
| PostgreSQL CPU snapshot after run | 37.92% |
| API CPU snapshot after run | 39.07% |

The run crossed all latency/error thresholds. No database connection-exhaustion error was observed; the dominant behavior was bounded application backpressure, with 429 responses accounting for the failed request volume.

## Result

The current configuration can drive substantial work—65,670 completed iterations and 18,945 committed payments—but it cannot sustain the offered high-concurrency load. The 500-VU ceiling was reached, while the API’s 30 total database permits and bounded queues rejected most excess work.

This is an admission-control ceiling, not a measured CPU ceiling: post-run CPU snapshots were below saturation, but they are not a substitute for time-series resource telemetry. The next capacity experiment should increase database permits only after measuring queue wait, PostgreSQL lock waits, and transaction duration; otherwise adding concurrency would risk turning controlled 429s into database contention.
