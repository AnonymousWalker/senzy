# Initial workload and reliability targets

These are engineering targets for future validation, not measured capacity or a production SLA. Phase 0 establishes the workload and measurement definitions; Phase 4 must execute load and recovery tests before claiming these targets are met. Backend ownership includes instrumentation and recovery; product ownership includes confirming traffic assumptions before a pilot.

## Workload

- Planning volume: 10 million committed payment commands/day, approximately 116/second averaged over 24 hours.
- Peak factor: 10x average, rounded to 1,200 new payment commands/second for 15 minutes.
- Short burst: 2,400 new commands/second for 60 seconds; apply explicit backpressure rather than accept work that cannot be durably processed.
- Read traffic: five authenticated balance/activity/status reads per new command; peak mixed workload is 1,200 commands and 6,000 reads/second.
- Add 10% command replays using existing idempotency keys; count these as HTTP attempts, not new posted payments.
- Initial dataset: one million synthetic accounts and 30 days of synthetic activity. Exercise both distributed account pairs and contention where 20% of sends involve 1% of accounts.
- Exercise insufficient funds, invalid requests, concurrent sends from one account, duplicate commands, timeouts around commit, PostgreSQL restart, and outbox-consumer outage. No real identities or bank data in load fixtures.

Run a 15-minute warmup, a 60-minute steady workload, the peak/burst stages, and a 24-hour soak. Record hardware, database settings, dataset, client concurrency, latency histograms, queue depth, locks, pool waits, CPU/I/O, and error categories. Review cost per committed payment. Revisit these assumptions after actual cohort measurements.

## Service objectives

| Measure | Initial target and definition |
| --- | --- |
| API availability | 99.9% over rolling 30 days, measured as non-5xx, non-timeout eligible requests; exclude expected validation/auth/client errors. Overload-related 429s for otherwise eligible traffic count as failures. |
| Internal payment latency | p95 <= 300 ms and p99 <= 1 s from API receipt to committed response under the agreed peak workload; report retries separately. External pending-to-settled time is a separate provider metric. |
| Read latency | p95 <= 200 ms and p99 <= 500 ms for balance/activity/status under the mixed workload. |
| Correctness | Zero unbalanced postings, duplicate postings, acknowledged uncommitted payments, or unexplained reconciliation variance. Any violation blocks rollout. |
| Outbox freshness | p99 delivery lag <= 60 seconds during healthy operation; alert after oldest undelivered event exceeds 5 minutes. |
| Reconciliation | Daily reconciliation completes by 02:00 UTC for the prior UTC day; failures or unexplained differences require investigation before expansion. |
| Recovery time | Restore API and authoritative database service within 60 minutes in a documented recovery exercise. |
| Recovery point | Backup/PITR target <= 5 minutes for disaster recovery; acknowledged-commit loss target is zero for ordinary process/instance failures. Disaster data gaps require reconciliation and suspended transfers before reopening. |

Measure HTTP requests, business outcomes, and commit outcomes separately. Track correlation IDs across payment, ledger, and outbox records, but never log tokens, raw financial credentials, or unrestricted request bodies. Keep user IDs out of metric labels to avoid high cardinality and unnecessary exposure.

Before pilot: define alert routing and owners, prove backup restoration, measure external workflows separately, and record actual results beside these targets. PostgreSQL durability/replication and backup choices must support the stated failure scenarios.

## Refactor runtime requirements — planned

The [accepted refactor](MODULAR_MONOLITH_REFACTOR.md) retains one application artifact and PostgreSQL. None of the following is a measured capacity claim or a statement that multi-replica operation is already verified.

- API instances may scale behind a load balancer; size aggregate connection pools and concurrency limits against database capacity. Include hot-wallet contention in measurements because additional replicas do not remove row-lock contention.
- Share protected Data Protection keys and validate session revocation across instances. Configure trusted forwarded headers before using client IPs or scheme information behind a proxy. Establish edge/distributed rate limits where a limit must apply across replicas; current application limiters are process-local.
- Keep the in-memory banking stub explicitly single-instance for development, or implement and verify durable sandbox state before a multi-instance sandbox test. Preserve Development guards and token ownership/expiry/single-use semantics.
- Support optional API/worker roles in the same artifact. Coordinate scheduled reconciliation with database leases and unique scheduled-run identities, including safe takeover after crashes. Current workers run after startup and every 24 hours per process; they do not yet meet the prior-day 02:00 UTC scheduling objective.
- Define reconciliation cutoff and snapshot semantics before claiming results under concurrent writes. Track discrepancies and authorized resolutions without editing posted ledger entries.
- Extend outbox delivery with per-consumer checkpoints/deduplication, explicit failed-delivery state, authorized replay, and worker health. Measure retry volume, oldest pending age, failed deliveries, and projection freshness; do not claim exactly-once delivery.
- Correlate HTTP commands, financial commits, events, and background work through tracing metadata. Measure lock waits, pool saturation, posting outcomes, reconciliation variance, and queue lag using low-cardinality labels. Keep sensitive audit records access-controlled and separate from general diagnostic logs.
- Apply migrations explicitly, verify old/new instance and event compatibility during rollout, drain in-flight work on shutdown, and test restore/PITR plus post-restore reconciliation before reopening transfers. Define alert owners and recovery procedures before operational sign-off.

Validate with multiple application instances against one isolated PostgreSQL database, including process termination during commands/delivery, expired leases, database restarts, duplicate delivery, and coordinated scheduling. Record measured results separately from these requirements. See the [refactor exit criteria](MODULAR_MONOLITH_REFACTOR.md#migration-sequence-and-exit-criteria).
