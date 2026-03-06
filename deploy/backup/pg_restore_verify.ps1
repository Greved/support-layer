param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,
    [string]$DbHost = $env:PGHOST,
    [string]$DbPort = $(if ($env:PGPORT) { $env:PGPORT } else { "5433" }),
    [string]$DbName = $(if ($env:PGDATABASE) { $env:PGDATABASE } else { "supportlayer" }),
    [string]$DbUser = $(if ($env:PGUSER) { $env:PGUSER } else { "supportlayer" }),
    [string]$DbPassword = $(if ($env:PGPASSWORD) { $env:PGPASSWORD } else { "supportlayer" }),
    [string]$RestoreDbName = "",
    [switch]$KeepRestoreDatabase
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $BackupPath -PathType Leaf)) {
    throw "Backup file not found: $BackupPath"
}

if ([string]::IsNullOrWhiteSpace($DbHost)) {
    $DbHost = "localhost"
}

if ([string]::IsNullOrWhiteSpace($RestoreDbName)) {
    $RestoreDbName = "$DbName`_restore_check"
}

if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
    throw "psql is not available on PATH."
}

if (-not (Get-Command pg_restore -ErrorAction SilentlyContinue)) {
    throw "pg_restore is not available on PATH."
}

$env:PGPASSWORD = $DbPassword

Write-Host "Preparing restore database: $RestoreDbName"
& psql -h $DbHost -p $DbPort -U $DbUser -d postgres -v ON_ERROR_STOP=1 `
    -c "DROP DATABASE IF EXISTS ""$RestoreDbName"";" `
    -c "CREATE DATABASE ""$RestoreDbName"";"

if ($LASTEXITCODE -ne 0) {
    throw "Failed to prepare restore database."
}

Write-Host "Restoring backup into: $RestoreDbName"
& pg_restore -h $DbHost -p $DbPort -U $DbUser -d $RestoreDbName --no-owner --no-privileges $BackupPath
if ($LASTEXITCODE -ne 0) {
    throw "pg_restore failed with exit code $LASTEXITCODE"
}

$tableCountQuery = "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';"
$sourceTableCount = (& psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -t -A -c $tableCountQuery).Trim()
$restoredTableCount = (& psql -h $DbHost -p $DbPort -U $DbUser -d $RestoreDbName -t -A -c $tableCountQuery).Trim()

if ($sourceTableCount -ne $restoredTableCount) {
    throw "Restore verification failed: table count mismatch (source=$sourceTableCount, restored=$restoredTableCount)."
}

$rowEstimateQuery = "SELECT coalesce(sum(n_live_tup)::bigint,0) FROM pg_stat_user_tables;"
$sourceRows = (& psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -t -A -c $rowEstimateQuery).Trim()
$restoredRows = (& psql -h $DbHost -p $DbPort -U $DbUser -d $RestoreDbName -t -A -c $rowEstimateQuery).Trim()

if ([int64]$restoredRows -le 0 -and [int64]$sourceRows -gt 0) {
    throw "Restore verification failed: restored database appears empty."
}

Write-Host "Restore verification passed."
Write-Host "Table count: $restoredTableCount"
Write-Host "Estimated rows (source/restored): $sourceRows / $restoredRows"

if (-not $KeepRestoreDatabase.IsPresent) {
    Write-Host "Cleaning up restore database: $RestoreDbName"
    & psql -h $DbHost -p $DbPort -U $DbUser -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS ""$RestoreDbName"";"
}
