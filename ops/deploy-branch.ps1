param(
  [string]$Branch,
  [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

Set-Location (Join-Path $PSScriptRoot "..")

if (-not $Branch -or [string]::IsNullOrWhiteSpace($Branch)) {
  $Branch = (git rev-parse --abbrev-ref HEAD).Trim()
}

if ([string]::IsNullOrWhiteSpace($Branch)) {
  throw "Unable to determine branch name. Pass -Branch explicitly."
}

$slug = $Branch.ToLowerInvariant() -replace "[^a-z0-9-]", "-"
$slug = $slug -replace "-{2,}", "-"
$slug = $slug.Trim("-")
if ([string]::IsNullOrWhiteSpace($slug)) { $slug = "branch" }

$sha = [System.Security.Cryptography.SHA256]::Create()
$bytes = [System.Text.Encoding]::UTF8.GetBytes($Branch)
$hash = $sha.ComputeHash($bytes)
$slot = ([BitConverter]::ToUInt16($hash, 0) % 700)

$frontendPort = 3100 + $slot
$apiPort = 5100 + $slot
$projectName = "deployflow-$slug"

$branchEnvPath = Join-Path (Get-Location) ".env.branch"
$branchEnv = @(
  "BRANCH_NAME=$Branch"
  "BRANCH_SLUG=$slug"
  "COMPOSE_PROJECT_NAME=$projectName"
  "FRONTEND_HOST_PORT=$frontendPort"
  "API_HOST_PORT=$apiPort"
  "ALLOWED_ORIGINS_OVERRIDE=http://localhost:$frontendPort"
  "NEXT_PUBLIC_API_URL=/proxy"
  "NEXT_PUBLIC_WS_URL="
)

Set-Content -Path $branchEnvPath -Value ($branchEnv -join "`n") -Encoding ascii

$composeArgs = @(
  "compose"
  "--env-file", ".env.branch"
  "-f", "docker-compose.yml"
  "-f", "docker-compose.branch.override.yml"
  "up", "-d"
)

if (-not $NoBuild) {
  $composeArgs += "--build"
}

Write-Host "Deploying branch '$Branch' as project '$projectName'" -ForegroundColor Cyan
Write-Host "Frontend: http://localhost:$frontendPort" -ForegroundColor Green
Write-Host "API:      http://localhost:$apiPort" -ForegroundColor Green

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
  Write-Error "Docker CLI is not available in this shell. Install Docker Desktop/Engine or run this script on the deploy host."
  exit 1
}

& docker @composeArgs

Write-Host "`nDeployment complete." -ForegroundColor Green
Write-Host "Run: .\ops\check-branch-health.ps1 -ComposeProjectName $projectName -FrontendPort $frontendPort -ApiPort $apiPort"
