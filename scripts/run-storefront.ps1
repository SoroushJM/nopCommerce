param(
    [ValidateSet('Madadrang', 'MishaShop', 'Boutique')]
    [string]$Theme = 'Madadrang',
    [ValidateRange(1, 65535)]
    [int]$Port
)

$ErrorActionPreference = 'Stop'
$Theme = if ($Theme -in @('MishaShop', 'Boutique')) { 'MishaShop' } else { 'Madadrang' }
$repo = Split-Path $PSScriptRoot -Parent
$webDirectory = Join-Path $repo 'src/Presentation/Nop.Web'
$webDll = Join-Path $webDirectory 'bin/Debug/net10.0/Nop.Web.dll'
$webExecutable = Join-Path $webDirectory 'bin/Debug/net10.0/Nop.Web.exe'
$pluginProject = Join-Path $repo 'src/Plugins/Nop.Plugin.Misc.PersianStorefront/Nop.Plugin.Misc.PersianStorefront.csproj'

if (-not $PSBoundParameters.ContainsKey('Port')) {
    $Port = if ($Theme -eq 'MishaShop') { 5630 } else { 5602 }
}

foreach ($command in @('dotnet', 'npm')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "$command is required and must be available on PATH."
    }
}

function Test-CheckoutServer($Process) {
    $isThisDll = $Process.CommandLine -and
        $Process.CommandLine.IndexOf($webDll, [StringComparison]::OrdinalIgnoreCase) -ge 0
    $isThisExecutable = $Process.ExecutablePath -and
        $Process.ExecutablePath.Equals($webExecutable, [StringComparison]::OrdinalIgnoreCase)
    return $isThisDll -or $isThisExecutable
}

# A theme selects the UI, not a separate database or installation.
# Check the requested port before stopping any server from this checkout.
$listeners = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty OwningProcess -Unique)
foreach ($listenerId in $listeners) {
    $listenerProcess = Get-CimInstance Win32_Process -Filter "ProcessId=$listenerId"
    if (-not (Test-CheckoutServer $listenerProcess)) {
        throw "Port $Port is occupied by another application or checkout. Stop it or choose -Port."
    }
}

$postgres = Get-Service -Name 'postgresql-x64-17' -ErrorAction SilentlyContinue
if ($postgres -and $postgres.Status -ne 'Running') {
    Write-Host 'Starting local PostgreSQL...'
    try {
        Start-Service -Name $postgres.Name
        $postgres.WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
    } catch {
        throw 'PostgreSQL could not start. Start its Windows service, then run the launcher again.'
    }
}

# Switching themes can change the port, but the same checkout still shares build files.
$checkoutServers = @(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe' OR Name = 'Nop.Web.exe'" |
    Where-Object { Test-CheckoutServer $_ })
foreach ($checkoutServer in $checkoutServers) {
    Write-Host 'Stopping the previous server from this checkout...'
    Stop-Process -Id $checkoutServer.ProcessId
    Wait-Process -Id $checkoutServer.ProcessId -ErrorAction SilentlyContinue
}

$developmentEnvironment = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    DOTNET_ENVIRONMENT = 'Development'
    StorefrontThemeConfig__Theme = $Theme
    StorefrontHostConfig__HttpPort = "$Port"
    StorefrontHostConfig__HttpsPort = ''
    CommonConfig__UsePrecompiledViews = 'false'
}
$previousEnvironment = @{}
foreach ($name in $developmentEnvironment.Keys) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

Push-Location $repo
try {
    foreach ($name in $developmentEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $developmentEnvironment[$name], 'Process')
    }

    Write-Host "Building nopCommerce, the storefront plugin and $Theme assets..."
    dotnet build $pluginProject --configuration Debug "-p:StorefrontTheme=$Theme" -p:UseSharedCompilation=false -maxcpucount:1 --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed. The site was not started.' }

    Write-Host "Store: http://localhost:$Port/"
    Write-Host "Admin: http://localhost:$Port/admin"
    Write-Host 'Keep this window open. Press Ctrl+C to stop the site.'
    Push-Location $webDirectory
    try {
        dotnet $webDll --urls "http://localhost:$Port"
        if ($LASTEXITCODE -ne 0) { throw "Site exited with code $LASTEXITCODE." }
    } finally { Pop-Location }
} finally {
    Pop-Location
    foreach ($name in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
}
