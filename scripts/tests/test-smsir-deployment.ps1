# No Docker daemon, SSH server, SMS account or database is required.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scripts = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('smsir-deployment-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
function Assert([bool]$Condition, [string]$Message) { if (!$Condition) { throw $Message } }
try {
    $app = Join-Path $testRoot 'app'
    foreach ($name in @('Misc.PersianStorefront','Sms.SmsIr')) {
        $directory = Join-Path $app "Plugins/$name"
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $directory 'plugin.json'), '{"SystemName":"' + $name + '"}')
    }
    & (Join-Path $scripts 'initialize-storefront-plugins.ps1') -AppPath $app | Out-Null
    $registry = Join-Path $app 'App_Data/plugins.json'
    $state = Get-Content -LiteralPath $registry -Raw | ConvertFrom-Json
    Assert ($state.PluginNamesToInstall.Count -eq 2) 'A fresh instance must queue both required plugins.'
    $state.InstalledPlugins = @([pscustomobject]@{ SystemName='Misc.PersianStorefront'; Version='5.00.custom'; Custom='preserve' })
    $state.PluginNamesToInstall = @([pscustomobject]@{ Item1='Other.Plugin'; Item2='user-id' })
    $state | Add-Member -NotePropertyName PluginNamesToDelete -NotePropertyValue @('Keep.Delete.Queue')
    $state | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $registry
    1..2 | ForEach-Object { & (Join-Path $scripts 'initialize-storefront-plugins.ps1') -AppPath $app | Out-Null }
    $state = Get-Content -LiteralPath $registry -Raw | ConvertFrom-Json
    Assert ($state.InstalledPlugins[0].Version -eq '5.00.custom' -and $state.InstalledPlugins[0].Custom -eq 'preserve') 'Installed metadata must remain intact.'
    Assert ($state.PluginNamesToInstall.Count -eq 2 -and $state.PluginNamesToInstall[0].Item2 -eq 'user-id') 'Repeated initialization must preserve unrelated queues without duplication.'
    Assert ($state.PluginNamesToInstall[1].Item1 -eq 'Sms.SmsIr' -and $state.PluginNamesToDelete[0] -eq 'Keep.Delete.Queue') 'Only the missing SMS plugin should be queued.'
    . (Join-Path $scripts 'resolve-storefront-deployment.ps1')
    $private = Join-Path $testRoot 'private'
    $failed = $false
    try { Resolve-StorefrontDeploymentInput -PrivateDirectory $private -NonInteractive | Out-Null } catch { $failed = $_.Exception.Message -like 'Missing deployment parameter:*' }
    Assert $failed 'Non-interactive deployment must reject missing inputs.'
    $config = Join-Path $testRoot 'input.json'
    @{ host='old.example'; username='deployer'; remoteRoot='/srv/shop'; keyFilename='identity'; knownHostsPath='known_hosts' } | ConvertTo-Json | Set-Content -LiteralPath $config
    $effective = Resolve-StorefrontDeploymentInput -ConfigPath $config -PrivateDirectory $private -Server 'new.example' -SshPort 2222 -NonInteractive
    $resolved = Get-Content -LiteralPath $effective -Raw | ConvertFrom-Json
    Assert ($resolved.host -eq 'new.example' -and $resolved.port -eq 2222) 'Explicit parameters must override private config.'
    Assert ($resolved.keyFilename -eq (Join-Path $testRoot 'identity')) 'Relative identity paths must resolve beside the config.'
    $seedApp = Join-Path $testRoot 'seed-app'
    New-Item -ItemType Directory -Path (Join-Path $seedApp 'App_Data') -Force | Out-Null
    # Stub the external provisioner: verify DB selection without any SQL writes.
    $global:SmsIrDeploymentTestPythonArgs = @()
    function python { $global:SmsIrDeploymentTestPythonArgs = @($args); $global:LASTEXITCODE = 0 }
    @{ ConnectionStrings=@{ ConnectionString='Host=localhost;Database=nop_stationery_dev;' } } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $seedApp 'App_Data/appsettings.json')
    & (Join-Path $scripts 'seed-storefront.ps1') -Theme MishaShop -AppPath $seedApp -CredentialsPath (Join-Path $testRoot 'credentials.json') -AdminOnly
    Assert ($global:SmsIrDeploymentTestPythonArgs[1] -eq 'madadrang') 'Misha UI on the shared local DB must use that database guard.'
    @{ ConnectionStrings=@{ ConnectionString='Host=localhost;Database=nop_themeqa_boutique_20261012185304;' } } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $seedApp 'App_Data/appsettings.json')
    & (Join-Path $scripts 'seed-storefront.ps1') -Theme MishaShop -AppPath $seedApp -CredentialsPath (Join-Path $testRoot 'credentials.json') -AdminOnly
    Assert ($global:SmsIrDeploymentTestPythonArgs[1] -eq 'boutique') 'The older isolated Misha DB must retain its original guard.'
    Write-Output 'PASS: fresh/idempotent plugin queue, metadata preservation, missing-input failure, parameter precedence and local DB selection for both Misha layouts.'
} finally {
    $absolute = [IO.Path]::GetFullPath($testRoot)
    $parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (!$absolute.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($absolute) -notlike 'smsir-deployment-test-*') { throw 'Unexpected temporary test directory.' }
    Remove-Item -LiteralPath $absolute -Recurse -Force
}
