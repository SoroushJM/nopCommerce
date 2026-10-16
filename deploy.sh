#!/bin/sh
set -eu
cd -- "$(dirname -- "$0")"
exec bash scripts/deploy-madadrang.sh "$@"
