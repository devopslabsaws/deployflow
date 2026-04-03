param(
  [string]$TargetHost = "localhost",
  [int]$FrontendPort,
  [int]$ApiPort,
  [string]$ComposeProjectName
)

$ErrorActionPreference = "Stop"

Set-Location (Join-Path $PSScriptRoot "..")

if (($FrontendPort -le 0 -or $ApiPort -le 0) -and (Test-Path ".env.branch")) {
  $envLines = Get-Content ".env.branch"
  foreach ($line in $envLines) {
    if ($line -match "^FRONTEND_HOST_PORT=(\d+)$" -and $FrontendPort -le 0) { $FrontendPort = [int]$Matches[1] }
    if ($line -match "^API_HOST_PORT=(\d+)$" -and $ApiPort -le 0) { $ApiPort = [int]$Matches[1] }
    if ($line -match "^COMPOSE_PROJECT_NAME=(.+)$" -and [string]::IsNullOrWhiteSpace($ComposeProjectName)) { $ComposeProjectName = $Matches[1] }
  }
}

if ($FrontendPort -le 0 -or $ApiPort -le 0) {
  throw "Frontend/API ports are required. Pass -FrontendPort and -ApiPort, or ensure .env.branch exists."
}

if ([string]::IsNullOrWhiteSpace($ComposeProjectName)) {
  $ComposeProjectName = "deployflow-branch"
}

$ok = $true

Write-Host "== Docker Compose Status ==" -ForegroundColor Cyan
try {
  docker compose --project-name $ComposeProjectName -f docker-compose.yml -f docker-compose.branch.override.yml ps
} catch {
  Write-Host "Failed to fetch compose status for project '$ComposeProjectName'." -ForegroundColor Yellow
  $ok = $false
}

Write-Host "`n== Port Binding Checks ==" -ForegroundColor Cyan
$frontendPortCheck = Test-NetConnection -ComputerName $TargetHost -Port $FrontendPort -WarningAction SilentlyContinue
$apiPortCheck = Test-NetConnection -ComputerName $TargetHost -Port $ApiPort -WarningAction SilentlyContinue

if ($frontendPortCheck.TcpTestSucceeded) {
  Write-Host "Frontend port open: ${TargetHost}:$FrontendPort" -ForegroundColor Green
} else {
  Write-Host "Frontend port closed: ${TargetHost}:$FrontendPort" -ForegroundColor Red
  $ok = $false
}

if ($apiPortCheck.TcpTestSucceeded) {
  Write-Host "API port open:      ${TargetHost}:$ApiPort" -ForegroundColor Green
} else {
  Write-Host "API port closed:    ${TargetHost}:$ApiPort" -ForegroundColor Red
  $ok = $false
}

Write-Host "`n== HTTP Health Checks ==" -ForegroundColor Cyan
try {
  $apiHealth = Invoke-WebRequest -Uri "http://$TargetHost`:$ApiPort/health" -UseBasicParsing -TimeoutSec 10
  Write-Host "API health status:  $($apiHealth.StatusCode)" -ForegroundColor Green
} catch {
  Write-Host "API health failed:  $($_.Exception.Message)" -ForegroundColor Red
  $ok = $false
}

try {
  $frontendHealth = Invoke-WebRequest -Uri "http://$TargetHost`:$FrontendPort/" -UseBasicParsing -TimeoutSec 10
  Write-Host "Frontend status:    $($frontendHealth.StatusCode)" -ForegroundColor Green
} catch {
  Write-Host "Frontend check failed: $($_.Exception.Message)" -ForegroundColor Red
  $ok = $false
}

Write-Host "`n== Result ==" -ForegroundColor Cyan
if ($ok) {
  Write-Host "PASS: Service ports and HTTP checks are healthy." -ForegroundColor Green
  Write-Host "Open: http://$TargetHost`:$FrontendPort"
  exit 0
}

Write-Host "FAIL: One or more checks failed. Do not open URL yet." -ForegroundColor Red
exit 1
