param(
    [string]$BaseUrl = 'http://localhost:5602',
    [string]$AdminEmail,
    [securestring]$AdminPassword,
    [ValidateSet('Madadrang','MishaShop','Boutique')][string]$Theme = 'Madadrang',
    [string]$AppPath = (Join-Path $PSScriptRoot '../src/Presentation/Nop.Web'),
    [string]$CredentialsPath = (Join-Path $env:LOCALAPPDATA 'MadadrangDev/local-settings.json'),
    [string]$SmsIrSettingsPath = (Join-Path $env:LOCALAPPDATA 'MadadrangDev/smsir-local.json'),
    [switch]$AdminOnly
)
$ErrorActionPreference = 'Stop'
$Theme = if ($Theme -in @('MishaShop', 'Boutique')) { 'MishaShop' } else { 'Madadrang' }
if (!$PSBoundParameters.ContainsKey('BaseUrl')) { $BaseUrl = if ($Theme -eq 'MishaShop') { 'http://localhost:5630' } else { 'http://localhost:5602' } }
if ($AdminOnly -or (Test-Path -LiteralPath $CredentialsPath)) {
    # The UI theme does not select a DB. This checkout's local launcher shares
    # nop_stationery_dev; the older isolated Misha runtime uses its guarded DB.
    $provisioningTheme = if ($Theme -eq 'MishaShop') { 'boutique' } else { 'madadrang' }
    $appSettingsPath = Join-Path $AppPath 'App_Data/appsettings.json'
    if (Test-Path -LiteralPath $appSettingsPath) {
        $localConnection = (Get-Content -LiteralPath $appSettingsPath -Raw | ConvertFrom-Json).ConnectionStrings.ConnectionString
        $databaseName = [regex]::Match($localConnection, '(?i)(?:^|;)\s*Database\s*=\s*([^;]+)').Groups[1].Value.Trim()
        if ($databaseName -eq 'nop_stationery_dev') { $provisioningTheme = 'madadrang' }
        elseif ($databaseName -eq 'nop_themeqa_boutique_20261012185304') { $provisioningTheme = 'boutique' }
        $localConnection = $null
    }
    & python (Join-Path $PSScriptRoot 'DeployTestServer/native-admin.py') $provisioningTheme --local-app $AppPath --credentials $CredentialsPath
    if ($LASTEXITCODE -ne 0) { throw 'Local administrator provisioning failed.' }
    if ($AdminOnly) { return }
}
$BaseUrl = $BaseUrl.TrimEnd('/')
$pluginFile = Join-Path $AppPath 'App_Data/plugins.json'
& (Join-Path $PSScriptRoot 'initialize-storefront-plugins.ps1') -AppPath $AppPath
$requiredPlugins = @('Misc.PersianStorefront','Sms.SmsIr')
$installedNames = @()
if (Test-Path $pluginFile) {
    $plugins = Get-Content $pluginFile -Raw | ConvertFrom-Json
    $installedNames = @($plugins.InstalledPluginNames) + @($plugins.InstalledPlugins | ForEach-Object SystemName)
}
if (@($requiredPlugins | Where-Object { $_ -notin $installedNames }).Count -gt 0) {
    Write-Output 'Required plugins queued. Restart using start-local.cmd, then rerun seed-storefront.ps1 to configure SMS.ir. Existing catalog data is preserved.'
    return
}
if (!(Test-Path -LiteralPath $SmsIrSettingsPath)) { Write-Output 'Both plugins are installed. Configure SMS.ir in Admin > SMS providers.'; return }
if ((!$AdminEmail -or !$AdminPassword) -and (Test-Path -LiteralPath $CredentialsPath)) {
    $localCredentials = Get-Content -LiteralPath $CredentialsPath -Raw | ConvertFrom-Json
    $AdminEmail = $localCredentials.AdminEmail
    $AdminPassword = ConvertTo-SecureString $localCredentials.AdminPassword -AsPlainText -Force
    $localCredentials = $null
}
if (!$AdminEmail -or !$AdminPassword) { throw 'Plugin installation requires AdminEmail and SecureString AdminPassword; use -AdminOnly for local provisioning without HTTP.' }
function Get-AntiforgeryToken([string]$Html) {
    $inputTag = [regex]::Match($Html, '<input[^>]*name="__RequestVerificationToken"[^>]*>').Value
    $token = [regex]::Match($inputTag, 'value="([^"]+)"').Groups[1].Value
    if (!$token) { throw 'The page did not provide an antiforgery token.' }
    return [System.Net.WebUtility]::HtmlDecode($token)
}
$login = Invoke-WebRequest "$BaseUrl/login" -SessionVariable seedSession -UserAgent 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130.0.0.0 Safari/537.36'
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($AdminPassword)
try {
    $passwordText = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    $null = Invoke-WebRequest "$BaseUrl/login" -Method Post -WebSession $seedSession -Body @{
        Email = $AdminEmail
        Password = $passwordText
        RememberMe = 'false'
        __RequestVerificationToken = (Get-AntiforgeryToken $login.Content)
    }
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    $passwordText = $null
}
$page = Invoke-WebRequest "$BaseUrl/Admin/SmsIr/Configure" -WebSession $seedSession
if ($page.Content -notmatch 'name="ApiKey"') { throw 'Administrator SMS configuration page was not available.' }
if ($page.Content -match 'name="HasApiKey"\s+value="true"') { Write-Output 'Both plugins installed; existing administrator SMS settings preserved.'; return }
$smsSettings = Get-Content -LiteralPath $SmsIrSettingsPath -Raw | ConvertFrom-Json
try {
    $null = Invoke-WebRequest "$BaseUrl/Admin/SmsIr/Configure" -Method Post -WebSession $seedSession -Body @{
        ApiKey = $smsSettings.ApiKey
        Sandbox = "$($smsSettings.Sandbox)".ToLowerInvariant()
        TemplateId = $smsSettings.TemplateId
        CodeParameterName = $smsSettings.CodeParameterName
        LineNumber = 0
        __RequestVerificationToken = (Get-AntiforgeryToken $page.Content)
    }
} finally { $smsSettings = $null }
$verified = Invoke-WebRequest "$BaseUrl/Admin/SmsIr/Configure" -WebSession $seedSession
if ($verified.Content -notmatch 'name="HasApiKey"\s+value="true"') { throw 'SMS.ir settings were not saved.' }
Write-Output 'Both plugins installed; local SMS.ir configuration initialized. No SMS was sent by the seed.'
