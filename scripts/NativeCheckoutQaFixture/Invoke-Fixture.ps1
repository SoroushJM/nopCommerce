param([Parameter(Mandatory)][ValidateSet('Boutique','Madadrang')][string]$Theme,
 [Parameter(Mandatory)][ValidateSet('Prepare','Configure','Verify','ValidateRestore','Restore')][string]$Phase,
 [switch]$Apply)
$ErrorActionPreference='Stop'
$targets=@{
 Boutique=@{App='C:/Users/s_jamili/Desktop/nopcomerce/theme-build-checks/runtime-smoke-boutique-1e5498f14ffc47e486403f68e5162ca4/app';Db='nop_themeqa_boutique_20261012185304'}
 Madadrang=@{App='C:/Users/s_jamili/Desktop/nopcomerce/theme-build-checks/runtime-smoke-madadrang-3dd9cb04406e46a6b1161eda95a9073b/app';Db='nop_themeqa_madadrang_20261012185304'}
}
$target=$targets[$Theme]
$config=Get-Content (Join-Path $target.App 'App_Data/appsettings.json') -Raw|ConvertFrom-Json
$connection=[System.Data.Common.DbConnectionStringBuilder]::new()
$connection.set_ConnectionString($config.ConnectionStrings.ConnectionString)
if($config.ConnectionStrings.DataProvider -ne 'postgresql' -or $connection['database'] -ne $target.Db -or $connection['host'] -notin @('localhost','127.0.0.1','::1')){throw 'Exact QA connection guard failed.'}
$pluginFile=Join-Path $target.App 'App_Data/plugins.json'
$plugins=Get-Content $pluginFile -Raw|ConvertFrom-Json
$names=@('Payments.CheckMoneyOrder','Shipping.FixedByWeightByTotal')
$state=Join-Path $target.App 'App_Data/native-checkout-qa-v1'
$beforeFile=Join-Path $state 'plugins.before.json'
foreach($name in $names){
 $descriptorFile=Join-Path $target.App ('Plugins/'+$name+'/plugin.json')
 $descriptor=Get-Content $descriptorFile -Raw|ConvertFrom-Json
 if($descriptor.SystemName -ne $name -or '5.00' -notin $descriptor.SupportedVersions -or !(Test-Path -LiteralPath (Join-Path (Split-Path $descriptorFile) $descriptor.FileName))){throw 'Native plugin binary/descriptor guard failed.'}
}
if($Phase -eq 'Configure' -and @($names|Where-Object {$_ -notin @($plugins.InstalledPlugins.SystemName)}).Count){throw 'Both native plugins must complete real startup installation before configure.'}
if(!$Apply){Write-Output ('Dry run: '+$Theme+' '+$Phase+' exact isolated database '+$target.Db);$connection.Clear();return}
if($Phase -in @('Prepare','Restore')){
 $ports=if($Theme -eq 'Boutique'){@(6250,6251)}else{@(6240,6241)}
 if(@(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue|Where-Object {$_.LocalPort -in $ports}).Count){throw 'Server owner must stop this exact QA host before metadata changes.'}
}
if($Phase -eq 'Prepare'){
 if(@($plugins.PluginNamesToInstall|Where-Object {$_.Item1 -notin $names}).Count){throw 'Unexpected pending plugin install.'}
 if(@($plugins.PluginNamesToUninstall).Count -or @($plugins.PluginNamesToDelete).Count){throw 'Unexpected pending plugin maintenance.'}
 if(!(Test-Path -LiteralPath $beforeFile)){
  New-Item -ItemType Directory -Path $state -Force|Out-Null
  # Metadata only; never copy private connection settings.
  Get-Content -LiteralPath $pluginFile -Raw|Set-Content -LiteralPath $beforeFile -Encoding utf8
 }
}
if($Phase -in @('ValidateRestore','Restore')){
 if(!(Test-Path -LiteralPath $beforeFile)){throw 'Original metadata snapshot is missing.'}
 $beforePlugins=Get-Content -LiteralPath $beforeFile -Raw|ConvertFrom-Json
 if(@($plugins.PluginNamesToInstall).Count -or @($plugins.PluginNamesToUninstall).Count -or @($plugins.PluginNamesToDelete).Count){throw 'Pending plugin changes prevent rollback.'}
 $baseline=@($beforePlugins.InstalledPlugins|ForEach-Object {$_.SystemName+'|'+$_.Version}|Sort-Object)
 $currentBaseline=@($plugins.InstalledPlugins|Where-Object {$_.SystemName -notin $names}|ForEach-Object {$_.SystemName+'|'+$_.Version}|Sort-Object)
 if(($baseline -join ';') -ne ($currentBaseline -join ';') -or @($plugins.InstalledPlugins).Count -ne ($baseline.Count+2)){throw 'Unrelated plugin metadata changed; refuse rollback.'}
 foreach($name in $names){
  $descriptor=Get-Content (Join-Path $target.App ('Plugins/'+$name+'/plugin.json')) -Raw|ConvertFrom-Json
  if(@($plugins.InstalledPlugins|Where-Object {$_.SystemName -eq $name -and $_.Version -eq $descriptor.Version}).Count -ne 1){throw 'Native installed version changed; refuse rollback.'}
 }
}
$sqlPhase=if($Phase -eq 'ValidateRestore'){'Restore'}else{$Phase}
$phaseSql=Get-Content (Join-Path $PSScriptRoot ($sqlPhase+'.sql')) -Raw
if($Phase -eq 'ValidateRestore'){$phaseSql=$phaseSql.Replace('COMMIT;','ROLLBACK;')}
$sql=if($Phase -eq 'Verify'){$phaseSql}else{(Get-Content (Join-Path $PSScriptRoot 'Common.sql') -Raw)+[Environment]::NewLine+$phaseSql}
$sqlFile=Join-Path $state ('fixture-'+$Phase+'.sql')
Set-Content -LiteralPath $sqlFile -Value $sql -Encoding utf8
$oldPassword=$env:PGPASSWORD
try{
 $env:PGPASSWORD=$connection['password']
 & 'C:/Program Files/PostgreSQL/18/bin/psql.exe' -X -v ON_ERROR_STOP=1 -h $connection['host'] -p $connection['port'] -U $connection['username'] -d $target.Db -f $sqlFile
 if($LASTEXITCODE -ne 0){throw 'Guarded fixture transaction failed.'}
}finally{$env:PGPASSWORD=$oldPassword;$connection.Clear()}
if($Phase -eq 'Prepare'){
 $pending=@($plugins.PluginNamesToInstall)
 if(@($pending|Where-Object {$_.Item1 -notin $names}).Count){throw 'Unexpected pending plugin install.'}
 foreach($name in $names){if($name -notin @($plugins.InstalledPlugins.SystemName) -and $name -notin @($pending.Item1)){$pending+=@{Item1=$name;Item2=$null}}}
 $plugins.PluginNamesToInstall=$pending
 $plugins|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $pluginFile -Encoding utf8
 Write-Output ('Native startup install queued for '+$Theme+'; all message templates/email task disabled. Server owner performs restart.')
}
if($Phase -eq 'Restore'){
 # Caller stops this exact QA host before rollback; preserve order/customer evidence.
 Get-Content -LiteralPath $beforeFile -Raw|Set-Content -LiteralPath $pluginFile -Encoding utf8
 Write-Output ('QA native fixture restored for '+$Theme+'; server owner refreshes process/cache.')
}
if($Phase -eq 'ValidateRestore'){Write-Output ('Rollback validation passed for '+$Theme+'; every temporary restore change was ROLLBACK, live fixture remains configured.')}

