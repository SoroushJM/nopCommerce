#!/usr/bin/env bash
set -euo pipefail
usage() {
  cat <<'HELP'
Usage: bash scripts/deploy-{madadrang,mishashop}.sh [options]
  --project-dir PATH    Compose project on this Linux host (default: repository root)
  --image TAG           Deploy an already loaded image
  --archive FILE        Load a docker image-save archive first (requires --image)
  --build               Build the selected shop image on this Linux host
  --non-interactive     Fail on missing inputs instead of prompting
  --timeout SECONDS     Startup timeout (default: 240)
  --help                Print help without changing anything
The project needs its own .env. Databases, uploads and App_Data remain in named volumes.
Fresh databases use the native web installer; plugin installation follows its restart.
HELP
}
theme=${STOREFRONT_DEPLOY_THEME:-}
project=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
image= archive= build=false interactive=true timeout=240
while (($#)); do
  case "$1" in
    --help|-h) usage; exit 0;;
    --non-interactive) interactive=false; shift;;
    --build) build=true; shift;;
    --project-dir|--image|--archive|--timeout)
      (($# >= 2)) || { usage >&2; exit 2; }
      case "$1" in --project-dir) project=$2;; --image) image=$2;; --archive) archive=$2;; --timeout) timeout=$2;; esac
      shift 2;;
    *) echo "Unknown option: $1" >&2; exit 2;;
  esac
done
case "$theme" in Madadrang) slug=madadrang;; MishaShop) slug=mishashop;; *) echo 'Use a per-shop entry script.' >&2; exit 2;; esac
[[ $timeout =~ ^[1-9][0-9]*$ ]] || { echo 'Invalid timeout' >&2; exit 2; }
if [[ -z $image && $build == false ]]; then
  if [[ $interactive == true ]]; then read -r -p 'Image tag already loaded on this host: ' image; fi
  [[ -n $image ]] || { echo 'Supply --image TAG or --build.' >&2; exit 2; }
fi
command -v docker >/dev/null
command -v flock >/dev/null
command -v python3 >/dev/null
cd -- "$project"
[[ -f docker-compose.yml && -s .env ]] || { echo 'Compose project and private .env are required; see docs/smsir-selfhost.md.' >&2; exit 2; }
if [[ ${STOREFRONT_DEPLOY_LOCK_HELD:-} != 1 ]]; then
  exec 9> .storefront-deploy.lock
  flock -n 9 || { echo 'Another deployment holds the project lock.' >&2; exit 1; }
fi
configured_theme=$(docker compose config --format json | python3 -c 'import sys,json; print(json.load(sys.stdin)["services"]["nopcommerce_web"]["environment"]["StorefrontThemeConfig__Theme"])')
[[ $configured_theme == "$theme" || ( $theme == MishaShop && $configured_theme == Boutique ) ]] || { echo 'The .env belongs to a different shop; refusing deployment.' >&2; exit 2; }
if [[ -n $archive ]]; then [[ -f $archive ]] || { echo 'Image archive missing.' >&2; exit 2; }; docker image load --input "$archive"; fi
if [[ $build == true ]]; then
  docker compose build nopcommerce_web
  image=$(docker compose config --format json | python3 -c 'import sys,json; print(json.load(sys.stdin)["services"]["nopcommerce_web"]["image"])')
fi
docker image inspect "$image" >/dev/null
image_theme=$(docker image inspect --format '{{index .Config.Labels "com.nop.storefront.theme"}}' "$image")
[[ -z $image_theme || $image_theme == "$theme" ]] || { echo 'Image belongs to a different shop.' >&2; exit 2; }
mkdir -p .storefront-releases
chmod 700 .storefront-releases
stamp=$(date -u +%Y%m%dT%H%M%SZ)-$$
release=".storefront-releases/$stamp"
mkdir -m 700 "$release"
override="$PWD/.storefront-releases/current-image.yml"
candidate="$PWD/$release/candidate.yml"
# Explicit image names are restricted before embedding them into YAML.
[[ $image =~ ^[A-Za-z0-9][A-Za-z0-9._:/@-]*$ ]] || { echo 'Invalid image reference.' >&2; exit 2; }
printf 'services:\n  nopcommerce_web:\n    image: "%s"\n' "$image" > "$candidate"
base=(docker compose -f "$PWD/docker-compose.yml")
previous=$("${base[@]}" ps -aq nopcommerce_web)
previous_image=
if [[ -n $previous ]]; then previous_image=$(docker inspect --format '{{.Image}}' "$previous"); fi
if [[ -f $override ]]; then cp -- "$override" "$release/previous.yml"; fi
db=$("${base[@]}" ps -q nopcommerce_database)
if [[ -n $db ]]; then
  docker exec "$db" sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$release/database.dump"
  [[ -s $release/database.dump ]]
fi
success=false
recover() {
  result=$?
  if [[ $result -ne 0 && $success == false && -n $previous_image ]]; then
    printf 'services:\n  nopcommerce_web:\n    image: "%s"\n' "$previous_image" > "$release/rollback.yml"
    "${base[@]}" -f "$PWD/$release/rollback.yml" up -d --no-build --no-deps nopcommerce_web || true
    echo "Deployment failed; previous application image restored. Database backup: $release" >&2
  fi
}
trap recover EXIT
if ! docker image inspect nopcommerce:certificate-helper >/dev/null 2>&1; then "${base[@]}" build certificates; fi
database_image=$("${base[@]}" config --format json | python3 -c 'import sys,json; print(json.load(sys.stdin)["services"]["nopcommerce_database"]["image"])')
if ! docker image inspect "$database_image" >/dev/null 2>&1; then "${base[@]}" pull nopcommerce_database; fi
"${base[@]}" -f "$candidate" up -d --no-build --pull never --wait --wait-timeout "$timeout"
container=$("${base[@]}" -f "$candidate" ps -q nopcommerce_web)
if docker exec "$container" sh -c 'jq -e ".ConnectionStrings.ConnectionString | strings | length > 0" /app/App_Data/appsettings.json >/dev/null 2>&1'; then
  docker exec "$container" sh /app/check-storefront-plugins.sh
else
  echo 'Fresh database: finish the native web installer; both plugins are queued automatically.'
fi
cp -- "$candidate" "$override.tmp"
mv -- "$override.tmp" "$override"
success=true
echo "$theme deployment ready. Future runs preserve the named volumes and administrator SMS settings."
