#!/usr/bin/env bash

set -u

root_dir="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
sample_file="$root_dir/load-test/results/pg-sample.tsv"
duration_seconds="${1:-240}"
profile="${PROFILE:-capacity150}"

printf 'timestamp|active_client_backends|server_waiting|lock_waiting|io_waiting|ungranted_locks|max_active_query_seconds\n' > "$sample_file"

(
  end_time=$((SECONDS + duration_seconds))
  while (( SECONDS < end_time )); do
    docker compose \
      -f "$root_dir/infra/compose.yaml" \
      -f "$root_dir/load-test/compose.yaml" \
      exec -T postgres \
      psql -U senzy -d senzy -AtF '|' -c \
      "select clock_timestamp(),
          count(*) filter (where backend_type = 'client backend' and state = 'active'),
          count(*) filter (where backend_type = 'client backend'
              and wait_event_type in ('Lock', 'IO', 'LWLock', 'BufferPin', 'Extension', 'Timeout')),
          count(*) filter (where backend_type = 'client backend' and wait_event_type = 'Lock'),
          count(*) filter (where backend_type = 'client backend' and wait_event_type = 'IO'),
          (select count(*) from pg_locks where not granted),
          coalesce(max(extract(epoch from clock_timestamp() - query_start))
              filter (where backend_type = 'client backend' and state = 'active' and query_start is not null), 0)
       from pg_stat_activity;" >> "$sample_file" 2>/dev/null || true
    sleep 2
  done
) &
sampler_pid=$!

cleanup() {
  kill "$sampler_pid" 2>/dev/null || true
  wait "$sampler_pid" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

PROFILE="$profile" docker compose \
  -f "$root_dir/infra/compose.yaml" \
  -f "$root_dir/load-test/compose.yaml" \
  run --rm --no-deps k6
