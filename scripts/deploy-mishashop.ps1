[CmdletBinding()]
param(
    [string]$ConfigPath,
    [string]$OutputDirectory,
    [ValidateRange(30, 1800)][int]$StartupTimeout = 180,
    [switch]$KeepBuildArtifacts
)

$ErrorActionPreference = 'Stop'
$deploymentScript = Join-Path $PSScriptRoot 'deploy-storefront.ps1'
& $deploymentScript -Theme MishaShop @PSBoundParameters
