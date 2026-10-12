[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Madadrang', 'Boutique')][string]$Theme,
    [ValidateSet('test', 'production')][string]$Environment = 'test',
    [string]$RuntimeIdentifier = 'win-x64',
    [string]$OutputDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) 'storefront-packages'),
    [ValidateRange(0, 65535)][int]$HttpPort = 0,
    [ValidateRange(0, 65535)][int]$HttpsPort = 0
)

$ErrorActionPreference = 'Stop'
$Theme = if ($Theme -ieq 'Boutique') { 'Boutique' } else { 'Madadrang' }
$Environment = $Environment.ToLowerInvariant()
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$themeSlug = $Theme.ToLowerInvariant()
$runId = [Guid]::NewGuid().ToString('N')
$packageRoot = Join-Path ([System.IO.Path]::GetFullPath($OutputDirectory)) "$themeSlug-$Environment-$runId"
if ($packageRoot.StartsWith($repoRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputDirectory must be outside the source checkout.' }
$sourceRoot = Join-Path $packageRoot 'source'
$publishRoot = Join-Path $packageRoot 'app'
$instanceName = "nopcommerce-$themeSlug-$Environment"
if ($HttpPort -eq 0) {
    $HttpPort = 2020 + $(if ($Theme -eq 'Boutique') { 10 } else { 0 }) + $(if ($Environment -eq 'production') { 1000 } else { 0 })
}
if ($HttpsPort -eq 0) { $HttpsPort = $HttpPort + 1 }
if ($HttpPort -eq $HttpsPort -or $HttpsPort -gt 65535) { throw 'Choose different valid HTTP and HTTPS ports.' }
if (Test-Path -LiteralPath $packageRoot) { throw "Fresh package directory already exists: $packageRoot" }
New-Item -ItemType Directory -Path $sourceRoot, $publishRoot -Force | Out-Null

function Invoke-Dotnet {
    param([string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE. The isolated source and output are retained at $packageRoot" }
}

# Snapshot tracked and non-ignored working files. Build outputs and local store state
# are not copied; every native plugin can safely write into this private checkout.
$sourceFiles = & git -C $repoRoot -c core.quotepath=false ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0) { throw 'The source snapshot requires a Git checkout.' }
Write-Output "Preparing isolated $Theme source snapshot: $sourceRoot"
foreach ($relativePath in ($sourceFiles | Sort-Object -Unique)) {
    if ($relativePath -match '(^|/)(bin|obj|node_modules|\.git|\.artifacts)(/|$)' -or
        $relativePath -match '^src/Presentation/Nop.Web/(Plugins/|App_Data/DataProtectionKeys/)' -or
        $relativePath -match '(^|/)\.env($|\.)' -or
        $relativePath -match '\.(pfx|key)$') { continue }
    $inputPath = Join-Path $repoRoot $relativePath
    if (!(Test-Path -LiteralPath $inputPath -PathType Leaf)) { continue }
    $destinationPath = Join-Path $sourceRoot $relativePath
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($destinationPath)) | Out-Null
    [System.IO.File]::Copy($inputPath, $destinationPath)
}
Write-Output "Building $Theme in isolated source snapshot."

$previousLocation = Get-Location
try {
    Set-Location -LiteralPath $sourceRoot
    $buildArguments = @('-c', 'Release', "-p:StorefrontTheme=$Theme", "-p:DockerRuntimeIdentifier=$RuntimeIdentifier", '-p:ShouldUnsetParentConfigurationAndPlatform=false', '-p:DebugType=None', '-p:DebugSymbols=false')
    $helperOutput = Join-Path $packageRoot 'build-helper'
    Invoke-Dotnet -Arguments (@('build', 'src/Build/src/ClearPluginAssemblies/ClearPluginAssemblies.csproj', '-c', 'Release', '-o', $helperOutput))
    Get-ChildItem -LiteralPath $helperOutput -Filter 'ClearPluginAssemblies.*' -File | Copy-Item -Destination (Join-Path $sourceRoot 'src/Build')
    Invoke-Dotnet -Arguments (@('build', 'src/Docker.slnx') + $buildArguments)
    Invoke-Dotnet -Arguments (@('publish', 'src/Presentation/Nop.Web/Nop.Web.csproj', '--no-build', '--no-restore', '--self-contained', 'false', '-r', $RuntimeIdentifier, '-o', $publishRoot) + $buildArguments)
    $viewsAssembly = Join-Path $sourceRoot "src/Presentation/Nop.Docker.Views/bin/Release/net10.0/$RuntimeIdentifier/Nop.Docker.Views.dll"
    if (!(Test-Path -LiteralPath $viewsAssembly)) { throw "Missing compiled native views: $viewsAssembly" }
    Copy-Item -LiteralPath $viewsAssembly -Destination $publishRoot

    $themeCss = if ($Theme -eq 'Boutique') { 'boutique.css' } else { 'storefront.css' }
    $activeAssets = Join-Path $publishRoot "Plugins/Misc.PersianStorefront/Content/Storefront.UI.$Theme/$themeCss"
    $wasmAssets = Join-Path $publishRoot 'Plugins/Misc.PersianStorefront/Content/Client/wwwroot/_framework'
    if (!(Test-Path -LiteralPath $activeAssets) -or !(Test-Path -LiteralPath $wasmAssets)) { throw 'The selected theme CSS or browser app was not packaged.' }
    if (!(Get-ChildItem -LiteralPath $wasmAssets -Filter "Storefront.UI.$Theme*.wasm" -File) -or
        !(Get-ChildItem -LiteralPath $wasmAssets -Filter 'Storefront.UI.Shared*.wasm' -File) -or
        !(Test-Path -LiteralPath (Join-Path $publishRoot "Plugins/Misc.PersianStorefront/Storefront.UI.$Theme.dll")) -or
        !(Test-Path -LiteralPath (Join-Path $publishRoot "Themes/$Theme")) -or
        !(Test-Path -LiteralPath (Join-Path $publishRoot 'Themes/DefaultClean'))) { throw 'The selected brand/shared assemblies or native theme folders are missing.' }
    $inactiveTheme = if ($Theme -eq 'Madadrang') { 'Boutique' } else { 'Madadrang' }
    $inactiveFiles = Get-ChildItem -LiteralPath $publishRoot -Recurse -File | Where-Object { $_.Name -like "Storefront.UI.$inactiveTheme*" -or $_.FullName -match "[\\/](Themes[\\/]$inactiveTheme|Storefront\.UI\.$inactiveTheme)[\\/]" }
    if ($inactiveFiles) { throw "The release contains inactive $inactiveTheme artifacts; refusing to label this package as isolated." }
    New-Item -ItemType Directory -Path (Join-Path $publishRoot 'logs'), (Join-Path $publishRoot 'bin'), (Join-Path $publishRoot 'App_Data/DataProtectionKeys') -Force | Out-Null
}
finally { Set-Location -LiteralPath $previousLocation }

# This is a configuration template, not a deployed service or a credential store.
$composeTemplate = @"
STOREFRONT_THEME=$Theme
STOREFRONT_THEME_SLUG=$themeSlug
STOREFRONT_ENVIRONMENT=$Environment
StorefrontThemeConfig__Theme=$Theme
HTTP_PORT=$HttpPort
HTTPS_PORT=$HttpsPort
POSTGRES_DB=nopcommerce_${themeSlug}_${Environment}
POSTGRES_USER=nopcommerce
POSTGRES_PASSWORD=replace-with-a-unique-password
HTTPS_CERT_PASSWORD=replace-with-a-unique-certificate-password
ADMIN_EMAIL=admin@example.com
"@
[System.IO.File]::WriteAllText((Join-Path $packageRoot 'deployment.env.example'), $composeTemplate)
$manifest = [ordered]@{ Theme = $Theme; Environment = $Environment; RuntimeIdentifier = $RuntimeIdentifier; Instance = $instanceName; HttpPort = $HttpPort; HttpsPort = $HttpsPort; PublishedAtUtc = [DateTime]::UtcNow.ToString('o'); AppPath = $publishRoot; SourceSnapshot = $sourceRoot }
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageRoot 'package.json') -Encoding utf8
Write-Output "Prepared $Theme/$Environment package: $publishRoot"
Write-Output "Compose template: $(Join-Path $packageRoot 'deployment.env.example')"
Write-Output 'No service was started or deployed. The source snapshot is retained for reproducibility.'
