param([Parameter(Mandatory)][string]$AppPath)
$ErrorActionPreference = 'Stop'
$data = Join-Path $AppPath 'App_Data'
$registry = Join-Path $data 'plugins.json'
New-Item -ItemType Directory -Path $data -Force | Out-Null
$plugins = if (Test-Path -LiteralPath $registry) { Get-Content -LiteralPath $registry -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
foreach ($field in @('InstalledPluginNames','InstalledPlugins','PluginNamesToInstall')) {
    if (!$plugins.PSObject.Properties[$field]) { $plugins | Add-Member -NotePropertyName $field -NotePropertyValue @() }
}
$installed = @($plugins.InstalledPluginNames) + @($plugins.InstalledPlugins | ForEach-Object { $_.SystemName })
$queue = @($plugins.PluginNamesToInstall | Where-Object { $null -ne $_ })
foreach ($name in @('Misc.PersianStorefront','Sms.SmsIr')) {
    if ($name -notin $installed -and !($queue | Where-Object { ($_.PSObject.Properties['Item1'] -and $_.Item1 -eq $name) -or ($_.PSObject.Properties['SystemName'] -and $_.SystemName -eq $name) })) {
        if (!(Test-Path -LiteralPath (Join-Path $AppPath "Plugins/$name/plugin.json"))) { throw "Build the $name plugin before initialization." }
        $queue += [pscustomobject]@{ Item1 = $name; Item2 = $null }
    }
}
$plugins | Add-Member -NotePropertyName PluginNamesToInstall -NotePropertyValue $queue -Force
$temporary = "$registry.$([guid]::NewGuid().ToString('N')).tmp"
try {
    [IO.File]::WriteAllText($temporary, ($plugins | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporary -Destination $registry -Force
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
Write-Output 'Storefront and SMS.ir installation queues prepared; installed plugins and settings preserved.'
