# Docker load test

This harness runs k6, the API, and PostgreSQL through Docker Compose. It creates synthetic users and exercises authenticated wallet/status reads plus idempotent simulated payments.

From the repository root:

```sh
mkdir -p load-test/results
DOCKER_BUILDKIT=0 docker compose -f infra/compose.yaml -f load-test/compose.yaml build api migrate
docker compose -f infra/compose.yaml -f load-test/compose.yaml up -d postgres migrate api
docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Stress profile:
PROFILE=stress docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Controlled offered load with client backoff:
PROFILE=controlled docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Ceiling probe capped at 75 iterations/sec:
PROFILE=ceiling docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Lower ceiling probe capped at 50 iterations/sec:
PROFILE=ceiling50 docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Same 50/sec probe with eight distributed sender/recipient pairs:
PROFILE=distributed50 docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6
```

Results are written to `load-test/results/summary.json` and `load-test/results/k6.json`. The Compose override raises only the benchmark instance's process-local auth/payment rate limits; it does not change application source or any production configuration.

The checked-in scenario includes a baseline ramp and a short stress ramp. The API Compose configuration keeps a 30-connection pool and uses separate 8-read/22-write database concurrency lanes with bounded 60-request queues. Never point it at production or a database containing real identities or financial data.
