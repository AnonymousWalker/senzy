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

# High-concurrency distributed-account probe, up to 500 VUs:
PROFILE=concurrency docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Sustainable-capacity probe, up to 200 VUs:
PROFILE=capacity docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Higher-capacity probe through 200 iterations/sec:
PROFILE=capacity200 docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Boundary probe through 150 iterations/sec:
PROFILE=capacity150 docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Same boundary probe with 128 distributed account pairs:
PROFILE=capacity150wide docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Same boundary probe with 256 distributed account pairs:
PROFILE=capacity150widest docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Passing-boundary probe with 256 distributed account pairs through 125 iterations/sec:
PROFILE=capacity125wide docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Boundary probe with 256 distributed account pairs through 110 iterations/sec:
PROFILE=capacity110wide docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Boundary probe with 256 distributed account pairs through 105 iterations/sec:
PROFILE=capacity105wide docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Longer steady-state validation at 105 iterations/sec:
PROFILE=steady105wide docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Narrow boundary probe at 102 iterations/sec:
PROFILE=steady102wide docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Final one-step boundary probe at 101 iterations/sec:
PROFILE=steady101wide docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Longer steady-state validation at 100 iterations/sec:
PROFILE=steady100wide docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Read-only lane isolation through 150 iterations/sec:
PROFILE=read150 docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Payment-only lane isolation through 150 iterations/sec:
PROFILE=write150 docker compose -f infra/compose.yaml -f load-test/compose.yaml run --rm --no-deps k6

# Mixed probe with PostgreSQL activity and lock sampling:
bash load-test/run-sampled-mixed.sh 240

# Long steady-state probe with PostgreSQL activity and lock sampling:
PROFILE=steady100wide bash load-test/run-sampled-mixed.sh 480
```

Results are written to `load-test/results/summary.json` and `load-test/results/k6.json`. The Compose override raises only the benchmark instance's process-local auth/payment rate limits; it does not change application source or any production configuration.

The load-test thresholds match the operating targets: payment p95 <=300ms, wallet/status read p95 <=200ms, and HTTP failure rate <1%. Wallet and operations-status reads are reported separately so shared read-lane queueing can be distinguished from endpoint-specific query cost.

The checked-in scenario includes a baseline ramp and a short stress ramp. The API Compose configuration keeps a 30-connection pool and uses separate 8-read/22-write database concurrency lanes with bounded 60-request queues. Never point it at production or a database containing real identities or financial data.
