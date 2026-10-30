# 035 — Read/write lane split 7/23 at steady 101/sec

Date: 2026-10-30 UTC
Branch: `load-test-backend`
Compared with: [034-steady-101-postgres-tuned.md](034-steady-101-postgres-tuned.md)

## Change tested

At the passing steady 101/sec workload, one permit was moved from the read lane to the write lane:

- Read concurrency: 8 → 7
- Write concurrency: 22 → 23
- Total concurrency: unchanged at 30
- PostgreSQL pool: unchanged at 30 connections
- Read/write queues: unchanged at 60 items

All PostgreSQL tuning, the one-query wallet lock, 256 account pairs, and the 5-minute steady profile were unchanged.

## Results

| Metric | 8/22 | 7/23 | Change |
|---|---:|---:|---:|
| Completed iterations | 34,014 | 34,017 | +0.0% |
| Committed payments | 33,837 | 33,879 | +0.1% |
| HTTP failure rate | 0.17% | 0.13% | improved |
| HTTP 429 responses | 177 | 138 | -22.0% |
| HTTP 500/503 responses | 0 | 0 | unchanged |
| Payment p95 | 208 ms | 241 ms | +15.5% |
| Wallet read p95 | 22 ms | 24 ms | +9.1% |
| Operations status p95 | 38 ms | 41 ms | +7.9% |

The 7/23 run recorded 68,547 HTTP 200 responses, 34,391 HTTP 201 responses, and 138 HTTP 429 responses. Setup completed successfully for all 512 accounts.

## Threshold result

The run passed all configured thresholds, but it did not improve the primary payment latency metric:

- HTTP failure rate: `0.13% < 1%`
- Payment p95: `241 ms < 300 ms`
- Wallet read p95: `24 ms < 200 ms`
- Operations status p95: `41 ms < 200 ms`

## Interpretation

The extra write permit reduced 429s but increased payment and read tail latency. This is consistent with PostgreSQL write-path saturation: allowing more write work into the database lowers admission rejection but increases queueing or service contention. The 8/22 split remains the preferred configuration because it gives materially more payment latency headroom at the same offered load.
