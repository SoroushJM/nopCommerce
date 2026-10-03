param([int]$Port=5090)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$env:ASPNETCORE_ENVIRONMENT='Development'
Push-Location $repo
try {
 dotnet build src/Plugins/Nop.Plugin.Misc.PersianStorefront -v minimal
 if($LASTEXITCODE -ne 0){throw 'Storefront build failed'}
 Push-Location src/Presentation/Nop.Web
 try {dotnet run --no-build --no-launch-profile --urls "http://localhost:$Port"} finally {Pop-Location}
} finally {Pop-Location}
