#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Benchmark', 'Output', 'Source', 'Styles', 'Theme', 'RealOutput', 'All')]
    [string]$Suite = 'Benchmark',
    [ValidateRange(1, 10)][int]$Runs = 3
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$mutatesSources = $Suite -in @('Source', 'Styles', 'RealOutput', 'All')
if ($mutatesSources -and !(Test-Path -LiteralPath (Join-Path $repo '.git') -PathType Leaf)) {
    throw 'Source-changing suites run in a linked Git worktree. Use Benchmark or Output in the primary checkout; see docs/storefront-current-development.md.'
}
$artifacts = Join-Path $repo ('runtime-verification/incremental-tests-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6))
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$project = 'src/Plugins/Nop.Plugin.Misc.PersianStorefront/Nop.Plugin.Misc.PersianStorefront.csproj'
$builds = [Collections.Generic.List[object]]::new()
$completed = [Collections.Generic.List[string]]::new()
$failure = $null

function Invoke-FullBuild([string]$Name) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    & dotnet build $project --configuration Debug -p:StorefrontTheme=All -maxcpucount:1 --verbosity minimal -clp:PerformanceSummary *> (Join-Path $artifacts "$Name.log")
    $code = $LASTEXITCODE
    $result = [pscustomobject]@{ name = $Name; seconds = [Math]::Round($timer.Elapsed.TotalSeconds, 3); exitCode = $code }
    $builds.Add($result)
    $builds | ConvertTo-Json | Set-Content (Join-Path $artifacts 'build-times.json')
    Write-Host ($result | ConvertTo-Json -Compress)
    if ($code -ne 0) { throw "Build failed; see $artifacts/$Name.log" }
}

Push-Location $repo
try {
    Write-Host "Verification artifacts: $artifacts"
    Invoke-FullBuild 'prepare'
    $suites = if ($Suite -eq 'All') { @('Output', 'Source', 'Styles', 'Theme', 'RealOutput', 'Benchmark') } else { @($Suite) }
    foreach ($current in $suites) {
        switch ($current) {
            'Benchmark' {
                foreach ($round in 1..$Runs) { Invoke-FullBuild "warm-$round" }
            }
            'RealOutput' {
                & (Join-Path $PSScriptRoot 'build-tests/verify-real-output.ps1') -ArtifactsDirectory $artifacts
            }
            default {
                & (Join-Path $PSScriptRoot 'build-tests/verify-incremental.ps1') -Suite $current -ArtifactsDirectory $artifacts
            }
        }
        $completed.Add($current)
    }
}
catch {
    $failure = $_.Exception.Message
    # Mutation helpers restore their original source bytes in finally. Restore
    # generated output as well if an assertion or compilation interrupted a suite.
    if ($mutatesSources) {
        & dotnet build $project --configuration Debug -p:StorefrontTheme=All -maxcpucount:1 --verbosity minimal *> (Join-Path $artifacts 'recovery-build.log')
    }
    throw
}
finally {
    [ordered]@{
        suite = $Suite
        completed = @($completed)
        passed = ($null -eq $failure)
        failure = $failure
        artifactsDirectory = $artifacts
    } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $artifacts 'result.json')
    Pop-Location
}
