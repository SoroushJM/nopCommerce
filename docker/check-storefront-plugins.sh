#!/bin/sh
set -eu
registry=${1:-/app/App_Data/plugins.json}
jq -e '
 ([.InstalledPlugins[]?.SystemName] + (.InstalledPluginNames // [])) as $installed |
 (["Misc.PersianStorefront", "Sms.SmsIr"] - $installed | length) == 0 and
 ([.PluginNamesToInstall[]? | (.Item1 // .SystemName)] | map(select(. == "Misc.PersianStorefront" or . == "Sms.SmsIr")) | length) == 0
' "$registry" >/dev/null
state="$(dirname "$registry")/smsir-runtime-status.json"
if [ ! -s "$state" ]; then echo 'SMS.ir runtime configuration status is missing.' >&2; exit 1; fi
jq -e '(.HasKey | type) == "boolean" and (.Usable | type) == "boolean"' "$state" >/dev/null
if jq -e '.HasKey == true and .Usable != true' "$state" >/dev/null; then
  echo 'Saved SMS.ir settings cannot be used in this environment; check the admin plugin configuration.' >&2
  exit 1
fi
if jq -e '.HasKey != true' "$state" >/dev/null; then
  echo 'SMS.ir installed; administrator configuration is required before customer SMS login is available.'
fi
