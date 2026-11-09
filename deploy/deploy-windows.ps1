<#
.SYNOPSIS
  Build FreelanceFlow on Windows and deploy it to an Ubuntu server over SSH (no Docker).

.DESCRIPTION
  Reads deploy\freelanceflow.conf (same file as setup-ubuntu.sh), then:
    1. checks prerequisites (dotnet SDK >= 9, node/npm, tar.exe, ssh/scp)
    2. builds the SPA (npm ci + npm run build) and publishes the API (linux-x64, framework-dependent)
    3. packages  app/ (API)  web/ (SPA)  manifest.json  into one .tar.gz under deploy\.build\
    4. uploads setup-ubuntu.sh (+ the config for setup) and the archive with scp
    5. runs  sudo bash setup-ubuntu.sh setup ...  (first time / -FirstTime) or  deploy <archive>  over ssh -t
    6. shows the health check
  Windows PowerShell 5.1 compatible; uses the built-in OpenSSH client and %SystemRoot%\System32\tar.exe.

.PARAMETER ConfigPath
  Config file (default: deploy\freelanceflow.conf next to this script).
.PARAMETER FirstTime
  Run the full idempotent server setup (install everything + deploy). Auto-detected when the server has
  never been set up. Also use it after changing the config (domain, email, limits, webhook, ...).
.PARAMETER SkipBuild
  Re-use the newest archive in deploy\.build\ instead of building.
.PARAMETER SkipUpload
  Build and package only (no SSH). Prints the archive path.
.PARAMETER Action
  deploy (default) | setup | status | logs | rollback | backup

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File deploy\deploy-windows.ps1 -FirstTime
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File deploy\deploy-windows.ps1
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File deploy\deploy-windows.ps1 -Action status
#>
[CmdletBinding()]
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'freelanceflow.conf'),
    [switch]$FirstTime,
    [switch]$SkipBuild,
    [switch]$SkipUpload,
    [ValidateSet('deploy', 'setup', 'status', 'logs', 'rollback', 'backup')]
    [string]$Action = 'deploy'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$RepoRoot  = Split-Path $PSScriptRoot -Parent
$BuildRoot = Join-Path $PSScriptRoot '.build'
$TarExe    = Join-Path $env:SystemRoot 'System32\tar.exe'   # bsdtar; Git's GNU tar would read C: as a host

function Write-Step([string]$Message) { Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Ok([string]$Message)   { Write-Host "  ok $Message" -ForegroundColor Green }
function Write-Warn2([string]$Message) { Write-Host "WARN $Message" -ForegroundColor Yellow }
function Fail([string]$Message) { throw $Message }

# Run a native command; throw when it exits non-zero
function Invoke-Native {
    param([Parameter(Mandatory = $true)][string]$Exe, [string[]]$Arguments = @())
    & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) { Fail ("'{0} {1}' failed with exit code {2}" -f $Exe, ($Arguments -join ' '), $LASTEXITCODE) }
}

# Same rules as parse_config in setup-ubuntu.sh: KEY=value, optional quotes, no evaluation
function Read-FfConfig([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail ("Config not found: {0}`n  Copy deploy\freelanceflow.conf.example to deploy\freelanceflow.conf and fill it in." -f $Path)
    }
    $cfg = @{}
    $n = 0
    foreach ($line in (Get-Content -LiteralPath $Path -Encoding UTF8)) {
        $n++
        if ($line -match '^\s*(#.*)?$') { continue }
        if ($line -notmatch '^\s*([A-Z][A-Z0-9_]*)\s*=\s*(.*)$') { Fail ("{0}:{1}: expected KEY=value" -f $Path, $n) }
        $key = $Matches[1]
        $val = $Matches[2]
        if ($val -match '^"([^"]*)"\s*(#.*)?$') { $val = $Matches[1] }
        elseif ($val -match "^'([^']*)'\s*(#.*)?$") { $val = $Matches[1] }
        elseif ($val -match '^([^\s#]*)\s*(#.*)?$') { $val = $Matches[1] }
        else { Fail ("{0}:{1}: unquoted value of {2} contains spaces" -f $Path, $n, $key) }
        $cfg[$key] = $val
    }
    return $cfg
}

function Get-Cfg([hashtable]$Cfg, [string]$Key, [string]$Default = '') {
    if ($Cfg.ContainsKey($Key) -and $Cfg[$Key] -ne '') { return [string]$Cfg[$Key] }
    return $Default
}

function Test-Tool([string]$Name) { return [bool](Get-Command $Name -ErrorAction SilentlyContinue) }

# Copy a text file with LF line endings and no BOM (bash on the server chokes on CRLF)
function Copy-AsLf([string]$Source, [string]$Destination) {
    $text = [IO.File]::ReadAllText($Source)
    $text = $text -replace "`r`n", "`n"
    [IO.File]::WriteAllText($Destination, $text, (New-Object Text.UTF8Encoding $false))
}

function Get-GitValue([string[]]$GitArgs, [string]$Default) {
    if (-not (Test-Tool 'git')) { return $Default }
    # PS 5.1 turns redirected native stderr into errors; with 'Stop' that would throw
    $ErrorActionPreference = 'Continue'
    $out = & git -C $RepoRoot @GitArgs 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $out) { return $Default }
    return (($out | Out-String).Trim())
}

# ------------------------------------------------------------------------------ build
function Test-BuildPrerequisites {
    Write-Step 'Checking build prerequisites'
    if (-not (Test-Tool 'dotnet')) { Fail '.NET SDK not found. Install the .NET 9 (or newer) SDK: https://dotnet.microsoft.com/download' }
    $sdks = @(& dotnet --list-sdks)
    $majors = @($sdks | ForEach-Object { if ($_ -match '^(\d+)\.') { [int]$Matches[1] } })
    if (-not ($majors | Where-Object { $_ -ge 9 })) { Fail '.NET SDK 9 or newer is required (dotnet --list-sdks shows none).' }
    if (-not (Test-Tool 'node')) { Fail 'Node.js not found. Install Node.js 20+ (includes npm): https://nodejs.org' }
    # npm.cmd, not the npm.ps1 shim: that shim fails under this script's Set-StrictMode
    $npmCmd = Get-Command 'npm.cmd' -ErrorAction SilentlyContinue
    if (-not $npmCmd) { Fail 'npm not found (comes with Node.js).' }
    $script:NpmExe = $npmCmd.Source
    if (-not (Test-Path -LiteralPath $TarExe)) { Fail "tar.exe not found at $TarExe (Windows 10 1803+ ships it)." }
    Write-Ok ("dotnet SDK {0}, node {1}, tar.exe" -f (($majors | Sort-Object -Descending)[0]), (& node --version))
}

function New-ReleaseArchive {
    Test-BuildPrerequisites
    $stage = Join-Path $BuildRoot 'stage'
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    New-Item -ItemType Directory -Force -Path (Join-Path $stage 'app'), (Join-Path $stage 'web') | Out-Null

    $commit = Get-GitValue @('rev-parse', '--short', 'HEAD') 'unknown'
    $branch = Get-GitValue @('rev-parse', '--abbrev-ref', 'HEAD') 'unknown'
    $dirty  = (Get-GitValue @('status', '--porcelain') '') -ne ''
    if ($dirty) { Write-Warn2 'Working tree has uncommitted changes - they are included in this build (manifest: dirty=true).' }

    # Build from a copy: `npm ci` wipes node_modules, which breaks (or is blocked by) a running `npm run dev`
    # in frontend/, and the dev tree's dist/ stays untouched. The copy keeps its own node_modules.
    Write-Step 'Building the frontend in deploy\.build\frontend (npm ci + npm run build)'
    $frontendSrc = Join-Path $RepoRoot 'frontend'
    $frontend = Join-Path $BuildRoot 'frontend'
    & robocopy $frontendSrc $frontend /MIR /XD node_modules dist playwright-report test-results .vite /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { Fail "robocopy of frontend/ failed (exit $LASTEXITCODE)" }
    Push-Location $frontend
    try {
        Invoke-Native $NpmExe @('ci', '--no-audit', '--no-fund')
        Invoke-Native $NpmExe @('run', 'build')
    } finally { Pop-Location }
    $dist = Join-Path $frontend 'dist'
    if (-not (Test-Path -LiteralPath (Join-Path $dist 'index.html'))) { Fail 'dist/index.html missing after the frontend build.' }
    Copy-Item -Path (Join-Path $dist '*') -Destination (Join-Path $stage 'web') -Recurse -Force

    # --artifacts-path keeps the linux-x64 restore/obj out of backend/ (a running `dotnet run` is not disturbed)
    Write-Step 'Publishing the API (Release, linux-x64, framework-dependent)'
    $csproj = Join-Path $RepoRoot 'backend\src\FreelanceFlow.Api\FreelanceFlow.Api.csproj'
    Invoke-Native 'dotnet' @('publish', $csproj, '-c', 'Release', '-r', 'linux-x64', '--self-contained', 'false',
        '--artifacts-path', (Join-Path $BuildRoot 'dotnet'), '-o', (Join-Path $stage 'app'), '-nologo')
    # Development settings may hold local secrets (and must never configure production)
    Get-ChildItem -LiteralPath (Join-Path $stage 'app') -Filter 'appsettings.Development*.json' | Remove-Item -Force
    if (-not (Test-Path -LiteralPath (Join-Path $stage 'app\FreelanceFlow.Api.dll'))) { Fail 'FreelanceFlow.Api.dll missing after publish.' }

    $builtAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    $manifest = [ordered]@{ app = 'FreelanceFlow'; commit = $commit; branch = $branch; dirty = $dirty; builtAt = $builtAt }
    [IO.File]::WriteAllText((Join-Path $stage 'manifest.json'), ((New-Object PSObject -Property $manifest) | ConvertTo-Json),
        (New-Object Text.UTF8Encoding $false))

    $stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
    $archive = Join-Path $BuildRoot ("freelanceflow-{0}-{1}.tar.gz" -f $stamp, $commit)
    Write-Step 'Packaging'
    Invoke-Native $TarExe @('-czf', $archive, '-C', $stage, 'app', 'web', 'manifest.json')
    $listing = @(& $TarExe -tzf $archive)
    foreach ($required in @('app/FreelanceFlow.Api.dll', 'web/index.html', 'manifest.json')) {
        if ($listing -notcontains $required) { Fail "Archive is missing $required" }
    }
    if ($listing | Where-Object { $_ -like 'app/appsettings.Development*' }) { Fail 'Archive contains appsettings.Development.json' }
    Remove-Item -LiteralPath $stage -Recurse -Force
    # keep the 3 newest archives
    Get-ChildItem -LiteralPath $BuildRoot -Filter 'freelanceflow-*.tar.gz' | Sort-Object LastWriteTime -Descending |
        Select-Object -Skip 3 | Remove-Item -Force
    $sizeMb = [math]::Round((Get-Item -LiteralPath $archive).Length / 1MB, 1)
    Write-Ok ("{0} ({1} MB, {2} files, commit {3})" -f $archive, $sizeMb, $listing.Count, $commit)
    # not `return`: npm/dotnet output is part of this function's output stream, so the caller reads this instead
    $script:ArchivePath = $archive
}

# ------------------------------------------------------------------------------ main
$cfg = Read-FfConfig $ConfigPath
$domain = Get-Cfg $cfg 'DOMAIN'
if (-not $domain) { Fail "DOMAIN is required in $ConfigPath" }
if (Get-Cfg $cfg 'LETSENCRYPT_EMAIL') { $publicUrl = "https://$domain" } else { $publicUrl = "http://$domain" }

if ($FirstTime) { $Action = 'setup' }
$needsArchive = ($Action -eq 'deploy' -or $Action -eq 'setup')

New-Item -ItemType Directory -Force -Path $BuildRoot | Out-Null
$archive = $null
if ($needsArchive) {
    if ($SkipBuild) {
        $latest = Get-ChildItem -LiteralPath $BuildRoot -Filter 'freelanceflow-*.tar.gz' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $latest) { Fail "-SkipBuild: no archive in $BuildRoot - build once without -SkipBuild." }
        $archive = $latest.FullName
        Write-Ok "re-using $archive"
    } else {
        New-ReleaseArchive
        $archive = $script:ArchivePath
    }
}
if ($SkipUpload) {
    Write-Host ''
    Write-Host "Archive ready (not uploaded): $archive" -ForegroundColor Green
    return
}

# --- SSH
$sshHost = Get-Cfg $cfg 'SSH_HOST'
$sshUser = Get-Cfg $cfg 'SSH_USER'
$sshPort = Get-Cfg $cfg 'SSH_PORT' '22'
$sshKey  = Get-Cfg $cfg 'SSH_KEY_PATH'
$remoteDir = Get-Cfg $cfg 'REMOTE_UPLOAD_DIR' 'freelanceflow-deploy'
if (-not $sshHost -or -not $sshUser) { Fail "SSH_HOST and SSH_USER are required in $ConfigPath" }
if ($remoteDir -notmatch '^[A-Za-z0-9._-]+$') { Fail 'REMOTE_UPLOAD_DIR must be a plain folder name (it lives in the SSH user home).' }
if (-not (Test-Tool 'ssh') -or -not (Test-Tool 'scp')) {
    Fail 'OpenSSH client not found. Settings > Apps > Optional features > add "OpenSSH Client".'
}
$common = @('-o', 'StrictHostKeyChecking=accept-new', '-o', 'ServerAliveInterval=30')
if ($sshKey) {
    if (-not (Test-Path -LiteralPath $sshKey)) { Fail "SSH_KEY_PATH not found: $sshKey" }
    $common += @('-i', $sshKey)
}
$sshArgs = @('-p', $sshPort) + $common
$scpArgs = @('-P', $sshPort) + $common
$target = "$sshUser@$sshHost"
if ($sshUser -eq 'root') { $sudo = '' } else { $sudo = 'sudo ' }

# Always call as a statement (never `$x = Invoke-Remote ...`): capturing the output would redirect ssh's
# stdout and hide the sudo password prompt. The exit code lands in $script:RemoteExit.
$script:RemoteExit = 0
function Invoke-Remote([string]$Command, [switch]$Tty, [switch]$AllowFail) {
    $a = @($sshArgs)
    if ($Tty) { $a += '-t' }
    $a += @($target, $Command)
    & ssh @a
    $script:RemoteExit = $LASTEXITCODE
    if (-not $AllowFail -and $script:RemoteExit -ne 0) { Fail "Remote command failed (exit $script:RemoteExit)" }
}

Write-Step "Connecting to $target (port $sshPort)"
Invoke-Remote 'true'
Write-Ok 'SSH works'

if ($Action -eq 'deploy' -and -not $FirstTime) {
    Invoke-Remote 'test -e /usr/local/sbin/freelanceflow' -AllowFail
    if ($script:RemoteExit -ne 0) {
        Write-Warn2 'Server has not been set up yet - running the full setup (-FirstTime).'
        $Action = 'setup'
    }
}

switch ($Action) {
    { $_ -eq 'setup' -or $_ -eq 'deploy' } {
        $archiveName = Split-Path $archive -Leaf
        $upload = Join-Path $BuildRoot 'upload'
        New-Item -ItemType Directory -Force -Path $upload | Out-Null
        Copy-AsLf (Join-Path $PSScriptRoot 'setup-ubuntu.sh') (Join-Path $upload 'setup-ubuntu.sh')
        $files = @((Join-Path $upload 'setup-ubuntu.sh'), $archive)
        if ($Action -eq 'setup') {
            Copy-AsLf $ConfigPath (Join-Path $upload 'freelanceflow.conf')
            $files += (Join-Path $upload 'freelanceflow.conf')
        }
        Write-Step "Uploading to ~/$remoteDir"
        Invoke-Remote ("mkdir -p ~/{0} && chmod 700 ~/{0}" -f $remoteDir)
        Invoke-Native 'scp' ($scpArgs + $files + @("${target}:$remoteDir/"))
        Remove-Item -LiteralPath (Join-Path $upload 'freelanceflow.conf') -Force -ErrorAction SilentlyContinue
        Write-Ok 'uploaded'

        # The uploaded config (it may hold secrets) and the archive are removed whatever the outcome
        if ($Action -eq 'setup') {
            Write-Step 'Running the full server setup (sudo may ask for your password)'
            $cmd = 'cd ~/{0} && {1}bash ./setup-ubuntu.sh --config ./freelanceflow.conf setup ./{2}; rc=$?; rm -f ./freelanceflow.conf ./freelanceflow-*.tar.gz; exit $rc' -f $remoteDir, $sudo, $archiveName
        } else {
            Write-Step 'Deploying the release (sudo may ask for your password)'
            $cmd = 'cd ~/{0} && {1}bash ./setup-ubuntu.sh deploy ./{2}; rc=$?; rm -f ./freelanceflow-*.tar.gz; exit $rc' -f $remoteDir, $sudo, $archiveName
        }
        Invoke-Remote $cmd -Tty -AllowFail
        if ($script:RemoteExit -ne 0) { Fail "Server step failed (exit $script:RemoteExit). Logs: deploy-windows.ps1 -Action logs" }
    }
    default {
        Invoke-Remote ('{0}freelanceflow {1}' -f $sudo, $Action) -Tty -AllowFail
        if ($script:RemoteExit -ne 0) { Fail "'freelanceflow $Action' failed (exit $script:RemoteExit). Has the server been set up (-FirstTime)?" }
        return
    }
}

# --- health
Write-Step 'Health check'
$port = Get-Cfg $cfg 'KESTREL_PORT' '5000'
Invoke-Remote ("curl -fsS --max-time 5 http://127.0.0.1:{0}/health; echo" -f $port) -AllowFail
if ($script:RemoteExit -eq 0) { Write-Ok 'API /health OK (on the server)' } else { Write-Warn2 'API /health failed on the server' }
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $resp = Invoke-WebRequest -Uri "$publicUrl/" -UseBasicParsing -TimeoutSec 15
    Write-Ok ("{0} -> HTTP {1}" -f $publicUrl, $resp.StatusCode)
} catch {
    Write-Warn2 ("{0} not reachable from this PC yet: {1}" -f $publicUrl, $_.Exception.Message)
}
Write-Host ''
Write-Host "Done. Open $publicUrl" -ForegroundColor Green
