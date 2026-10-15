[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Madadrang', 'MishaShop', 'Boutique')]
    [string]$Theme,

    [string]$ConfigPath,
    [string]$OutputDirectory,

    [ValidateRange(30, 1800)]
    [int]$StartupTimeout = 180,

    [switch]$KeepBuildArtifacts
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-DeploymentConfiguration {
    param(
        [string]$RepositoryRoot,
        [string]$PrivateDirectory,
        [string]$RequestedPath
    )

    if (!$RequestedPath) {
        $configCandidates = @(
            (Join-Path $PrivateDirectory 'deploy.private.json')
            (Join-Path (Split-Path -Parent $RepositoryRoot) `
                'dual-store-deployment-20261012/deploy.private.json')
        )
        $RequestedPath = $configCandidates |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
            Select-Object -First 1
    }

    if (!$RequestedPath) {
        throw 'Private deployment configuration was not found. See docs/one-command-deployment.md.'
    }

    $resolvedPath = (Resolve-Path -LiteralPath $RequestedPath).Path
    $repositoryPrefix = $RepositoryRoot + [IO.Path]::DirectorySeparatorChar
    if ($resolvedPath.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Keep deployment credentials and configuration outside the Git checkout.'
    }

    $settings = Get-Content -LiteralPath $resolvedPath -Raw | ConvertFrom-Json
    foreach ($field in @('host', 'username', 'knownHostsPath', 'remoteRoot')) {
        if (!$settings.PSObject.Properties[$field] -or
            [string]::IsNullOrWhiteSpace([string]$settings.$field)) {
            throw "Missing private configuration field: $field"
        }
    }

    $authenticationField = if ($settings.PSObject.Properties['keyFilename'] -and
        $settings.keyFilename) { 'keyFilename' } else { 'handoffPath' }

    if (!$settings.PSObject.Properties[$authenticationField] -or
        !$settings.$authenticationField) {
        throw 'Configure an SSH keyFilename or a private handoffPath.'
    }

    foreach ($field in @('knownHostsPath', $authenticationField)) {
        $privateFile = [string]$settings.$field
        if (![IO.Path]::IsPathRooted($privateFile)) {
            $privateFile = Join-Path (Split-Path -Parent $resolvedPath) $privateFile
        }
        if (!(Test-Path -LiteralPath $privateFile -PathType Leaf)) {
            throw "Missing private configuration file: $field"
        }
    }

    $imageRepository = if ($settings.PSObject.Properties['imageRepository']) {
        [string]$settings.imageRepository
    }
    else {
        'nop-dual'
    }
    if ($imageRepository -notmatch '^[a-z0-9]+(?:[._/-][a-z0-9]+)*$') {
        throw 'imageRepository must be a lowercase Docker repository name without a tag.'
    }

    return [pscustomobject]@{
        Path = $resolvedPath
        ImageRepository = $imageRepository
    }
}

function Invoke-LocalDocker {
    param(
        [string]$Context,
        [string[]]$Arguments,
        [string]$FailureMessage
    )

    & docker --context $Context @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw $FailureMessage
    }
}

function Start-LocalDocker {
    $context = & docker context show
    if ($LASTEXITCODE -ne 0) {
        throw 'Cannot identify the local Docker context.'
    }
    $context = $context.Trim()

    $endpoint = & docker context inspect $context --format '{{.Endpoints.docker.Host}}'
    if ($LASTEXITCODE -ne 0 -or $endpoint -notmatch '^(npipe|unix)://') {
        throw 'Select a local Docker Desktop context; remote Docker engines are not used for builds.'
    }

    $platform = & docker --context $context info --format '{{.OSType}}/{{.Architecture}}' 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Starting Docker Desktop; waiting up to 120 seconds for its Linux engine.'
        & docker desktop start --timeout 120 | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw 'Docker Desktop could not start. Start its Linux engine and run the script again.'
        }
        $platform = Invoke-LocalDocker -Context $context `
            -Arguments @('info', '--format', '{{.OSType}}/{{.Architecture}}') `
            -FailureMessage 'The Docker engine is unavailable.'
    }

    if ($platform.Trim() -notmatch '^linux/(x86_64|amd64)$') {
        throw 'This deployment requires Docker with the Linux amd64 engine.'
    }
    return $context
}

function New-StorefrontPackage {
    param(
        [string]$SelectedTheme,
        [string]$RunDirectory
    )

    $publishOptions = @{
        Theme = $SelectedTheme
        Environment = 'test'
        RuntimeIdentifier = 'linux-x64'
        OutputDirectory = $RunDirectory
        HttpPort = if ($SelectedTheme -eq 'Madadrang') { 9011 } else { 9012 }
    }
    # Keep publisher logs out of this function's structured return value.
    & (Join-Path $PSScriptRoot 'publish-storefront.ps1') @publishOptions | Out-Host

    $themeSlug = $SelectedTheme.ToLowerInvariant()
    $packages = @(Get-ChildItem -LiteralPath $RunDirectory -Directory -Filter "$themeSlug-test-*")
    if ($packages.Count -ne 1) {
        throw 'The publisher did not produce exactly one isolated package.'
    }

    $manifestPath = Join-Path $packages[0].FullName 'package.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.Theme -ne $SelectedTheme -or
        $manifest.RuntimeIdentifier -ne 'linux-x64' -or
        $manifest.SourceCommit -notmatch '^[0-9a-f]{40}$' -or
        $manifest.SourceSha256 -notmatch '^[0-9a-f]{64}$') {
        throw 'The published package identity is invalid.'
    }

    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $RunDirectory 'package.json')
    return [pscustomobject]@{
        Directory = $packages[0].FullName
        Manifest = $manifest
    }
}

function New-StorefrontImage {
    param(
        [object]$Package,
        [string]$SelectedTheme,
        [string]$ImageTag,
        [string]$DockerContext
    )

    $dockerfile = Join-Path $Package.Manifest.SourceSnapshot 'scripts/DeployTestServer/Dockerfile'
    Copy-Item -LiteralPath $dockerfile -Destination (Join-Path $Package.Directory 'Dockerfile')
    [IO.File]::WriteAllText(
        (Join-Path $Package.Directory '.dockerignore'),
        "**`n!Dockerfile`n!app/`n!app/**`n"
    )

    # Only the published app enters the Docker context. Credentials and the
    # source snapshot remain outside the image.
    $buildArguments = @(
        'build', '--platform', 'linux/amd64', '--pull', '--tag', $ImageTag
        '--label', "com.nop.storefront.theme=$SelectedTheme"
        '--label', "org.opencontainers.image.revision=$($Package.Manifest.SourceCommit)"
        '--label', "com.nop.storefront.source-sha256=$($Package.Manifest.SourceSha256)"
        $Package.Directory
    )
    Invoke-LocalDocker -Context $DockerContext -Arguments $buildArguments `
        -FailureMessage 'Docker image build failed; the remote store was not changed.'
}

function Remove-LocalBuildArtifacts {
    param(
        [string]$RunDirectory,
        [string]$PackageDirectory,
        [string]$ThemeSlug,
        [string]$ImageTag,
        [string]$DockerContext
    )

    $resolvedRun = (Resolve-Path -LiteralPath $RunDirectory).Path
    $resolvedPackage = (Resolve-Path -LiteralPath $PackageDirectory).Path
    $isOwnedChild = [IO.Path]::GetDirectoryName($resolvedPackage) -eq $resolvedRun
    $hasExpectedName = [IO.Path]::GetFileName($resolvedPackage).StartsWith("$ThemeSlug-test-")
    if (!$isOwnedChild -or !$hasExpectedName) {
        throw 'Refusing cleanup outside this deployment run.'
    }

    try {
        Remove-Item -LiteralPath $resolvedPackage -Recurse -Force
        Remove-Item -LiteralPath (Join-Path $resolvedRun 'image.tar') -Force
        Invoke-LocalDocker -Context $DockerContext -Arguments @('image', 'rm', $ImageTag) `
            -FailureMessage 'The local temporary image could not be removed.'
    }
    catch {
        Write-Warning "Deployment succeeded, but local build cleanup was incomplete: $($_.Exception.Message)"
    }
}

if ($PSVersionTable.PSVersion -lt [version]'7.4') {
    throw 'Run this script with PowerShell 7.4 or newer.'
}

$Theme = if ($Theme -in @('MishaShop', 'Boutique')) { 'MishaShop' } else { 'Madadrang' }
$themeSlug = $Theme.ToLowerInvariant()
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$privateDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'NopStorefrontDeploy'
$configuration = Get-DeploymentConfiguration -RepositoryRoot $repositoryRoot `
    -PrivateDirectory $privateDirectory -RequestedPath $ConfigPath

foreach ($command in @('git', 'dotnet', 'node', 'npm', 'docker')) {
    Get-Command $command -ErrorAction Stop | Out-Null
}

if (!$OutputDirectory) {
    $OutputDirectory = Join-Path $privateDirectory 'runs'
}
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$repositoryPrefix = $repositoryRoot + [IO.Path]::DirectorySeparatorChar
if ($outputRoot -eq $repositoryRoot -or
    $outputRoot.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be outside the source checkout.'
}

$runId = [guid]::NewGuid().ToString('N').Substring(0, 8)
$runTimestamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$runName = "$themeSlug-$runTimestamp-$runId"
$runDirectory = Join-Path $outputRoot $runName
$receiptPath = Join-Path $runDirectory 'deployment.json'
New-Item -ItemType Directory -Path $runDirectory, $privateDirectory -Force | Out-Null

$deploymentLock = $null
$transcriptStarted = $false
try {
    $lockPath = Join-Path $privateDirectory 'deployment.lock'
    try {
        $deploymentLock = [IO.File]::Open(
            $lockPath,
            [IO.FileMode]::OpenOrCreate,
            [IO.FileAccess]::ReadWrite,
            [IO.FileShare]::None
        )
    }
    catch {
        throw 'Another storefront deployment is already running on this computer.'
    }

    Start-Transcript -LiteralPath (Join-Path $runDirectory 'deployment.log') | Out-Null
    $transcriptStarted = $true
    Write-Host "Deploying $Theme. Records: $runDirectory"

    . (Join-Path $PSScriptRoot 'DeployTestServer/Import-StorefrontSsh.ps1')
    Import-StorefrontSsh
    $dockerContext = Start-LocalDocker

    Write-Host '1/4 Publishing current working files in an isolated Linux build.'
    $package = New-StorefrontPackage -SelectedTheme $Theme -RunDirectory $runDirectory
    $shortCommit = $package.Manifest.SourceCommit.Substring(0, 12)
    $imageTag = '{0}:{1}-{2}-{3}-{4}' -f `
        $configuration.ImageRepository, $themeSlug, $shortCommit, $runTimestamp, $runId

    Write-Host "2/4 Building local image $imageTag"
    New-StorefrontImage -Package $package -SelectedTheme $Theme `
        -ImageTag $imageTag -DockerContext $dockerContext

    Write-Host '3/4 Saving the image for transfer.'
    $archivePath = Join-Path $runDirectory 'image.tar'
    Invoke-LocalDocker -Context $dockerContext `
        -Arguments @('image', 'save', '--output', $archivePath, $imageTag) `
        -FailureMessage 'Docker image save failed; the remote store was not changed.'
    $imageSize = Invoke-LocalDocker -Context $dockerContext `
        -Arguments @('image', 'inspect', '--format', '{{.Size}}', $imageTag) `
        -FailureMessage 'Cannot determine image size for the remote disk-space preflight.'
    if ($imageSize -notmatch '^\d+$' -or [long]$imageSize -le 0) {
        throw 'Docker returned an invalid image size.'
    }

    Write-Host '4/4 Transferring the image and replacing only the selected store.'
    $deploymentOptions = @{
        Theme = $Theme
        Image = $imageTag
        ImageSize = [long]$imageSize
        ArchivePath = $archivePath
        ConfigPath = $configuration.Path
        ReceiptPath = $receiptPath
        SourceCommit = $package.Manifest.SourceCommit
        SourceHash = $package.Manifest.SourceSha256
        StartupTimeout = $StartupTimeout
    }
    $deploymentScript = Join-Path $package.Manifest.SourceSnapshot 'scripts/DeployTestServer/Deploy-Image.ps1'
    & $deploymentScript @deploymentOptions

    if (!(Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
        throw 'Deployment did not return its receipt. Inspect the server before retrying.'
    }
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if ($receipt.status -ne 'success') {
        throw "Deployment was not confirmed successful. See $receiptPath"
    }

    if (!$KeepBuildArtifacts) {
        Remove-LocalBuildArtifacts -RunDirectory $runDirectory -PackageDirectory $package.Directory `
            -ThemeSlug $themeSlug -ImageTag $imageTag -DockerContext $dockerContext
    }
    Write-Host "Deployment succeeded for $Theme. Receipt: $receiptPath"
}
finally {
    if ($transcriptStarted) {
        Stop-Transcript | Out-Null
    }
    if ($null -ne $deploymentLock) {
        $deploymentLock.Dispose()
    }
}
