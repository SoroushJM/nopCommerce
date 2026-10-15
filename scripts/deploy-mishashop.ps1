[CmdletBinding()]
param(
    [string]$ConfigPath,
    [string]$OutputDirectory,
    [string]$Python = 'python',
    [ValidateRange(30, 1800)][int]$StartupTimeout = 180,
    [switch]$KeepBuildArtifacts
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'deploy-storefront.ps1') -Theme Boutique @PSBoundParameters
