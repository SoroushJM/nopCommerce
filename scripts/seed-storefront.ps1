param(
    [string]$BaseUrl = 'http://localhost:5090',
    [Parameter(Mandatory = $true)][string]$AdminEmail,
    [Parameter(Mandatory = $true)][securestring]$AdminPassword
)
$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')
$pluginFile = Join-Path $PSScriptRoot '../src/Presentation/Nop.Web/App_Data/plugins.json'
if (Test-Path $pluginFile) {
    $plugins = Get-Content $pluginFile -Raw | ConvertFrom-Json
    if (($plugins.InstalledPluginNames -contains 'Misc.PersianStorefront') -or
        ($plugins.InstalledPlugins | Where-Object SystemName -eq 'Misc.PersianStorefront')) {
        Write-Output 'Persian storefront is already installed. Existing sample data is preserved.'
        return
    }
}
function Get-AntiforgeryToken([string]$Html) {
    $inputTag = [regex]::Match($Html, '<input[^>]*name="__RequestVerificationToken"[^>]*>').Value
    $token = [regex]::Match($inputTag, 'value="([^"]+)"').Groups[1].Value
    if (!$token) { throw 'The page did not provide an antiforgery token.' }
    return [System.Net.WebUtility]::HtmlDecode($token)
}
$login = Invoke-WebRequest "$BaseUrl/login" -SessionVariable seedSession
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
$page = Invoke-WebRequest "$BaseUrl/Admin/Plugin/List" -WebSession $seedSession
if ($page.Content -notmatch 'plugins-form-local') { throw 'Administrator authentication failed.' }
$null = Invoke-WebRequest "$BaseUrl/Admin/Plugin/List" -Method Post -WebSession $seedSession -Body @{
    'install-plugin-link-Misc.PersianStorefront' = 'Install'
    __RequestVerificationToken = (Get-AntiforgeryToken $page.Content)
}
$plugins = Get-Content $pluginFile -Raw | ConvertFrom-Json
if (!($plugins.PluginNamesToInstall | Where-Object { $_.Item1 -eq 'Misc.PersianStorefront' -or $_.SystemName -eq 'Misc.PersianStorefront' })) {
    throw 'The plugin installation was not queued. Inspect the administrator plugin page.'
}
Write-Output 'Persian storefront installation queued. Restart the Development application to seed the eight sample products.'
