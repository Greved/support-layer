param(
    [string]$DbHost = $env:PGHOST,
    [string]$DbPort = $(if ($env:PGPORT) { $env:PGPORT } else { "5433" }),
    [string]$DbName = $(if ($env:PGDATABASE) { $env:PGDATABASE } else { "supportlayer" }),
    [string]$DbUser = $(if ($env:PGUSER) { $env:PGUSER } else { "supportlayer" }),
    [string]$DbPassword = $(if ($env:PGPASSWORD) { $env:PGPASSWORD } else { "supportlayer" }),
    [string]$OutputDir = "artifacts/backups",
    [int]$RetentionDays = 30,
    [string]$S3Bucket = $env:BACKUP_S3_BUCKET,
    [string]$S3Prefix = $env:BACKUP_S3_PREFIX
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($DbHost)) {
    $DbHost = "localhost"
}

if (-not (Get-Command pg_dump -ErrorAction SilentlyContinue)) {
    throw "pg_dump is not available on PATH."
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupFile = Join-Path $OutputDir "$DbName-$timestamp.dump"

$env:PGPASSWORD = $DbPassword

$dumpArgs = @(
    "--host", $DbHost,
    "--port", $DbPort,
    "--username", $DbUser,
    "--format=custom",
    "--no-owner",
    "--no-privileges",
    "--file", $backupFile,
    $DbName
)

Write-Host "Creating backup: $backupFile"
& pg_dump @dumpArgs
if ($LASTEXITCODE -ne 0) {
    throw "pg_dump failed with exit code $LASTEXITCODE"
}

if (-not [string]::IsNullOrWhiteSpace($S3Bucket)) {
    if (-not (Get-Command aws -ErrorAction SilentlyContinue)) {
        throw "aws CLI is required for S3 upload but was not found on PATH."
    }

    $prefix = if ([string]::IsNullOrWhiteSpace($S3Prefix)) { "" } else { "$($S3Prefix.Trim('/'))/" }
    $destination = "s3://$S3Bucket/$prefix$([IO.Path]::GetFileName($backupFile))"
    Write-Host "Uploading backup to $destination"
    & aws s3 cp $backupFile $destination
    if ($LASTEXITCODE -ne 0) {
        throw "aws s3 cp failed with exit code $LASTEXITCODE"
    }
}

$cutoff = (Get-Date).AddDays(-$RetentionDays)
$oldBackups = Get-ChildItem -Path $OutputDir -Filter "$DbName-*.dump" -File |
    Where-Object { $_.LastWriteTime -lt $cutoff }

foreach ($file in $oldBackups) {
    Remove-Item -Path $file.FullName -Force
    Write-Host "Removed expired backup: $($file.FullName)"
}

Write-Host "Backup completed successfully."
Write-Host "Backup file: $backupFile"
