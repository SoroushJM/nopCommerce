[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Madadrang', 'Boutique')][string]$Theme,
    [string]$ConfigPath,
    [string]$OutputDirectory,
    [string]$Python = 'python',
    [ValidateRange(30, 1800)][int]$StartupTimeout = 180,
    [switch]$KeepBuildArtifacts
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Run this script with PowerShell 7: pwsh -File scripts/deploy-madadrang.ps1 (or deploy-mishashop.ps1).' }
$Theme = if ($Theme -ieq 'Boutique') { 'Boutique' } else { 'Madadrang' }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$themeSlug = $Theme.ToLowerInvariant()
$privateDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'NopStorefrontDeploy'
if (!$ConfigPath) {
    $configCandidates = @(
        (Join-Path $privateDirectory 'deploy.private.json'),
        (Join-Path (Split-Path -Parent $repoRoot) 'dual-store-deployment-20261012/deploy.private.json')
    )
    $ConfigPath = $configCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (!$ConfigPath) { throw 'Private deployment configuration was not found. See docs/one-command-deployment.md for the one-time setup.' }
}
$ConfigPath = (Resolve-Path -LiteralPath $ConfigPath).Path
if ($ConfigPath.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Keep deployment credentials/configuration outside the Git checkout.' }
$settings = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
foreach ($required in @('host', 'username', 'knownHostsPath', 'remoteRoot')) {
    if (!$settings.PSObject.Properties[$required] -or [string]::IsNullOrWhiteSpace([string]$settings.$required)) { throw "Missing private configuration field: $required" }
}
$authenticationField = if ($settings.PSObject.Properties['keyFilename'] -and $settings.keyFilename) { 'keyFilename' } else { 'handoffPath' }
if (!$settings.PSObject.Properties[$authenticationField] -or !$settings.$authenticationField) { throw 'Configure an SSH keyFilename or a private handoffPath.' }
foreach ($requiredFile in @('knownHostsPath', $authenticationField)) {
    $configuredPath = [string]$settings.$requiredFile
    if (![IO.Path]::IsPathRooted($configuredPath)) { $configuredPath = Join-Path (Split-Path -Parent $ConfigPath) $configuredPath }
    if (!(Test-Path -LiteralPath $configuredPath -PathType Leaf)) { throw "Missing private configuration file: $requiredFile" }
}
$imageRepository = if ($settings.PSObject.Properties['imageRepository']) { [string]$settings.imageRepository } else { 'nop-dual' }
if ($imageRepository -notmatch '^[a-z0-9]+(?:[._/-][a-z0-9]+)*$') { throw 'imageRepository must be a lowercase Docker repository name without a tag.' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $privateDirectory 'runs' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if ($outputRoot -eq $repoRoot -or $outputRoot.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputDirectory must be outside the source checkout.' }
foreach ($command in @('git', 'dotnet', 'node', 'npm', 'docker', $Python)) { Get-Command $command -ErrorAction Stop | Out-Null }
& $Python -c 'import paramiko'
if ($LASTEXITCODE -ne 0) { throw "Paramiko is missing. Install it once with: $Python -m pip install paramiko" }

$runName = '{0}-{1}-{2}' -f $themeSlug, [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'), [Guid]::NewGuid().ToString('N').Substring(0, 8)
$runRoot = Join-Path $outputRoot $runName
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$receiptPath = Join-Path $runRoot 'deployment.json'
$lockPath = Join-Path $privateDirectory 'deployment.lock'
New-Item -ItemType Directory -Path $privateDirectory -Force | Out-Null
$deploymentLock = $null
$transcriptStarted = $false
try {
    # One local deployment at a time; the remote helper also holds a server lock.
    try { $deploymentLock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
    catch { throw 'Another storefront deployment is already running on this computer.' }
    Start-Transcript -LiteralPath (Join-Path $runRoot 'deployment.log') | Out-Null
    $transcriptStarted = $true
    Write-Host "Deploying $Theme. Build and deployment records: $runRoot"

    $dockerContext = & docker context show
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the local Docker context.' }
    $dockerContext = $dockerContext.Trim()
    $dockerEndpoint = & docker context inspect $dockerContext --format '{{.Endpoints.docker.Host}}'
    if ($LASTEXITCODE -ne 0 -or $dockerEndpoint -notmatch '^(npipe|unix)://') { throw 'Select a local Docker Desktop context before deployment; remote Docker engines are not used for this build.' }
    # Pin every image command to this local context, including when DOCKER_HOST
    # is set or another terminal changes the default context during the build.
    $localDocker = @('--context', $dockerContext)
    $dockerPlatform = & docker @localDocker info --format '{{.OSType}}/{{.Architecture}}' 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Starting Docker Desktop; waiting for its Linux engine (up to 120 seconds).'
        & docker desktop start --timeout 120
        if ($LASTEXITCODE -ne 0) { throw 'Docker Desktop could not start. Start its Linux engine and run this script again.' }
        $dockerPlatform = & docker @localDocker info --format '{{.OSType}}/{{.Architecture}}'
        if ($LASTEXITCODE -ne 0) { throw 'The Docker engine is unavailable.' }
    }
    if ($dockerPlatform.Trim() -notmatch '^linux/(x86_64|amd64)$') { throw 'This deployment requires Docker with the Linux amd64 engine.' }

    Write-Host '1/4 Publishing the current working files in an isolated Linux build.'
    & (Join-Path $PSScriptRoot 'publish-storefront.ps1') -Theme $Theme -Environment test -RuntimeIdentifier linux-x64 -OutputDirectory $runRoot -HttpPort $(if ($Theme -eq 'Madadrang') { 9011 } else { 9012 })
    $packages = @(Get-ChildItem -LiteralPath $runRoot -Directory -Filter "$themeSlug-test-*")
    if ($packages.Count -ne 1) { throw 'The publisher did not produce exactly one isolated package.' }
    $packageRoot = $packages[0].FullName
    $manifest = Get-Content -LiteralPath (Join-Path $packageRoot 'package.json') -Raw | ConvertFrom-Json
    if ($manifest.Theme -ne $Theme -or $manifest.RuntimeIdentifier -ne 'linux-x64' -or $manifest.SourceCommit -notmatch '^[0-9a-f]{40}$' -or $manifest.SourceSha256 -notmatch '^[0-9a-f]{64}$') { throw 'The published package identity is invalid.' }
    $imageTag = '{0}:{1}-{2}-{3}' -f $imageRepository, $themeSlug, $manifest.SourceCommit.Substring(0, 12), $runName.Substring($themeSlug.Length + 1)
    Copy-Item -LiteralPath (Join-Path $packageRoot 'package.json') -Destination (Join-Path $runRoot 'package.json')

    # Keep the Docker context limited to the published app, never the source,
    # credentials, logs, or the archived image that will be created afterwards.
    Copy-Item -LiteralPath (Join-Path $manifest.SourceSnapshot 'scripts/DeployTestServer/Dockerfile') -Destination (Join-Path $packageRoot 'Dockerfile')
    [IO.File]::WriteAllText((Join-Path $packageRoot '.dockerignore'), "**`n!Dockerfile`n!app/`n!app/**`n")
    Write-Host "2/4 Building local image $imageTag"
    & docker @localDocker build --platform linux/amd64 --pull --tag $imageTag --label "com.nop.storefront.theme=$Theme" --label "org.opencontainers.image.revision=$($manifest.SourceCommit)" --label "com.nop.storefront.source-sha256=$($manifest.SourceSha256)" $packageRoot
    if ($LASTEXITCODE -ne 0) { throw 'Docker image build failed; the remote store was not changed.' }
    $archivePath = Join-Path $runRoot 'image.tar'
    Write-Host '3/4 Saving the image for transfer.'
    & docker @localDocker image save --output $archivePath $imageTag
    if ($LASTEXITCODE -ne 0) { throw 'Docker image save failed; the remote store was not changed.' }
    $imageSizeOutput = & docker @localDocker image inspect --format '{{.Size}}' $imageTag
    if ($LASTEXITCODE -ne 0 -or $imageSizeOutput -notmatch '^\d+$' -or [long]$imageSizeOutput -le 0) { throw 'Cannot determine the image size for the remote disk-space preflight.' }

    Write-Host '4/4 Transferring the image and replacing only the selected store.'
    & $Python (Join-Path $manifest.SourceSnapshot 'scripts/DeployTestServer/deploy-image.py') --theme $themeSlug --image $imageTag --image-size $imageSizeOutput --archive $archivePath --config $ConfigPath --receipt $receiptPath --source-commit $manifest.SourceCommit --source-hash $manifest.SourceSha256 --startup-timeout $StartupTimeout
    if ($LASTEXITCODE -ne 0) { throw "Deployment failed. Inspect $receiptPath and deployment.log; build artifacts were retained." }
    if (!(Test-Path -LiteralPath $receiptPath -PathType Leaf)) { throw 'Deployment did not return its receipt. Inspect the server before retrying.' }
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if ($receipt.status -ne 'success') { throw "Deployment was not confirmed successful. See $receiptPath" }

    if (!$KeepBuildArtifacts) {
        # Only delete this run's exact, verified child package and archive.
        $resolvedRunRoot = (Resolve-Path -LiteralPath $runRoot).Path
        $resolvedPackageRoot = (Resolve-Path -LiteralPath $packageRoot).Path
        if ([IO.Path]::GetDirectoryName($resolvedPackageRoot) -ne $resolvedRunRoot -or !([IO.Path]::GetFileName($resolvedPackageRoot).StartsWith("$themeSlug-test-"))) { throw 'Refusing cleanup outside this deployment run.' }
        try {
            Remove-Item -LiteralPath $resolvedPackageRoot -Recurse -Force
            Remove-Item -LiteralPath (Join-Path $resolvedRunRoot 'image.tar') -Force
            & docker @localDocker image rm $imageTag
            if ($LASTEXITCODE -ne 0) { Write-Warning 'Deployment succeeded, but the local temporary image could not be removed.' }
        }
        catch { Write-Warning "Deployment succeeded, but local build cleanup was incomplete: $($_.Exception.Message)" }
    }
    Write-Host "Deployment succeeded for $Theme. Receipt: $receiptPath"
}
finally {
    if ($transcriptStarted) { Stop-Transcript | Out-Null }
    if ($null -ne $deploymentLock) { $deploymentLock.Dispose() }
}
