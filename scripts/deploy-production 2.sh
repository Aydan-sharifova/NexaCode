#!/usr/bin/env bash
set -Eeuo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
compose_file="$repo_dir/docker-compose.prod.yml"

if [[ ! -f "$repo_dir/.env" ]]; then
  echo "Missing .env. Create it from .env.example with production values." >&2
  exit 1
fi

cd "$repo_dir"
docker compose --env-file .env -f "$compose_file" config --quiet
docker compose --env-file .env -f "$compose_file" pull

if [[ "${SKIP_DATABASE_BACKUP:-false}" != "true" ]]; then
  "$repo_dir/scripts/backup-postgres.sh"
fi

docker compose --env-file .env -f "$compose_file" --profile operations run --rm migrator
docker compose --env-file .env -f "$compose_file" up -d --remove-orphans

health_url="${DEPLOY_HEALTH_URL:-http://127.0.0.1/health/ready}"
for _ in {1..30}; do
  if curl --fail --silent --show-error "$health_url" >/dev/null; then
    docker compose --env-file .env -f "$compose_file" ps
    echo "Production services are healthy."
    exit 0
  fi
  sleep 2
done

docker compose --env-file .env -f "$compose_file" ps
docker compose --env-file .env -f "$compose_file" logs --tail=100 api nginx
echo "Deployment did not become ready at $health_url." >&2
exit 1
