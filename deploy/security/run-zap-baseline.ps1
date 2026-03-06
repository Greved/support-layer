param(
    [Parameter(Mandatory = $true)]
    [string]$TargetUrl,

    [int]$MaxMinutes = 5,

    [int]$TotalMaxMinutes = 5,

    [string]$OutputDir = "artifacts/zap"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is required to run ZAP baseline."
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$resolvedOutput = (Resolve-Path $OutputDir).Path
$workspace = (Get-Location).Path

Write-Host "Running ZAP baseline scan..."
Write-Host "Target: $TargetUrl"
Write-Host "Output: $resolvedOutput"

docker run --rm `
    -v "${workspace}:/zap/wrk/:rw" `
    ghcr.io/zaproxy/zaproxy:stable `
    zap-baseline.py `
    -t "$TargetUrl" `
    -m "$MaxMinutes" `
    -T "$TotalMaxMinutes" `
    -J "$OutputDir/zap-baseline.json" `
    -r "$OutputDir/zap-baseline.html" `
    -w "$OutputDir/zap-baseline.md" `
    -x "$OutputDir/zap-baseline.xml" | Out-Host

$jsonPath = Join-Path $resolvedOutput "zap-baseline.json"
if (-not (Test-Path $jsonPath)) {
    throw "Expected ZAP report not found: $jsonPath"
}

$report = Get-Content $jsonPath -Raw | ConvertFrom-Json
$counts = @{
    High = 0
    Medium = 0
    Low = 0
    Info = 0
}

foreach ($site in $report.site) {
    foreach ($alert in $site.alerts) {
        $instances = [int]$alert.count
        $riskCode = [int]$alert.riskcode
        switch ($riskCode) {
            3 { $counts.High += $instances }
            2 { $counts.Medium += $instances }
            1 { $counts.Low += $instances }
            default { $counts.Info += $instances }
        }
    }
}

Write-Host "ZAP summary: High=$($counts.High), Medium=$($counts.Medium), Low=$($counts.Low), Info=$($counts.Info)"

if ($counts.High -gt 0 -or $counts.Medium -gt 0) {
    throw "Phase 5 gate failed: ZAP High/Medium findings detected."
}

Write-Host "Phase 5 gate passed: no High/Medium findings."
