param(
    [string]$BasePublic = "http://localhost:5000",
    [string]$BasePortal = "http://localhost:5001",
    [string]$BaseAdmin = "http://localhost:5002",
    [string]$TestApiKey = "sl_live_test_key",
    [string]$OutDir = "k6/results"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command k6 -ErrorAction SilentlyContinue)) {
    throw "k6 is not installed or not in PATH."
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

$env:BASE_PUBLIC = $BasePublic
$env:BASE_PORTAL = $BasePortal
$env:BASE_ADMIN = $BaseAdmin
$env:TEST_API_KEY = $TestApiKey

Write-Host "Running widget chat scenario..."
k6 run --summary-export "$OutDir/widget-chat-$timestamp.json" "k6/scenarios/widget-chat.js"

Write-Host "Running PDF upload scenario..."
k6 run --summary-export "$OutDir/pdf-upload-$timestamp.json" "k6/scenarios/pdf-upload.js"

Write-Host "Running admin dashboard scenario..."
k6 run --summary-export "$OutDir/admin-dashboard-$timestamp.json" "k6/scenarios/admin-dashboard.js"

Write-Host "Done. Summaries saved to $OutDir"
