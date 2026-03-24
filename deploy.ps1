<#
.SYNOPSIS
    Deploy DeployFlow to the Azure VM (20.83.147.234).

.DESCRIPTION
    1. Copies source code to ~/deployflow on the remote server (via scp/ssh).
       Excludes build artefacts: node_modules, .next, bin, obj, .git, publish_temp*.
    2. Copies infra/server.env → ~/deployflow/.env
    3. Configures nginx and starts DeployFlow via docker compose.

.PARAMETER SshKey
    Path to your SSH private key.  Default: ~/.ssh/id_rsa

.PARAMETER SshUser
    SSH user on the remote server.  Default: azureuser

.EXAMPLE
    .\deploy.ps1
    .\deploy.ps1 -SshKey "C:\Users\you\.ssh\azure_key"
#>

param(
    [string]$SshKey  = "$env:USERPROFILE\.ssh\id_rsa",
    [string]$SshUser = "azureuser"
)

$SERVER = "20.83.147.234"
$REMOTE = "$SshUser@$SERVER"
$DEST   = "~/deployflow"
$ROOT   = $PSScriptRoot   # d:\LNT\Coolify

function Invoke-Ssh {
    param([string]$Cmd)
    Write-Host "  >> $Cmd" -ForegroundColor DarkGray
    if (Test-Path $SshKey) {
        ssh -i $SshKey -o StrictHostKeyChecking=no $REMOTE $Cmd
    } else {
        ssh -o StrictHostKeyChecking=no $REMOTE $Cmd
    }
}

function Invoke-Scp {
    param([string]$Src, [string]$Dst)
    Write-Host "  scp $Src -> $Dst" -ForegroundColor DarkGray
    if (Test-Path $SshKey) {
        scp -i $SshKey -r $Src "${REMOTE}:${Dst}"
    } else {
        scp -r $Src "${REMOTE}:${Dst}"
    }
}

Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  DeployFlow  ->  $SERVER" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# ── Step 1: Create directory structure on server ──────────────────────────────
Write-Host "`n[1/6] Creating remote directory $DEST..." -ForegroundColor Yellow
Invoke-Ssh "mkdir -p $DEST/backend $DEST/frontend $DEST/infra"

# ── Step 2: Copy source code ──────────────────────────────────────────────────
Write-Host "`n[2/6] Copying source code..." -ForegroundColor Yellow

# Build a temporary rsync-style exclusion by creating a zip of only the source
# On Windows we use scp; excluded dirs are copied separately to skip heavy folders.

# Copy backend (source only, skip bin/obj/publish_temp*)
Write-Host "  Copying backend/src..." -ForegroundColor Gray
Invoke-Scp "$ROOT\backend\src"                     "$DEST/backend/"
Invoke-Scp "$ROOT\backend\DeployFlow.sln"          "$DEST/backend/"

# Copy backend Dockerfile
Invoke-Scp "$ROOT\backend\src\DeployFlow.API\Dockerfile" "$DEST/backend/src/DeployFlow.API/"

# Copy frontend (source only, skip node_modules and .next)
Write-Host "  Copying frontend source files..." -ForegroundColor Gray
$frontendFiles = @(
    "app", "components", "hooks", "lib", "store", "types",
    "public",
    "package.json", "package-lock.json",
    "next.config.mjs", "tsconfig.json",
    "tailwind.config.ts", "postcss.config.js",
    "components.json", "next-env.d.ts",
    "Dockerfile"
)
foreach ($item in $frontendFiles) {
    $localPath = Join-Path "$ROOT\frontend" $item
    if (Test-Path $localPath) {
        Invoke-Scp $localPath "$DEST/frontend/"
    }
}

# Copy infra and compose files
Write-Host "  Copying infra + compose files..." -ForegroundColor Gray
Invoke-Scp "$ROOT\infra"           "$DEST/"
Invoke-Scp "$ROOT\docker-compose.yml" "$DEST/"

# ── Step 3: Upload .env ───────────────────────────────────────────────────────
Write-Host "`n[3/6] Uploading .env (secrets)..." -ForegroundColor Yellow
Invoke-Scp "$ROOT\infra\server.env" "$DEST/.env"
Write-Host "  .env uploaded." -ForegroundColor Green

# ── Step 4: Configure nginx ───────────────────────────────────────────────────
Write-Host "`n[4/6] Configuring nginx..." -ForegroundColor Yellow
Invoke-Ssh @"
sudo cp $DEST/infra/nginx/deployflow.conf /etc/nginx/sites-available/deployflow && \
sudo ln -sf /etc/nginx/sites-available/deployflow /etc/nginx/sites-enabled/deployflow && \
sudo rm -f /etc/nginx/sites-enabled/default && \
sudo nginx -t && sudo systemctl reload nginx
"@

# ── Step 5: Build & start containers ─────────────────────────────────────────
Write-Host "`n[5/6] Building and starting Docker containers..." -ForegroundColor Yellow
Write-Host "      Rebuilding api + frontend images; oracle/redis data volumes preserved." -ForegroundColor DarkYellow
Write-Host "      This will take 5-10 minutes on first build." -ForegroundColor DarkYellow
Invoke-Ssh "cd $DEST && docker compose build --no-cache api frontend && docker compose up -d --force-recreate api frontend oracle redis"

# ── Step 6: Health check ──────────────────────────────────────────────────────
Write-Host "`n[6/6] Waiting 60 seconds then checking container status..." -ForegroundColor Yellow
Invoke-Ssh "cd $DEST && sleep 60 && docker compose ps"
Invoke-Ssh "cd $DEST && docker compose ps | grep -E '(unhealthy|Exit|Restarting)' && docker compose logs --tail=40 api 2>&1 | tail -40 || echo 'All containers healthy.'"

Write-Host ""
Write-Host "==================================================" -ForegroundColor Green
Write-Host "  Deployment complete!" -ForegroundColor Green
Write-Host ""
Write-Host "  Frontend  : http://$SERVER" -ForegroundColor Green
Write-Host "  API       : http://$SERVER/api/health" -ForegroundColor Green
Write-Host ""
Write-Host "  Oracle takes ~3 min to initialise on first boot." -ForegroundColor DarkYellow
Write-Host "  Monitor:  ssh $REMOTE 'docker compose -f $DEST/docker-compose.yml logs -f'" -ForegroundColor DarkYellow
Write-Host "==================================================" -ForegroundColor Green
