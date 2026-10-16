#!/usr/bin/env bash
set -euo pipefail
project=$1 release=$2 theme=$3 image=$4 digest=$5 timeout=$6
case "$theme" in Madadrang) slug=madadrang;; MishaShop) slug=mishashop;; *) exit 2;; esac
cd -- "$project"
exec 9> .storefront-deploy.lock
flock -n 9 || { echo 'Another deployment holds the project lock.' >&2; exit 1; }
# Stage transfers independently, but publish every active input under the same
# lock held through deployment. Never overwrite another shop or unknown layout.
if [[ -f compose.yml || ( -f docker-compose.yml && ! -f .storefront-selfhost ) ]]; then
  echo 'Existing deployment layout requires explicit migration.' >&2; exit 1
fi
if [[ -f .storefront-selfhost && $(cat .storefront-selfhost) != "$theme" ]]; then
  echo 'This directory belongs to a different shop.' >&2; exit 1
fi
(cd -- "$release"; printf '%s  image.tar\n' "$digest" | sha256sum -c -)
docker image load --input "$release/image.tar"
if [[ ! -s .env ]]; then
  [[ -s "$release/.env" ]] || { echo 'A fresh project needs its private .env.' >&2; exit 1; }
  install -m 600 "$release/.env" .env
fi
# Validate the staged Compose inputs against the preserved environment before
# replacing active files. An explicit project name also avoids release-dir names.
configured_theme=$(docker compose --project-directory "$PWD" --env-file "$PWD/.env" -f "$release/docker-compose.yml" config --format json | python3 -c 'import sys,json; print(json.load(sys.stdin)["services"]["nopcommerce_web"]["environment"]["StorefrontThemeConfig__Theme"])')
[[ $configured_theme == "$theme" || ( $theme == MishaShop && $configured_theme == Boutique ) ]] || { echo 'The environment belongs to a different shop.' >&2; exit 1; }
mkdir -p docker scripts
cp -- "$release/docker-compose.yml" "$release/Dockerfile" .
cp -- "$release/docker/postgres-init.sql" "$release/docker/create-certificate.sh" docker/
cp -- "$release/scripts/deploy-storefront.sh" "$release/scripts/deploy-$slug.sh" scripts/
printf '%s\n' "$theme" > .storefront-selfhost
rm -f -- "$release/.env"
export STOREFRONT_DEPLOY_LOCK_HELD=1
exec bash "scripts/deploy-$slug.sh" --project-dir "$PWD" --image "$image" --non-interactive --timeout "$timeout"
