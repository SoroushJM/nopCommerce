param([ValidateSet('Output', 'Source', 'Styles', 'Theme')][string]$Suite = 'Output', [Parameter(Mandatory)][string]$ArtifactsDirectory)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location $repo
$project = 'src/Plugins/Nop.Plugin.Misc.PersianStorefront/Nop.Plugin.Misc.PersianStorefront.csproj'
$published = Join-Path $repo 'src/Presentation/Nop.Web/Plugins/Misc.PersianStorefront/Content/Client'
$stamp = Join-Path $repo 'src/Plugins/Nop.Plugin.Misc.PersianStorefront/obj/storefront-client.stamp'
$results = [Collections.Generic.List[object]]::new()

function Assert([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
}
function Invoke-VerifiedBuild([string]$Name, [string[]]$Extra = @(), [switch]$Full) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $log = Join-Path $ArtifactsDirectory "$Name.log"
    if ($Full) {
        & dotnet build $project --configuration Debug -p:StorefrontTheme=All -maxcpucount:1 --verbosity minimal @Extra *> $log
    } else {
        & dotnet msbuild $project -t:BuildStorefrontClient -p:Configuration=Debug -p:StorefrontTheme=All -maxcpucount:1 -v:minimal @Extra *> $log
    }
    $code = $LASTEXITCODE
    $result = [pscustomobject]@{ name = $Name; seconds = [Math]::Round($timer.Elapsed.TotalSeconds, 3); exitCode = $code }
    $results.Add($result)
    $results | ConvertTo-Json | Set-Content (Join-Path $ArtifactsDirectory "verify-$Suite.json")
    $result | ConvertTo-Json -Compress | Write-Host
    Assert ($code -eq 0) "Build failed: $log"
}
function Get-PublishedHashes {
    $hashes = @{}
    Get-ChildItem -LiteralPath $published -Recurse -File | ForEach-Object {
        $hashes[[IO.Path]::GetRelativePath($published, $_.FullName)] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
    return $hashes
}
function Get-ClientWasmHash {
    $manifest = Get-Content (Join-Path $published 'Storefront.Client.staticwebassets.endpoints.json') -Raw | ConvertFrom-Json
    $asset = $manifest.Endpoints.AssetFile | Where-Object { $_ -match '^_framework/Storefront\.Client\.[^.]+\.wasm$' } | Select-Object -Unique
    Assert (@($asset).Count -eq 1) 'Expected one active Client WASM asset in the publish manifest.'
    return (Get-FileHash (Join-Path "$published/wwwroot" $asset)).Hash
}

switch ($Suite) {
    'Output' {
        $beforeStamp = (Get-Item $stamp).LastWriteTimeUtc.Ticks
        $before = Get-PublishedHashes
        Invoke-VerifiedBuild 'verify-output-cached' -Full
        Assert ((Get-Item $stamp).LastWriteTimeUtc.Ticks -eq $beforeStamp) 'An unchanged full build republished Client.'
        Invoke-VerifiedBuild 'verify-output-forced' @('-p:StorefrontClientIncrementalPublish=false')
        Assert ((Get-Content (Join-Path $ArtifactsDirectory 'verify-output-forced.log') -Raw).Contains('Storefront.Client ->')) 'Forced publication did not execute.'
        Assert (!(Test-Path -LiteralPath $stamp)) 'Uncached publication left the previous cache valid.'
        $after = Get-PublishedHashes
        $changed = @($before.Keys | Where-Object { !$after.ContainsKey($_) -or $before[$_] -ne $after[$_] })
        Assert ($before.Count -eq $after.Count -and $changed.Count -eq 0) "Forced publication changed cached output: $changed"
        $victim = Join-Path $published 'wwwroot/_content/Storefront.UI.Boutique/boutique.css'
        Assert (Test-Path -LiteralPath $victim) "Expected output is missing: $victim"
        $victimHash = (Get-FileHash -LiteralPath $victim).Hash
        Remove-Item -LiteralPath $victim
        Invoke-VerifiedBuild 'verify-output-missing' -Full
        Assert ((Get-FileHash -LiteralPath $victim).Hash -eq $victimHash) 'Deleted output was not faithfully regenerated.'
        Assert ((Get-Item $stamp).LastWriteTimeUtc.Ticks -gt $beforeStamp) 'Missing output did not trigger publication.'
        Write-Host "PASS: cached and forced publication have identical SHA256 hashes for $($before.Count) files; deleted output regenerated."
    }
    'Source' {
        $program = Join-Path $repo 'src/Storefront/Storefront.Client/Program.cs'
        $probe = Join-Path $repo 'src/Storefront/Storefront.Client/IncrementalBuildProbe.cs'
        Assert (!(Test-Path -LiteralPath $probe)) 'Probe path already exists.'
        $original = [IO.File]::ReadAllBytes($program)
        $baselineHash = Get-ClientWasmHash
        try {
            [IO.File]::WriteAllText($probe, 'internal static class IncrementalBuildProbe { public static string Value => "incremental-probe-v1"; }')
            $source = [Text.Encoding]::UTF8.GetString($original).Replace('await builder.Build().RunAsync();', "Console.WriteLine(IncrementalBuildProbe.Value);`nawait builder.Build().RunAsync();")
            [IO.File]::WriteAllText($program, $source)
            Invoke-VerifiedBuild 'verify-source-added' -Full
            $addedHash = Get-ClientWasmHash
            Assert ($addedHash -ne $baselineHash) 'New source was not reflected in published WASM.'
            [IO.File]::WriteAllText($probe, 'internal static class IncrementalBuildProbe { public static string Value => "incremental-probe-v2"; }')
            Invoke-VerifiedBuild 'verify-source-edited' -Full
            Assert ((Get-ClientWasmHash) -ne $addedHash) 'Edited source was not reflected in published WASM.'
        } finally {
            [IO.File]::WriteAllBytes($program, $original)
            if (Test-Path -LiteralPath $probe) { Remove-Item -LiteralPath $probe }
        }
        Invoke-VerifiedBuild 'verify-source-removed' -Full
        Assert ((Get-ClientWasmHash) -eq $baselineHash) 'Removing the probe did not restore the original published Client WASM.'
        Write-Host 'PASS: adding/editing/removing C# source rebuilt the published WASM; original output hash restored.'
    }
    'Styles' {
        foreach ($theme in @(@('Madadrang', 'Storefront', 'storefront'), @('Boutique', 'Boutique', 'boutique'))) {
            $dir = Join-Path $repo "src/Storefront/Storefront.UI.$($theme[0])"
            $csproj = Join-Path $dir "Storefront.UI.$($theme[0]).csproj"
            $probe = Join-Path $dir 'Components/IncrementalStyleProbe.razor'
            $css = Join-Path $dir "wwwroot/$($theme[2]).css"
            $styleStamp = Join-Path $dir 'obj/Debug/net10.0/tailwind.stamp'
            Assert (!(Test-Path -LiteralPath $probe)) 'Style probe path already exists.'
            $originalHash = (Get-FileHash $css).Hash
            function Build-Styles([string]$Step) {
                & dotnet msbuild $csproj "-t:Build$($theme[1])Styles" -p:Configuration=Debug -v:minimal *> (Join-Path $ArtifactsDirectory "verify-style-$($theme[0])-$Step.log")
                Assert ($LASTEXITCODE -eq 0) "Style build failed: $($theme[0]) $Step"
            }
            try {
                $previousStamp = (Get-Item $styleStamp).LastWriteTimeUtc.Ticks
                Build-Styles 'cached'
                Assert ((Get-Item $styleStamp).LastWriteTimeUtc.Ticks -eq $previousStamp) 'Unchanged CSS was rebuilt.'
                [IO.File]::WriteAllText($probe, '<span class="w-[17.3125rem]">probe</span>')
                Build-Styles 'added'
                Assert ((Get-FileHash $css).Hash -ne $originalHash) 'Adding a Razor source did not rebuild CSS.'
                $addedHash = (Get-FileHash $css).Hash
                [IO.File]::WriteAllText($probe, '<span class="w-[17.4375rem]">probe</span>')
                Build-Styles 'edited'
                Assert ((Get-FileHash $css).Hash -ne $addedHash) 'Editing a Razor source did not rebuild CSS.'
            } finally {
                if (Test-Path -LiteralPath $probe) { Remove-Item -LiteralPath $probe }
            }
            Build-Styles 'removed'
            Assert ((Get-FileHash $css).Hash -eq $originalHash) 'Removing a Razor source did not restore the original CSS.'
            Remove-Item -LiteralPath $css
            Build-Styles 'missing'
            Assert ((Get-FileHash $css).Hash -eq $originalHash) 'Missing CSS was not regenerated.'
            $previousStamp = (Get-Item $styleStamp).LastWriteTimeUtc.Ticks
            Build-Styles 'final-cached'
            Assert ((Get-Item $styleStamp).LastWriteTimeUtc.Ticks -eq $previousStamp) 'Unchanged CSS was rebuilt after regeneration.'
            Write-Host "PASS: $($theme[0]) unchanged/add/edit/delete/missing CSS checks."
        }
    }
    'Theme' {
        $beforeStamp = (Get-Item $stamp).LastWriteTimeUtc.Ticks
        Invoke-VerifiedBuild 'verify-theme-boutique' @('-p:StorefrontTheme=Boutique')
        Assert ((Get-Item $stamp).LastWriteTimeUtc.Ticks -gt $beforeStamp) 'Theme switch did not trigger publication.'
        $manifest = Get-Content (Join-Path $published 'Storefront.Client.staticwebassets.endpoints.json') -Raw
        Assert (!$manifest.Contains('_content/Storefront.UI.Madadrang/')) 'Boutique manifest still contains Madadrang assets.'
        $beforeStamp = (Get-Item $stamp).LastWriteTimeUtc.Ticks
        Invoke-VerifiedBuild 'verify-theme-boutique-cached' @('-p:StorefrontTheme=Boutique')
        Assert ((Get-Item $stamp).LastWriteTimeUtc.Ticks -eq $beforeStamp) 'Unchanged Boutique publication was not cached.'
        Invoke-VerifiedBuild 'verify-theme-all' -Full
        $manifest = Get-Content (Join-Path $published 'Storefront.Client.staticwebassets.endpoints.json') -Raw
        Assert ($manifest.Contains('_content/Storefront.UI.Madadrang/') -and $manifest.Contains('_content/Storefront.UI.Boutique/')) 'All theme publication did not restore both asset sets.'
        Write-Host 'PASS: theme switch invalidates cache; repeated theme skips publication; All restores both themes.'
    }
}
