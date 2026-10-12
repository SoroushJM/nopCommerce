param([Parameter(Mandatory)][string]$SqlFile, [string]$OutputFile)
$ErrorActionPreference = 'Stop'
$qaApp = 'C:/Users/s_jamili/Desktop/nopcomerce/theme-build-checks/runtime-smoke-boutique-1e5498f14ffc47e486403f68e5162ca4/app'
$qaDatabase = 'nop_themeqa_boutique_20261012185304'
$settings = Get-Content (Join-Path $qaApp 'App_Data/appsettings.json') -Raw | ConvertFrom-Json
if ($settings.ConnectionStrings.DataProvider -ne 'postgresql') { throw 'Unexpected provider.' }
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection.set_ConnectionString($settings.ConnectionStrings.ConnectionString)
if ($connection['database'] -ne $qaDatabase) { throw 'Refusing a database other than the exact isolated Boutique QA database.' }
if ($connection['host'] -notin @('localhost','127.0.0.1','::1')) { throw 'Refusing a non-loopback database host.' }
$previousPassword = $env:PGPASSWORD
try {
    $env:PGPASSWORD = $connection['password']
    $arguments = @('-X','-v','ON_ERROR_STOP=1','-h',$connection['host'],'-p',$connection['port'],'-U',$connection['username'],'-d',$qaDatabase,'-f',(Resolve-Path -LiteralPath $SqlFile).Path)
    if ($OutputFile) { $arguments += @('-o',$OutputFile) }
    & 'C:/Program Files/PostgreSQL/18/bin/psql.exe' @arguments
    if ($LASTEXITCODE -ne 0) { throw 'QA SQL execution failed.' }
} finally { $env:PGPASSWORD = $previousPassword; $connection.Clear() }
