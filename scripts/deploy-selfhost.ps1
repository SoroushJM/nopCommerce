[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Madadrang','MishaShop')][string]$Theme,
    [string]$ConfigPath, [string]$OutputDirectory, [string]$Server, [string]$SshUser,
    [ValidateRange(0,65535)][int]$SshPort = 0,
    [string]$IdentityFile, [string]$KnownHostsPath, [string]$RemoteRoot,
    [string]$EnvironmentFile,
    [ValidateRange(30,1800)][int]$StartupTimeout = 240,
    [switch]$KeepBuildArtifacts, [switch]$NonInteractive
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSVersion -lt [version]'7.4') { throw 'PowerShell 7.4 or newer is required.' }
foreach ($command in @('docker','ssh','scp','dotnet','git','npm')) { Get-Command $command -ErrorAction Stop | Out-Null }
$private = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'NopStorefrontDeploy'
. (Join-Path $PSScriptRoot 'resolve-storefront-deployment.ps1')
$effective = Resolve-StorefrontDeploymentInput -ConfigPath $ConfigPath -PrivateDirectory $private -Server $Server -SshUser $SshUser -SshPort $SshPort -IdentityFile $IdentityFile -KnownHostsPath $KnownHostsPath -RemoteRoot $RemoteRoot -NonInteractive:$NonInteractive
$configuration = Get-Content -LiteralPath $effective -Raw | ConvertFrom-Json
foreach ($path in @($configuration.keyFilename, $configuration.knownHostsPath)) {
    if ($path -and !(Test-Path -LiteralPath $path -PathType Leaf)) { throw 'SSH private-key or known_hosts file is missing.' }
}
if (!$configuration.keyFilename) { throw 'Self-host deployment uses an SSH key; supply -IdentityFile. The legacy test-server script also supports password handoffs.' }
if ($configuration.host -notmatch '^[A-Za-z0-9.:-]+$' -or $configuration.username -notmatch '^[A-Za-z0-9_.-]+$' -or $configuration.remoteRoot -notmatch '^/[A-Za-z0-9_./-]+$' -or $configuration.remoteRoot -match '(^|/)\.\.?(/|$)' -or $configuration.remoteRoot.TrimEnd('/') -in @('','/root','/home')) { throw 'Invalid server, username or dedicated remote project directory.' }
$remote = $configuration.remoteRoot.TrimEnd('/')
$target = "$($configuration.username)@$($configuration.host)"
$sshOptions = @('-i',$configuration.keyFilename,'-o','BatchMode=yes','-o','StrictHostKeyChecking=yes','-o',"UserKnownHostsFile=$($configuration.knownHostsPath)")
function Invoke-Server([string]$Command) {
    & ssh @sshOptions -p $configuration.port $target $Command
    if ($LASTEXITCODE -ne 0) { throw 'Remote deployment command failed. Inspect the retained release files.' }
}
function Copy-ToServer([string[]]$Files,[string]$Destination) {
    & scp @sshOptions -P $configuration.port @Files "${target}:$Destination"
    if ($LASTEXITCODE -ne 0) { throw 'SCP transfer failed; no successful deployment was reported.' }
}
$slug = $Theme.ToLowerInvariant()
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $private 'selfhost-runs' }
$run = Join-Path $OutputDirectory "$slug-$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))-$([guid]::NewGuid().ToString('N').Substring(0,8))"
if ([IO.Path]::GetFullPath($run).StartsWith($repo + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Build artifacts must be outside the checkout.' }
New-Item -ItemType Directory -Path $run -Force | Out-Null
# Check connectivity, host-key verification and private environment before building.
Invoke-Server "set -e; mkdir -p '$remote'; command -v docker >/dev/null; command -v python3 >/dev/null; command -v flock >/dev/null; command -v install >/dev/null; docker compose version >/dev/null"
Invoke-Server "if test -f '$remote/compose.yml' || { test -f '$remote/docker-compose.yml' && test ! -f '$remote/.storefront-selfhost'; }; then echo 'Existing deployment layout requires explicit migration; choose a dedicated project directory or use deploy-storefront.ps1 for the existing test server.' >&2; exit 1; fi"
& ssh @sshOptions -p $configuration.port $target "test -s '$remote/.env'"
if ($LASTEXITCODE -ne 0 -and !$EnvironmentFile) {
    if ($NonInteractive) { throw 'A fresh remote project needs -EnvironmentFile. See docs/smsir-selfhost.md.' }
    $EnvironmentFile = Read-Host 'Path to the private .env for this shop (created from .env.example)'
}
if ($EnvironmentFile) {
    if (!(Test-Path -LiteralPath $EnvironmentFile -PathType Leaf)) { throw 'Environment file does not exist.' }
}
& docker info --format '{{.OSType}}' 2>$null | Tee-Object -Variable engine | Out-Null
if ($LASTEXITCODE -ne 0 -or $engine -ne 'linux') { throw 'Start Docker Desktop with its Linux engine, then rerun the command.' }
& (Join-Path $PSScriptRoot 'publish-storefront.ps1') -Theme $Theme -Environment production -RuntimeIdentifier linux-x64 -OutputDirectory $run
$packages = @(Get-ChildItem -LiteralPath $run -Directory -Filter "$slug-production-*")
if ($packages.Count -ne 1) { throw 'Expected exactly one isolated publish package.' }
$package = $packages[0].FullName
$manifest = Get-Content -LiteralPath (Join-Path $package 'package.json') -Raw | ConvertFrom-Json
$image = "nop-storefront:$slug-$($manifest.SourceCommit.Substring(0,12))-$([guid]::NewGuid().ToString('N').Substring(0,8))"
Copy-Item -LiteralPath (Join-Path $repo 'scripts/DeployTestServer/Dockerfile') -Destination (Join-Path $package 'Dockerfile')
[IO.File]::WriteAllText((Join-Path $package '.dockerignore'),"**`n!Dockerfile`n!app/`n!app/**`n")
& docker build --platform linux/amd64 --label "com.nop.storefront.theme=$Theme" --tag $image $package
if ($LASTEXITCODE -ne 0) { throw 'Linux image build failed.' }
$archive = Join-Path $run 'image.tar'
& docker image save --output $archive $image
if ($LASTEXITCODE -ne 0) { throw 'Image export failed.' }
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
$release = "$remote/releases/$([guid]::NewGuid().ToString('N'))"
Invoke-Server "mkdir -p '$release/docker' '$release/scripts'; chmod 700 '$release'"
Copy-ToServer @($archive) "$release/image.tar"
Copy-ToServer @((Join-Path $repo 'docker-compose.yml'),(Join-Path $repo 'Dockerfile')) "$release/"
Copy-ToServer @((Join-Path $repo 'docker/postgres-init.sql'),(Join-Path $repo 'docker/create-certificate.sh'),(Join-Path $repo 'docker/activate-storefront-release.sh')) "$release/docker/"
Copy-ToServer @((Join-Path $repo 'scripts/deploy-storefront.sh'),(Join-Path $repo "scripts/deploy-$slug.sh")) "$release/scripts/"
if ($EnvironmentFile) { Copy-ToServer @($EnvironmentFile) "$release/.env"; Invoke-Server "chmod 600 '$release/.env'" }
Invoke-Server "bash '$release/docker/activate-storefront-release.sh' '$remote' '$release' '$Theme' '$image' '$hash' '$StartupTimeout'"
Invoke-Server "rm '$release/image.tar'"
@{ Theme=$Theme; Image=$image; ArchiveSha256=$hash; RemoteRoot=$remote; SourceCommit=$manifest.SourceCommit; Status='success' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'deployment.json')
Write-Host "Deployment completed for $Theme. Receipt: $run/deployment.json"
if (!$KeepBuildArtifacts) { Remove-Item -LiteralPath $archive -Force }
