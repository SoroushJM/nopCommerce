#!/usr/bin/env bash
set -euo pipefail
export STOREFRONT_DEPLOY_THEME=Madadrang
exec bash "$(dirname -- "$0")/deploy-storefront.sh" "$@"
