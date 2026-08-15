#!/usr/bin/env bash
set -Eeuo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
backup_dir="${BACKUP_DIR:-$repo_dir/backups}"
mkdir -p "$backup_dir"
backup_file="$backup_dir/coding-$(date -u +%Y%m%dT%H%M%SZ).dump"

cd "$repo_dir"
docker compose --env-file .env -f docker-compose.prod.yml exec -T postgres \
  sh -c 'pg_dump --format=custom --no-owner --no-acl --username "$POSTGRES_USER" "$POSTGRES_DB"' \
  >"$backup_file"
chmod 600 "$backup_file"
echo "Backup written to $backup_file"
