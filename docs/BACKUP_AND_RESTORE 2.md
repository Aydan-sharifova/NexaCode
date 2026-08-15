# PostgreSQL backup and restore

`scripts/backup-postgres.sh` creates a mode-600 custom-format `pg_dump` under ignored `backups/` (or `BACKUP_DIR`). Run it before migrations and copy encrypted backups off-host according to retention policy.

Restore only during a maintenance window after testing the backup on an isolated database. Verify the target `.env`, stop application writes, then explicitly acknowledge the destructive operation:

```bash
CONFIRM_DATABASE_RESTORE=RESTORE ./scripts/restore-postgres.sh /absolute/path/to/backup.dump
```

The script uses `--clean --if-exists`; it can overwrite the selected database. Run migrations only if the restored release requires them, start the matching API image, and complete authentication/project/chat integrity checks before reopening traffic.
