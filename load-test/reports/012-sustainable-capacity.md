# 012 — Sustainable capacity probe through 100 iterations/sec

Date: 2026-10-29 UTC
Branch: `load-test-backend`
Compared with: [011-high-concurrency.md](011-high-concurrency.md)

## Test setup

- 30-connection PostgreSQL pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- 32 distributed sender/recipient pairs
- Maximum k6 virtual users: 200
- Ramp: 50 → 75 → 100 iterations/sec, then ramp down
- 500 ms client backoff after any 429 response

## Results

| Metric | Result |
|---|---:|
| Maximum offered rate | 100 iterations/sec |
| Maximum configured VUs | 200 |
| Completed iterations | 10,884 |
| Committed payments | 10,884 |
| Dropped iterations | 36 |
| HTTP failure rate | 0% |
| HTTP 429/500/503 responses | 0 |
| Payment p95 | 133 ms |
| Payment p99 | 218 ms |
| Read p95 | 40 ms |
| Read p99 | 64 ms |
| PostgreSQL connections after run | 36 |
| PostgreSQL CPU snapshot after run | 34.07% |
| API CPU snapshot after run | 37.43% |

All 32,780 correctness checks passed. Payment and read latency thresholds passed, and the HTTP failure threshold passed.

## Result

With distributed accounts, the current hardware and 30-connection pool sustained the full ramp through 100 mixed iterations/sec while meeting the measured targets. This is the strongest observed capacity point so far, but it is not a maximum: the test stopped at 100 iterations/sec and used a synthetic eight-pair distribution expanded to 32 pairs for the higher-concurrency probes.

The next step is to probe 125–200 iterations/sec with the same distributed workload, while separately retaining a hot-account mix to measure contention behavior. The 500-VU run should not be used as the system limit because it exceeded the configured admission budget and produced intentional 429 backpressure.
