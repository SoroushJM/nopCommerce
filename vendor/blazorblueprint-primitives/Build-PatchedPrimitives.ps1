[CmdletBinding()]
param([Parameter(Mandatory)][string]$WorkDirectory)

$ErrorActionPreference = 'Stop'
$sourceCommit = '9534520512db530e640b7bca878d73f7c444874e'
$packageVersion = '4.1.1-storefront.1'
$workPath = [IO.Path]::GetFullPath($WorkDirectory)
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ($workPath.Equals($repositoryRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $workPath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Choose a fresh working directory outside the storefront checkout.'
}
if (Test-Path -LiteralPath $workPath) { throw 'The rebuild destination must not already exist.' }
$patchPath = Join-Path $PSScriptRoot 'sheet-dialog-exit.patch'
if (!(Test-Path -LiteralPath $patchPath -PathType Leaf)) { throw 'Missing reviewed source patch.' }

& git clone --no-checkout https://github.com/blazorblueprintui/ui.git $workPath
if ($LASTEXITCODE -ne 0) { throw 'Upstream source clone failed.' }
& git -C $workPath checkout --detach $sourceCommit
if ($LASTEXITCODE -ne 0) { throw 'Exact upstream source checkout failed.' }
$actualCommit = & git -C $workPath rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $actualCommit.Trim() -ne $sourceCommit) { throw 'Unexpected source revision.' }
& git -C $workPath apply --check $patchPath
if ($LASTEXITCODE -ne 0) { throw 'Reviewed patch does not apply to the pinned source.' }
& git -C $workPath apply $patchPath
if ($LASTEXITCODE -ne 0) { throw 'Patch application failed.' }

$packageOutput = Join-Path $workPath 'storefront-package'
$projectPath = Join-Path $workPath 'src/BlazorBlueprint.Primitives/BlazorBlueprint.Primitives.csproj'
& dotnet pack $projectPath -c Release -o $packageOutput '-p:UseSharedCompilation=false' "-p:MinVerVersionOverride=$packageVersion" '-p:StorefrontExitPatch=true'
if ($LASTEXITCODE -ne 0) { throw 'Patched primitive build/pack failed; source is retained for diagnosis.' }
$packagePath = Join-Path $packageOutput "BlazorBlueprint.Primitives.$packageVersion.nupkg"
if (!(Test-Path -LiteralPath $packagePath -PathType Leaf)) { throw 'Expected patched package is missing.' }
Get-FileHash -LiteralPath $packagePath -Algorithm SHA256
Write-Output "Rebuilt package: $packagePath"
