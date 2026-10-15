#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string[]]$Configuration = @('Debug', 'Release'),
    [switch]$BuildSolution
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repo ('runtime-verification/compression-tests-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6))
New-Item -ItemType Directory -Path $artifacts | Out-Null
$client = 'src/Storefront/Storefront.Client/Storefront.Client.csproj'
$originalEnvironment = @{}
foreach ($name in @('CompressionExcludePatterns', 'GenerateCompressedFiles')) {
    $originalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    [Environment]::SetEnvironmentVariable($name, $null, 'Process')
}
$results = [Collections.Generic.List[object]]::new()
$failure = $null
function Assert([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
}
function Invoke-CheckedDotnet([string]$Name, [string[]]$Arguments) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    & dotnet @Arguments *> (Join-Path $artifacts "$Name.log")
    $result = [ordered]@{ name = $Name; seconds = [Math]::Round($timer.Elapsed.TotalSeconds, 3); exitCode = $LASTEXITCODE }
    $results.Add($result)
    Write-Host ($result | ConvertTo-Json -Compress)
    Assert ($result.exitCode -eq 0) "dotnet failed: $artifacts/$Name.log"
}
function Get-ExpandedHash([string]$Path) {
    $file = [IO.File]::OpenRead($Path)
    $decoder = $null
    try {
        $decoder = if ($Path.EndsWith('.br')) {
            [IO.Compression.BrotliStream]::new($file, [IO.Compression.CompressionMode]::Decompress)
        } else {
            [IO.Compression.GZipStream]::new($file, [IO.Compression.CompressionMode]::Decompress)
        }
        return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($decoder))
    } finally {
        if ($decoder) { $decoder.Dispose() }
        $file.Dispose()
    }
}

Push-Location $repo
try {
    Write-Host "Verification artifacts: $artifacts"
    if ($BuildSolution) { Invoke-CheckedDotnet 'solution-build' @('build', 'src/Storefront.sln') }
    foreach ($config in $Configuration) {
        $settingsJson = & dotnet msbuild $client "-p:Configuration=$config" '-p:StorefrontTheme=All' '-getProperty:CompressionEnabled,PublishCompressionFormats,EnableFindRazorSourceFile,CompressionExcludePatterns'
        Assert ($LASTEXITCODE -eq 0) 'Could not evaluate compression settings.'
        $settingsJson | Set-Content (Join-Path $artifacts "$config-settings.json")
        $settings = ($settingsJson | Out-String | ConvertFrom-Json).Properties
        Assert ($settings.CompressionEnabled -eq 'true') "$config compression is disabled."
        Assert ($settings.PublishCompressionFormats -match 'gzip' -and $settings.PublishCompressionFormats -match 'brotli') "$config publish must generate both compression formats."
        if ($config -eq 'Debug') {
            Assert ($settings.CompressionExcludePatterns.Contains('**/FindRazorSourceFileConfig.json')) 'CLI Debug build lacks the development-config exclusion.'
        } else {
            Assert ($settings.EnableFindRazorSourceFile -eq 'false') 'Release should disable FindRazorSourceFile.'
        }
        # A new destination proves these compressed files came from this publish.
        $destination = Join-Path $artifacts "$config-publish"
        Assert (!(Test-Path -LiteralPath $destination)) 'Publish destination must be fresh.'
        Invoke-CheckedDotnet "$config-publish" @('publish', $client, '--configuration', $config, '-p:StorefrontTheme=All', '--output', $destination, '--nologo')
        $framework = Join-Path $destination 'wwwroot/_framework'
        $assets = @(Get-ChildItem -LiteralPath $framework -File | Where-Object Extension -in @('.wasm', '.js'))
        Assert (@($assets | Where-Object Extension -eq '.wasm').Count -gt 0) 'No WASM assets were published.'
        Assert (@($assets | Where-Object Extension -eq '.js').Count -gt 0) 'No JavaScript assets were published.'
        $bytes = [ordered]@{ original = 0L; gzip = 0L; brotli = 0L }
        foreach ($asset in $assets) {
            $hash = (Get-FileHash -LiteralPath $asset.FullName -Algorithm SHA256).Hash
            $bytes.original += $asset.Length
            foreach ($format in @(@('.gz', 'gzip'), @('.br', 'brotli'))) {
                $compressed = $asset.FullName + $format[0]
                Assert (Test-Path -LiteralPath $compressed) "Missing compressed asset: $compressed"
                Assert ((Get-ExpandedHash $compressed) -eq $hash) "Decompressed bytes differ: $compressed"
                $bytes[$format[1]] += (Get-Item -LiteralPath $compressed).Length
            }
        }
        $badConfig = @(Get-ChildItem -LiteralPath $destination -Recurse -File | Where-Object { $_.Name -match '^FindRazorSourceFileConfig.*\.(gz|br)$' })
        Assert ($badConfig.Count -eq 0) 'Development config was published as a compressed asset.'
        $log = Get-Content (Join-Path $artifacts "$config-publish.log") -Raw
        Assert (!$log.Contains('Endpoints not found for compressed asset')) 'Compression endpoint regression occurred.'
        $evidence = [ordered]@{ configuration = $config; frameworkAssets = $assets.Count; compressedFilesVerified = $assets.Count * 2; bytes = $bytes; passed = $true }
        $evidence | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $artifacts "$config-evidence.json")
        Write-Host ($evidence | ConvertTo-Json -Depth 4 -Compress)
    }
} catch {
    $failure = $_.Exception.Message
    throw
} finally {
    [ordered]@{ passed = ($null -eq $failure); failure = $failure; runs = @($results); artifactsDirectory = $artifacts } |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $artifacts 'result.json')
    foreach ($name in $originalEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $originalEnvironment[$name], 'Process') }
    Pop-Location
}
