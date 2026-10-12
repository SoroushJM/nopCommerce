param([switch]$Apply)
$ErrorActionPreference='Stop'
& python (Join-Path $PSScriptRoot 'generate-media.py')
if ($LASTEXITCODE -ne 0) { throw 'Media generation failed.' }
& python (Join-Path $PSScriptRoot 'build-seed.py')
if ($LASTEXITCODE -ne 0) { throw 'SQL preparation failed.' }
if (-not $Apply) { Write-Output 'Dry run: pass -Apply to seed the exact isolated Boutique QA database.'; return }
$qaApp='C:/Users/s_jamili/Desktop/nopcomerce/theme-build-checks/runtime-smoke-boutique-1e5498f14ffc47e486403f68e5162ca4/app'
& (Join-Path $PSScriptRoot 'Invoke-QaSql.ps1') -SqlFile (Join-Path $PSScriptRoot 'seed.generated.sql')
$mediaTarget=Join-Path $qaApp 'wwwroot/images/boutique-sample'
New-Item -ItemType Directory -Force -Path $mediaTarget | Out-Null
Copy-Item -LiteralPath (Get-ChildItem (Join-Path $PSScriptRoot 'media') -Filter '*.svg').FullName -Destination $mediaTarget
Write-Output 'Seed transaction completed and original SVGs copied to the existing Boutique QA clone. Ask its owner to refresh application caches/restart; this script never starts or stops a server.'
