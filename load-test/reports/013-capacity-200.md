# 013 — Higher-capacity probe through 200 iterations/sec

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [012-sustainable-capacity.md](012-sustainable-capacity.md)

## Test setup

- 30-connection PostgreSQL pool
- 8 read permits / 22 write permits
- 60 queued requests per lane
- 32 distributed sender/recipient pairs
- Maximum k6 virtual users: 300
- Ramp: 100 → 125 → 150 → 200 iterations/sec, then ramp down
- 500 ms client backoff after any 429 response

## Results

| Metric | Result |
|---|---:|
| Maximum offered rate | 200 iterations/sec |
| Maximum configured VUs | 300 |
| Completed iterations | 29,021 |
| Committed payments | 21,697 |
| Dropped iterations | 274 |
| HTTP failure rate | 33.26% |
| HTTP 200 responses | 327,888 |
| HTTP 201 responses | 195,849 |
| HTTP 429 responses | 260,991 |
| HTTP 500/503 responses | 0 |
| Payment p95 | 1,031 ms |
| Payment p99 | 1,214 ms |
| Read p95 | 640 ms |
| Read p99 | 775 ms |
| PostgreSQL connections after run | 36 |
| PostgreSQL CPU snapshot after run | 31.32% |
| API CPU snapshot after run | 32.73% |

The run completed 58,192 checks and failed 28,999. The HTTP failure, payment p95, and read p95 thresholds all failed. The failures were bounded 429 responses; no 500 or 503 responses were observed.

## Result

The current configuration does not sustain the 200 iterations/sec target. It remained stable at the infrastructure level—no connection exhaustion or 500/503 errors—but the 8/22 admission lanes saturated, producing 33.26% HTTP failures and pushing both latency distributions beyond target. Compared with the passing 100 iterations/sec probe, the higher offered load increased payment p95 from 133 ms to 1,031 ms and read p95 from 40 ms to 640 ms.

The observed local capacity boundary is therefore between 100 and 200 mixed iterations/sec under the current 30-connection pool and 8/22 lane configuration. This is a stress boundary, not a production capacity claim.
