[CmdletBinding()]
param(
    [ValidateSet('Madadrang','Boutique','Both')][string]$Theme = 'Both',
    [string]$MadadrangAppPath,
    [string]$BoutiqueAppPath,
    [string]$PrivateConfig = 'C:/Users/s_jamili/Desktop/nopcomerce/dual-store-deployment-20261012/deploy.private.json',
    [string]$OutputDirectory = 'C:/Users/s_jamili/Desktop/nopcomerce/server-release-packages'
)
$ErrorActionPreference = 'Stop'
$themes = if ($Theme -eq 'Both') { @('Madadrang','Boutique') } else { @($Theme) }
foreach ($selected in $themes) {
    $appPath = if ($selected -eq 'Madadrang') { $MadadrangAppPath } else { $BoutiqueAppPath }
    if (!$appPath) {
        & (Join-Path $PSScriptRoot '../publish-storefront.ps1') -Theme $selected -Environment test -RuntimeIdentifier linux-x64 -OutputDirectory $OutputDirectory -HttpPort $(if ($selected -eq 'Madadrang') {9011} else {9012})
        $manifest = Get-ChildItem -LiteralPath $OutputDirectory -Directory -Filter "$($selected.ToLowerInvariant())-test-*" | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
        $appPath = (Get-Content -LiteralPath (Join-Path $manifest.FullName 'package.json') -Raw | ConvertFrom-Json).AppPath
    }
    & python (Join-Path $PSScriptRoot 'deploy.py') --theme $selected.ToLowerInvariant() --app $appPath --config $PrivateConfig
    if ($LASTEXITCODE -ne 0) { throw "Deployment failed for $selected; other application/database services were not recreated." }
}
