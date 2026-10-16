#!/usr/bin/env bash
set -euo pipefail
export STOREFRONT_DEPLOY_THEME=MishaShop
exec bash "$(dirname -- "$0")/deploy-storefront.sh" "$@"
