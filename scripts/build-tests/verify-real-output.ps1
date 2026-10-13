param([Parameter(Mandatory)][string]$ArtifactsDirectory)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location $repo
$project = 'src/Plugins/Nop.Plugin.Misc.PersianStorefront/Nop.Plugin.Misc.PersianStorefront.csproj'
$content = Join-Path $repo 'src/Presentation/Nop.Web/Plugins/Misc.PersianStorefront/Content'
$publish = Join-Path $content 'Client'
$stamp = Join-Path $repo 'src/Plugins/Nop.Plugin.Misc.PersianStorefront/obj/storefront-client.stamp'
$sourcePaths = [ordered]@{
    client = 'src/Storefront/Storefront.Client/Program.cs'
    razor = 'src/Storefront/Storefront.UI.Shared/Components/NativeSelectBridge.razor'
    madadrang = 'src/Storefront/Storefront.UI.Madadrang/Components/HomeHero.razor'
    boutique = 'src/Storefront/Storefront.UI.Boutique/Components/BoutiqueHero.razor'
    asset = 'src/Storefront/Storefront.UI.Shared/wwwroot/native-select.js'
}
$original = @{}
$originalHashes = @{}
foreach ($key in $sourcePaths.Keys) {
    $path = Join-Path $repo $sourcePaths[$key]
    $original[$key] = [IO.File]::ReadAllBytes($path)
    $originalHashes[$key] = (Get-FileHash -LiteralPath $path).Hash
    [IO.File]::WriteAllBytes((Join-Path $ArtifactsDirectory "real-output-$key.backup"), $original[$key])
}
$results = [Collections.Generic.List[object]]::new()
function Assert([bool]$Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function Build([string]$Name) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    & dotnet build $project --configuration Debug -p:StorefrontTheme=All -maxcpucount:1 --verbosity minimal *> (Join-Path $ArtifactsDirectory "$Name.log")
    $code = $LASTEXITCODE
    $result = [pscustomobject]@{ name = $Name; seconds = [Math]::Round($timer.Elapsed.TotalSeconds, 3); exitCode = $code }
    $results.Add($result)
    $results | ConvertTo-Json | Set-Content (Join-Path $ArtifactsDirectory 'real-output-timings.json')
    $result | ConvertTo-Json -Compress | Write-Host
    Assert ($code -eq 0) "Build failed: $Name"
}
function Active-Wasm([string]$Assembly) {
    $manifest = Get-Content (Join-Path $publish 'Storefront.Client.staticwebassets.endpoints.json') -Raw | ConvertFrom-Json
    $pattern = '^_framework/' + [regex]::Escape($Assembly) + '\.[^.]+\.wasm$'
    $assets = @($manifest.Endpoints.AssetFile | Where-Object { $_ -match $pattern } | Select-Object -Unique)
    Assert ($assets.Count -eq 1) "Expected one active WASM for $Assembly"
    return Join-Path "$publish/wwwroot" $assets[0]
}
function Contains-BinaryText([string]$Path, [string]$Text) {
    $hex = [Convert]::ToHexString([IO.File]::ReadAllBytes($Path))
    return $hex.Contains([Convert]::ToHexString([Text.Encoding]::Unicode.GetBytes($Text))) -or $hex.Contains([Convert]::ToHexString([Text.Encoding]::UTF8.GetBytes($Text)))
}
function Output-Hashes {
    return @{
        client = (Get-FileHash (Active-Wasm 'Storefront.Client')).Hash
        razor = (Get-FileHash (Active-Wasm 'Storefront.UI.Shared')).Hash
        madadrang = (Get-FileHash (Join-Path $publish 'wwwroot/_content/Storefront.UI.Madadrang/storefront.css')).Hash
        boutique = (Get-FileHash (Join-Path $publish 'wwwroot/_content/Storefront.UI.Boutique/boutique.css')).Hash
        asset = (Get-FileHash (Join-Path $publish 'wwwroot/_content/Storefront.UI.Shared/native-select.js')).Hash
    }
}
$baseline = Output-Hashes
$evidence = [Collections.Generic.List[object]]::new()
try {
    foreach ($version in 1..2) {
        $clientMarker = "CACHE_REAL_CLIENT_V$version"
        $razorMarker = "CACHE_REAL_RAZOR_V$version"
        $assetMarker = "CACHE_REAL_ASSET_V$version"
        $madWidth = if ($version -eq 1) { '17.8125rem' } else { '17.9375rem' }
        $boutWidth = if ($version -eq 1) { '18.0625rem' } else { '18.1875rem' }
        $clientText = [Text.Encoding]::UTF8.GetString($original.client).Replace('await builder.Build().RunAsync();', "Console.WriteLine(`"$clientMarker`");`nawait builder.Build().RunAsync();")
        [IO.File]::WriteAllText((Join-Path $repo $sourcePaths.client), $clientText)
        [IO.File]::WriteAllText((Join-Path $repo $sourcePaths.razor), [Text.Encoding]::UTF8.GetString($original.razor) + "`n<span hidden>$razorMarker</span>`n")
        [IO.File]::WriteAllText((Join-Path $repo $sourcePaths.madadrang), [Text.Encoding]::UTF8.GetString($original.madadrang) + "`n<span hidden class=`"w-[$madWidth]`">cache-probe</span>`n")
        [IO.File]::WriteAllText((Join-Path $repo $sourcePaths.boutique), [Text.Encoding]::UTF8.GetString($original.boutique) + "`n<span hidden class=`"w-[$boutWidth]`">cache-probe</span>`n")
        [IO.File]::WriteAllText((Join-Path $repo $sourcePaths.asset), [Text.Encoding]::UTF8.GetString($original.asset) + "`n/* $assetMarker */`n")
        Build "real-output-v$version"
        $clientWasm = Active-Wasm 'Storefront.Client'
        $sharedWasm = Active-Wasm 'Storefront.UI.Shared'
        Assert (Contains-BinaryText $clientWasm $clientMarker) 'The compiled C# marker is absent from the active published Client WASM.'
        Assert (Contains-BinaryText $sharedWasm $razorMarker) 'The Razor markup marker is absent from the active published Shared WASM.'
        foreach ($style in @(@('Storefront.UI.Madadrang', 'storefront.css', $madWidth), @('Storefront.UI.Boutique', 'boutique.css', $boutWidth))) {
            $publishedCss = Join-Path $publish "wwwroot/_content/$($style[0])/$($style[1])"
            $pluginCss = Join-Path $content "$($style[0])/$($style[1])"
            Assert ((Get-Content $publishedCss -Raw).Contains("width:$($style[2])")) "Tailwind utility not present in published $($style[1])."
            Assert ((Get-FileHash $publishedCss).Hash -eq (Get-FileHash $pluginCss).Hash) 'Plugin CSS copy differs from Client publication.'
        }
        $assetHash = (Get-FileHash (Join-Path $repo $sourcePaths.asset)).Hash
        foreach ($assetOutput in @("$publish/wwwroot/_content/Storefront.UI.Shared/native-select.js", "$content/Storefront.UI.Shared/native-select.js")) {
            Assert ((Get-Content $assetOutput -Raw).Contains($assetMarker)) 'Updated JavaScript marker is absent from output.'
            Assert ((Get-FileHash $assetOutput).Hash -eq $assetHash) 'Copied JavaScript is not byte-identical to source.'
        }
        if ($version -eq 2) {
            Assert (!(Contains-BinaryText $clientWasm 'CACHE_REAL_CLIENT_V1')) 'Old C# marker survived in active output.'
            Assert (!(Contains-BinaryText $sharedWasm 'CACHE_REAL_RAZOR_V1')) 'Old Razor marker survived in active output.'
            Assert (!(Get-Content "$publish/wwwroot/_content/Storefront.UI.Madadrang/storefront.css" -Raw).Contains('width:17.8125rem')) 'Old Madadrang utility survived.'
            Assert (!(Get-Content "$publish/wwwroot/_content/Storefront.UI.Boutique/boutique.css" -Raw).Contains('width:18.0625rem')) 'Old Boutique utility survived.'
        }
        $evidence.Add([ordered]@{ version = $version; clientWasm = $clientWasm; sharedWasm = $sharedWasm; markers = @($clientMarker, $razorMarker, $assetMarker, $madWidth, $boutWidth); hashes = (Output-Hashes); passed = $true })
        $evidence | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $ArtifactsDirectory 'real-output-evidence.json')
        Write-Host "PASS: version $version markers found in active WASM, both published CSS files and both JavaScript copies."
    }
} finally {
    foreach ($key in $sourcePaths.Keys) { [IO.File]::WriteAllBytes((Join-Path $repo $sourcePaths[$key]), $original[$key]) }
}
Build 'real-output-restored'
$restored = Output-Hashes
foreach ($key in $baseline.Keys) { Assert ($restored[$key] -eq $baseline[$key]) "Restored output hash differs: $key" }
foreach ($key in $sourcePaths.Keys) { Assert ((Get-FileHash (Join-Path $repo $sourcePaths[$key])).Hash -eq $originalHashes[$key]) "Source restoration failed: $key" }
$stampBefore = (Get-Item $stamp).LastWriteTimeUtc.Ticks
Build 'real-output-unchanged'
Assert ((Get-Item $stamp).LastWriteTimeUtc.Ticks -eq $stampBefore) 'An unchanged build republished Client after restoration.'
$result = [ordered]@{ sourceFiles = $sourcePaths; twoVersionsVerified = $true; originalSourceHashesRestored = $true; originalPublishedHashesRestored = $true; subsequentBuildSkippedPublication = $true; baselineHashes = $baseline; restoredHashes = $restored }
$result | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $ArtifactsDirectory 'real-output-result.json')
Write-Host 'PASS: all five original source files and five active published outputs restored byte-for-byte; subsequent unchanged build skipped publication.'
