#!/bin/sh
# Run from the installed Compose project. Explicitly for the separate stationery store.
set -eu
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
usage() {
    cat <<'HELP'
Usage: sh docker/deploy-madadrang.sh [--seed-data] [--image IMAGE] [--store-id ID]
  --seed-data     Add the eight sample stationery products, images and variants.
                 Idempotent; does not reset or overwrite existing products.
  --image IMAGE  Use an image already loaded on this host (no build or pull).
  --store-id ID  Installed single-store ID (default: STORE_ID or 1).
  --help         Show this help without accessing Docker or the database.
Without --seed-data no sample catalogue is created or repopulated.
Run from the installed Compose project directory, after nopCommerce installation.
HELP
}
store_id=${STORE_ID:-1}
seed_data=false
image=
while [ "$#" -gt 0 ]; do
    case "$1" in
        --help|-h) usage; exit 0;;
        --seed-data) seed_data=true; shift;;
        --image|--store-id)
            test "$#" -ge 2 || { usage >&2; exit 2; }
            case "$2" in ''|--*) usage >&2; exit 2;; esac
            if [ "$1" = --image ]; then image=$2; else store_id=$2; fi
            shift 2;;
        *) echo "Unknown argument: $1" >&2; usage >&2; exit 2;;
    esac
done
case "$store_id" in ''|0|*[!0-9]*) echo 'Store ID must be a positive integer' >&2; exit 2;; esac
command -v python3 >/dev/null
command -v flock >/dev/null
test -f "$script_dir/madadrang-presentation.sql"
test -f "$script_dir/madadrang-public-resources.sql"
test -f "$script_dir/verify-madadrang.py"
if [ "$seed_data" = true ]; then
    test -f "$script_dir/generate-madadrang-seed.py"
    test -f "$script_dir/madadrang-seed.json"
fi
if [ -n "$image" ]; then docker image inspect "$image" >/dev/null; fi
service_image=$(docker compose config --format json | python3 -c 'import json,sys; print(json.load(sys.stdin)["services"]["nopcommerce_web"]["image"])')
db=$(docker compose ps -q nopcommerce_database)
web=$(docker compose ps -aq nopcommerce_web)
test -n "$db" && test -n "$web"
# Reject a second concurrent deployment of the same checkout.
exec 9> .madadrang-deploy.lock
flock -n 9 || { echo 'Another deployment is running' >&2; exit 1; }
stamp=$(date -u +%Y%m%dT%H%M%SZ)-$$
backup="backups/presentation-$stamp"
mkdir -p "$backup"
chmod 700 "$backup"
old_image=$(docker inspect --format '{{.Image}}' "$web")
candidate=${image:-$old_image}
docker run --rm --network none --memory 16m --entrypoint /bin/sh "$candidate" -c \
    'test -f /app/Themes/Madadrang/theme.json && test -f /app/Plugins/Misc.PersianStorefront/plugin.json'
docker cp "$web:/app/App_Data/plugins.json" "$backup/plugins.json"
python3 - "$backup/plugins.json" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8-sig") as source:
    plugins = json.load(source)
names = set(plugins.get("InstalledPluginNames", []))
names.update(p.get("SystemName") for p in plugins.get("InstalledPlugins", []) if isinstance(p, dict))
if "Misc.PersianStorefront" not in names:
    raise SystemExit("Install Misc.PersianStorefront in nopCommerce administration before deployment; --seed-data does not install plugins.")
PY
docker tag "$old_image" "nopcommerce:rollback-$stamp"
docker ps --format '{{.Names}} {{.Image}}' > "$backup/services.before"
printf 'seed_data=%s\nstore_id=%s\nprevious_image=%s\nrequested_image=%s\n' "$seed_data" "$store_id" "$old_image" "$image" > "$backup/deployment.txt"
cp docker-compose*.yml "$backup/"
if [ "$seed_data" = true ]; then
    python3 "$script_dir/generate-madadrang-seed.py" "$script_dir/madadrang-seed.json" "$backup/madadrang-seed.sql"
    docker cp "$backup/madadrang-seed.sql" "$db:/tmp/madadrang-seed.sql"
fi
docker cp "$script_dir/madadrang-presentation.sql" "$db:/tmp/madadrang-presentation.sql"
docker cp "$script_dir/madadrang-public-resources.sql" "$db:/tmp/madadrang-public-resources.sql"
echo "Backup: $backup"
docker compose stop nopcommerce_web
# Resume the previous image if the atomic SQL migration fails before it commits.
committed=false
recover() {
    status=$?
    if [ "$status" -ne 0 ]; then
        if [ "$committed" = false ]; then
            docker compose start nopcommerce_web || true
        fi
        echo "Deployment failed. Backup: $backup; previous image: nopcommerce:rollback-$stamp" >&2
        echo 'Do not report success until the rendered storefront verifier passes.' >&2
    fi
}
trap recover EXIT
docker exec "$db" sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$backup/database.dump"
test -s "$backup/database.dump"
docker cp "$web:/app/App_Data" "$backup/App_Data"
docker exec "$db" sh -c 'psql -X -v ON_ERROR_STOP=1 -v store_id="$1" -v seed_data="$2" -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/madadrang-presentation.sql' sh "$store_id" "$seed_data"
committed=true
docker tag "$candidate" "$service_image"
docker compose up -d --no-build --no-deps --pull never --wait --wait-timeout 240 nopcommerce_web
# URL comes from the store; verifies the same host/port customers use.
base=$(docker exec "$db" sh -c 'psql -X -At -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT \"Url\" FROM \"Store\" WHERE \"Id\"=$1"' sh "$store_id")
if [ "$seed_data" = true ]; then
    python3 "$script_dir/verify-madadrang.py" "$base" --expect-seed-data
else
    python3 "$script_dir/verify-madadrang.py" "$base"
fi
docker ps --format '{{.Names}} {{.Image}}' > "$backup/services.after"
echo "Deployment verified. Backup: $backup"
