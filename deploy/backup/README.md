# Postgres Backup Runbook

## Create backup

```powershell
pwsh ./deploy/backup/pg_backup.ps1 `
  -DbHost localhost `
  -DbPort 5433 `
  -DbName supportlayer `
  -DbUser supportlayer `
  -DbPassword supportlayer `
  -OutputDir artifacts/backups `
  -RetentionDays 30
```

Optional S3 upload:

```powershell
$env:BACKUP_S3_BUCKET = "my-backups"
$env:BACKUP_S3_PREFIX = "supportlayer/prod"
pwsh ./deploy/backup/pg_backup.ps1
```

## Restore verification

```powershell
pwsh ./deploy/backup/pg_restore_verify.ps1 `
  -BackupPath artifacts/backups/supportlayer-20260306-010000.dump `
  -DbHost localhost `
  -DbPort 5433 `
  -DbName supportlayer `
  -DbUser supportlayer `
  -DbPassword supportlayer
```

By default the temporary restore database is dropped after verification.
