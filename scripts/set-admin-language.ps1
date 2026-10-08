param(
    [ValidateSet('English', 'Automatic')][string]$Mode = 'English'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$path = Join-Path $repo 'src/Presentation/Nop.Web/App_Data/admin-language.override.json'
# This flag is checked per request, so recovery never needs a translated page or a restart.
@{ ForceEnglish = ($Mode -eq 'English') } | ConvertTo-Json | Set-Content -LiteralPath $path -Encoding utf8
if ($Mode -eq 'English') {
    Write-Output 'Admin is forced to English. Reload http://localhost:5090/admin. Storefront preferences and data are preserved.'
} else {
    Write-Output 'Emergency override disabled. Each administrator uses their saved choice, defaulting to Persian.'
}
