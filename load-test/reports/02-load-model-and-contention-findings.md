# 02 — Load model and contention findings

## Test-model improvements

The load harness was expanded to distinguish application capacity from load-generator and fixture artifacts:

- Mixed iterations perform two reads and one payment.
- Read-only and payment-only profiles isolate each lane.
- Distributed synthetic sender/recipient pairs remove single-wallet hotspots.
- Long steady-state profiles use 256 account pairs.
- Wallet and operations-status latency are reported separately.
- PostgreSQL sampling excludes normal client socket waits and records server, lock, I/O, and ungranted-lock waits.

## Findings

### Account distribution mattered

At 50 iterations/sec, one sender/recipient pair produced 2.93-second payment p95 and 3.5% failures. Eight distributed pairs produced 42.6 ms payment p95 and 0% failures. The single-pair result was therefore an artificial hotspot, not system-wide capacity.

At the later 150/sec probe, increasing distributed pairs from 128 to 256 improved payment p95 from 662 ms to 604 ms and reduced failures from 5.36% to 3.68%.

### Reads and writes have different behavior

At 150/sec in isolation, reads passed with wallet p95 of 8 ms and status p95 of 13 ms. Payments alone reached 230 ms p95 with 0.11% failures. Mixed load caused the shared database budget and write path to affect both lanes, which justified separate read/write admission control.

### PostgreSQL I/O, not row locks, became the sustained bottleneck

Corrected steady-state sampling at 100/sec found:

- No lock waits and no ungranted locks.
- Average server-side waits: 2.04 backends.
- Average I/O waits: 1.00 backend.
- Peak server-side waits: 23 backends, all I/O waits.
- Payment p95: 444 ms before the later optimizations.

## Conclusion

The controlled test model showed that early failures came from connection exhaustion and synthetic hot-spotting, while the remaining sustained limit was concentrated in PostgreSQL write/I/O service time. This prevented treating queue size or virtual-user count as throughput improvements by themselves.
