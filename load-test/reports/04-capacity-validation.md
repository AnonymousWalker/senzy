# 04 — Final capacity validation

## Operating targets

- HTTP failure rate below 1%.
- Payment p95 below 300 ms.
- Wallet and operations-status read p95 below 200 ms.

## Final steady-state boundary

All runs used Docker Compose, one API container, PostgreSQL 17, 256 synthetic account pairs, and a five-minute steady phase.

| Steady iterations/sec | Payment p95 | Failure rate | Wallet p95 | Status p95 | Result |
|---:|---:|---:|---:|---:|---|
| 100 | 186 ms | 0.23% | 20 ms | 34 ms | Pass |
| 101 | 208 ms | 0.17% | 22 ms | 38 ms | Pass |
| 102 | 328 ms | 0.27% | 26 ms | 42 ms | Payment target failed |
| 105 | 330 ms | 0.13% | 27 ms | 50 ms | Payment target failed |

No HTTP 500/503 responses were observed in these final boundary runs. The payment tail-latency cliff begins between 101 and 102 iterations/sec, while reads remain healthy.

## Capacity conclusion

The current configuration demonstrated a sustained SLO-compliant boundary between 101 and 102 mixed iterations/sec. The conservative operating target is 100 mixed iterations/sec, equivalent to approximately 300 business requests/sec:

- 200 wallet/status reads/sec.
- 100 payment writes/sec.

These are local Docker observations using synthetic data, not production capacity guarantees. The retained system configuration is a 30-connection pool, 8 read permits, 22 write permits, 60-item queues, the one-query wallet lock, and the PostgreSQL I/O settings in report 03.
