[CmdletBinding()]
param(
    [string]$ConfigPath,
    [string]$OutputDirectory,
    [string]$Server,
    [string]$SshUser,
    [ValidateRange(0,65535)][int]$SshPort = 0,
    [string]$IdentityFile,
    [string]$KnownHostsPath,
    [string]$RemoteRoot,
    [string]$EnvironmentFile,
    [switch]$NonInteractive,
    [ValidateRange(30, 1800)][int]$StartupTimeout = 180,
    [switch]$KeepBuildArtifacts
)

$ErrorActionPreference = 'Stop'
$deploymentScript = Join-Path $PSScriptRoot 'deploy-selfhost.ps1'
& $deploymentScript -Theme Madadrang @PSBoundParameters
