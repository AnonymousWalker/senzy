# 03 — Payment path and PostgreSQL optimization

## Payment-path change

`WalletMovementStore` previously selected the two ledger-account IDs and then issued a second query to load and lock them. It now performs one deterministic `SELECT ... FOR UPDATE` query ordered by account ID. This removes one database round trip per payment while preserving transaction boundaries, lock ordering, and financial durability.

At steady 100 iterations/sec, this reduced payment p95 from 444 ms to 376 ms and reduced 429s from 3,942 to 3,780.

## PostgreSQL change

The Docker PostgreSQL service was tuned with:

```text
shared_buffers=1GB
max_wal_size=4GB
checkpoint_timeout=15min
wal_compression=on
synchronous_commit=on
fsync=on
```

The last two settings were retained to preserve synchronous commit and crash durability.

At the same steady 100/sec workload, PostgreSQL tuning produced:

| Metric | Before tuning | After tuning |
|---|---:|---:|
| Payment p95 | 376 ms | 186 ms |
| HTTP failure rate | 0.41% | 0.23% |
| HTTP 429 responses | 3,780 | 235 |
| Wallet read p95 | 34 ms | 20 ms |
| Status read p95 | 56 ms | 34 ms |
| PostgreSQL CPU snapshot | 68.59% | 59.45% |

## Rejected follow-up tuning

- Moving from 8/22 to 6/24 permits increased payment p95 to 413 ms.
- Moving from 8/22 to 7/23 permits increased payment p95 to 241 ms at 101/sec.
- Reducing the write queue from 60 to 40 increased payment p95 to 249 ms and 429s from 177 to 371.

These trials showed that admitting more write concurrency or rejecting earlier did not reduce database service time. The retained configuration is 8/22 with 60-item queues.
