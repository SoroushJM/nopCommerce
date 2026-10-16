#!/bin/sh
set -eu
cd /app
registry=App_Data/plugins.json
mkdir -p App_Data
# Queue through nopCommerce's real installer; never fabricate installed metadata.
if [ ! -f "$registry" ]; then printf '%s\n' '{}' > "$registry"; fi
pending=$(mktemp App_Data/plugins.XXXXXX)
trap 'rm -f "$pending"' EXIT
jq '
  .PluginNamesToInstall = (.PluginNamesToInstall // []) |
  reduce ["Misc.PersianStorefront", "Sms.SmsIr"][] as $name (.;
    if (([.InstalledPlugins[]?.SystemName] + (.InstalledPluginNames // [])) | index($name)) != null
       or ([.PluginNamesToInstall[]? | (.Item1 // .SystemName)] | index($name)) != null
    then . else .PluginNamesToInstall += [{"Item1": $name, "Item2": null}] end)
' "$registry" > "$pending"
mv "$pending" "$registry"
trap - EXIT
exec dotnet Nop.Web.dll "$@"
