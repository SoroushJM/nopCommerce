function Resolve-StorefrontDeploymentInput {
    param([string]$ConfigPath, [string]$PrivateDirectory, [string]$Server, [string]$SshUser,
        [int]$SshPort, [string]$IdentityFile, [string]$KnownHostsPath, [string]$RemoteRoot, [switch]$NonInteractive)
    $checkout = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd([IO.Path]::DirectorySeparatorChar)
    foreach ($candidate in @($ConfigPath, $PrivateDirectory)) {
        if ($candidate) {
            $absolute = [IO.Path]::GetFullPath($candidate)
            if ($absolute -eq $checkout -or $absolute.StartsWith($checkout + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Deployment configuration and private output must be outside the checkout.'
            }
        }
    }
    $settings = if ($ConfigPath) {
        Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json -AsHashtable
    } else { @{} }
    foreach ($name in @('host','username','keyFilename','knownHostsPath','remoteRoot','handoffPath','port','imageRepository')) {
        if (!$settings.ContainsKey($name)) { $settings[$name] = $null }
    }
    $overrides = @{ host=$Server; username=$SshUser; keyFilename=$IdentityFile; knownHostsPath=$KnownHostsPath; remoteRoot=$RemoteRoot }
    foreach ($name in $overrides.Keys) { if ($overrides[$name]) { $settings[$name] = $overrides[$name] } }
    if ($SshPort) { $settings.port = $SshPort }
    # Existing relative private-file paths refer to the supplied configuration.
    if ($ConfigPath) {
        foreach ($name in @('keyFilename','knownHostsPath','handoffPath')) {
            if ($settings[$name] -and ![IO.Path]::IsPathRooted($settings[$name])) {
                $settings[$name] = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent ([IO.Path]::GetFullPath($ConfigPath))) $settings[$name]))
            }
        }
    }
    $prompts = [ordered]@{ host='Linux server hostname or IP'; username='SSH username'; remoteRoot='Existing absolute Compose project directory on Linux'; knownHostsPath='Local known_hosts file containing the verified server key' }
    if (!$settings.keyFilename -and !$settings.handoffPath) { $prompts.keyFilename = 'Local SSH private key file' }
    foreach ($name in $prompts.Keys) {
        if (!$settings[$name]) {
            if ($NonInteractive) { throw "Missing deployment parameter: $name. Supply parameters or -ConfigPath." }
            $settings[$name] = Read-Host $prompts[$name]
            if ([string]::IsNullOrWhiteSpace($settings[$name])) { throw "Deployment parameter $name cannot be empty." }
        }
    }
    if (!$settings.port) { $settings.port = 22 }
    if (!$settings.imageRepository) { $settings.imageRepository = 'nop-dual' }
    New-Item -ItemType Directory -Path $PrivateDirectory -Force | Out-Null
    $effective = Join-Path $PrivateDirectory ("effective-$([guid]::NewGuid().ToString('N')).json")
    [IO.File]::WriteAllText($effective, ($settings | ConvertTo-Json -Depth 10))
    return $effective
}
