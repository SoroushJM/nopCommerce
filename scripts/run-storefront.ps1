param([ValidateRange(1,65535)][int]$Port = 5090)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$webDirectory = Join-Path $repo 'src/Presentation/Nop.Web'
$webDll = Join-Path $webDirectory 'bin/Debug/net10.0/Nop.Web.dll'

foreach ($command in @('dotnet', 'npm')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "$command is required and must be available on PATH."
    }
}

$postgres = Get-Service -Name 'postgresql-x64-17' -ErrorAction SilentlyContinue
if ($postgres -and $postgres.Status -ne 'Running') {
    Write-Host 'Starting local PostgreSQL...'
    try {
        Start-Service -Name $postgres.Name
        $postgres.WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
    } catch {
        throw 'PostgreSQL could not start. Start its Windows service, or run this launcher as administrator.'
    }
}

# Restart only this checkout's server, never an unrelated process using the port.
$listeners = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty OwningProcess -Unique)
foreach ($listenerId in $listeners) {
    $server = Get-CimInstance Win32_Process -Filter "ProcessId=$listenerId"
    $isThisDll = $server.CommandLine -and $server.CommandLine.Contains($webDll)
    $isThisExecutable = $server.ExecutablePath -and
        $server.ExecutablePath.Equals((Join-Path $webDirectory 'bin/Debug/net10.0/Nop.Web.exe'), [StringComparison]::OrdinalIgnoreCase)
    if (-not ($isThisDll -or $isThisExecutable)) {
        throw "Port $Port is occupied by another process. Stop that process or choose -Port."
    }
    Write-Host 'Stopping the previous local site...'
    Stop-Process -Id $listenerId
    Wait-Process -Id $listenerId -ErrorAction SilentlyContinue
}

$oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
$oldDotnetEnvironment = $env:DOTNET_ENVIRONMENT
Push-Location $repo
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    Write-Host 'Building nopCommerce, the storefront plugin and frontend assets...'
    dotnet build (Join-Path $repo 'src/Plugins/Nop.Plugin.Misc.PersianStorefront/Nop.Plugin.Misc.PersianStorefront.csproj') -v minimal
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
    $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
    $env:DOTNET_ENVIRONMENT = $oldDotnetEnvironment
}
