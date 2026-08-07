#!/usr/bin/env bash

set -Eeuo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
api_pid=""
frontend_pid=""

cleanup() {
  trap - EXIT INT TERM
  [[ -z "$frontend_pid" ]] || kill "$frontend_pid" 2>/dev/null || true
  [[ -z "$api_pid" ]] || kill "$api_pid" 2>/dev/null || true
  wait 2>/dev/null || true
}

trap cleanup EXIT INT TERM

if [[ ! -f "$root_dir/.env" ]]; then
  echo "Missing .env. Copy .env.example to .env and add the local values first." >&2
  exit 1
fi

dotnet run \
  --project "$root_dir/src/Coding.Api/Coding.Api.csproj" \
  --launch-profile http \
  --no-restore &
api_pid=$!

api_ready=false
for _ in {1..60}; do
  if ! kill -0 "$api_pid" 2>/dev/null; then
    wait "$api_pid"
    exit 1
  fi

  if curl --fail --silent --show-error http://localhost:5192/health >/dev/null 2>&1; then
    api_ready=true
    break
  fi

  sleep 0.5
done

if [[ "$api_ready" != true ]]; then
  echo "The API did not become healthy on http://localhost:5192." >&2
  exit 1
fi

(
  cd "$root_dir/frontend"
  npm run dev
) &
frontend_pid=$!

wait -n "$api_pid" "$frontend_pid"
