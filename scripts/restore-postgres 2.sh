#!/usr/bin/env bash
set -Eeuo pipefail

if [[ $# -ne 1 || ! -f "$1" ]]; then
  echo "Usage: $0 /absolute/path/to/backup.dump" >&2
  exit 2
fi
if [[ "${CONFIRM_DATABASE_RESTORE:-}" != "RESTORE" ]]; then
  echo "Restore refused. Set CONFIRM_DATABASE_RESTORE=RESTORE after verifying the target database." >&2
  exit 1
fi

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_dir"
docker compose --env-file .env -f docker-compose.prod.yml exec -T postgres \
  sh -c 'pg_restore --clean --if-exists --no-owner --no-acl --username "$POSTGRES_USER" --dbname "$POSTGRES_DB"' \
  <"$1"
echo "Restore completed. Run application smoke tests before reopening traffic."
