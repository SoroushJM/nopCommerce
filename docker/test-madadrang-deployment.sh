#!/bin/sh
# All mutations below target a disposable copy, never POSTGRES_DB.
set -eu
if [ "$#" -gt 1 ]; then echo 'Usage: test-madadrang-deployment.sh [database.dump]' >&2; exit 2; fi
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
db=$(docker compose ps -q nopcommerce_database)
test -n "$db"
scratch="nop_madadrang_deploy_test_$$"
created=false
work=$(mktemp -d)
cleanup() {
    if [ "$created" = true ]; then
        docker exec "$db" sh -c 'dropdb -U "$POSTGRES_USER" "$1"' sh "$scratch"
    fi
    docker exec "$db" rm -f "/tmp/$scratch.dump" "/tmp/$scratch-check.sql" "/tmp/$scratch-seed.sql" "/tmp/$scratch-presentation.sql" "/tmp/$scratch-resources.sql"
    rm -f "$work/madadrang-seed.sql" "$work/madadrang-presentation.sql"
    rmdir "$work"
}
trap cleanup EXIT
python3 "$script_dir/generate-madadrang-seed.py" "$script_dir/madadrang-seed.json" "$work/madadrang-seed.sql"
sed "s/madadrang-seed.sql/$scratch-seed.sql/; s/madadrang-public-resources.sql/$scratch-resources.sql/" "$script_dir/madadrang-presentation.sql" > "$work/madadrang-presentation.sql"
docker cp "$work/madadrang-seed.sql" "$db:/tmp/$scratch-seed.sql"
docker cp "$work/madadrang-presentation.sql" "$db:/tmp/$scratch-presentation.sql"
docker cp "$script_dir/madadrang-public-resources.sql" "$db:/tmp/$scratch-resources.sql"
if [ "$#" -eq 1 ]; then
    docker cp "$1" "$db:/tmp/$scratch.dump"
else
    docker exec "$db" sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc -f "/tmp/$1.dump"' sh "$scratch"
fi
docker exec "$db" sh -c 'createdb -U "$POSTGRES_USER" "$1"' sh "$scratch"
created=true
docker exec "$db" sh -c 'pg_restore --exit-on-error -U "$POSTGRES_USER" -d "$1" "/tmp/$1.dump"' sh "$scratch"
query() { docker exec -i "$db" sh -c 'psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$1"' sh "$scratch"; }
migrate() { docker exec "$db" sh -c 'psql -X -q -v ON_ERROR_STOP=1 -v store_id=1 -v seed_data="$2" -U "$POSTGRES_USER" -d "$1" -f "/tmp/$1-presentation.sql"' sh "$scratch" "$1"; }
checksum() {
    docker exec "$db" sh -c 'pg_dump -U "$POSTGRES_USER" -d "$1" --data-only -f "/tmp/$1-check.sql"' sh "$scratch" || return $?
    docker exec "$db" cat "/tmp/$scratch-check.sql" | sed '/^\\restrict /d; /^\\unrestrict /d' | sha256sum
}
# Reset only the scratch catalogue to exercise actual insertion after production was seeded.
query <<'SQL'
TRUNCATE "Product","Category","Manufacturer","Picture","ProductAttribute","Menu" RESTART IDENTITY CASCADE;
DELETE FROM "UrlRecord" WHERE "EntityName" IN ('Product','Category','Manufacturer');
DELETE FROM "GenericAttribute" WHERE "Key" LIKE 'Madadrang.%';
UPDATE "Setting" SET "Value"='DefaultClean' WHERE "Name"='storeinformationsettings.defaultstoretheme';
UPDATE "Store" SET "DefaultLanguageId"=0;
UPDATE "Language" SET "DisplayOrder"=CASE WHEN "LanguageCulture"='en-US' THEN 0 ELSE 1 END,"DefaultCurrencyId"=0;
DO $$ BEGIN
 IF NOT EXISTS (SELECT 1 FROM "Language" WHERE "LanguageCulture"='fa-IR' AND "Published" AND "Rtl") THEN
  RAISE EXCEPTION 'Test baseline must have a published RTL Persian language';
 END IF;
END $$;
SQL
migrate false
test "$(echo 'SELECT count(*) FROM "Product";' | query)" = 0
test "$(echo 'SELECT count(*) FROM "Category";' | query)" = 0
echo 'PASS: normal deployment creates no sample products/categories'
migrate true
query <<'SQL'
DO $$ BEGIN
 IF (SELECT count(*) FROM "Product")<>8 THEN RAISE EXCEPTION 'Expected eight products'; END IF;
 IF (SELECT count(*) FROM "Category")<>4 THEN RAISE EXCEPTION 'Expected four categories'; END IF;
 IF (SELECT count(*) FROM "ProductAttributeCombination")<>32 THEN RAISE EXCEPTION 'Expected 32 variants'; END IF;
 IF (SELECT count(*) FROM "Product_Picture_Mapping")<>8 OR (SELECT count(*) FROM "PictureBinary" WHERE octet_length("BinaryData")>0)<>8 THEN RAISE EXCEPTION 'Expected eight pictures'; END IF;
 IF (SELECT count(*) FROM "Menu" WHERE "CssClass" LIKE 'madadrang-%' AND "Published")<>3 THEN RAISE EXCEPTION 'Missing menus'; END IF;
 IF (SELECT count(*) FROM "MenuItem" WHERE "MenuItemTypeId"=1)<>8 THEN RAISE EXCEPTION 'Missing category menu links'; END IF;
 IF (SELECT count(*) FROM "UrlRecord" WHERE "EntityName"='Product' AND "IsActive")<>8 THEN RAISE EXCEPTION 'Missing product URLs'; END IF;
 IF (SELECT "LanguageCulture" FROM "Language" WHERE "Published" ORDER BY "DisplayOrder","Id" LIMIT 1)<>'fa-IR' THEN RAISE EXCEPTION 'Wrong request culture'; END IF;
 IF NOT EXISTS (SELECT 1 FROM "Language" l JOIN "Currency" c ON c."Id"=l."DefaultCurrencyId" WHERE l."LanguageCulture"='fa-IR' AND c."CurrencyCode"='IRT') THEN RAISE EXCEPTION 'Wrong display currency'; END IF;
END $$;
SQL
echo 'PASS: seed after normal deploy creates products, variants, pictures, menus and toman settings'
before=$(checksum)
migrate true
test "$before" = "$(checksum)"
echo 'PASS: repeated seed leaves the database identical'
query <<'SQL'
UPDATE "Product" SET "Price"=123456,"StockQuantity"=7,"ShowOnHomepage"=false WHERE "Sku"='MRG-1';
UPDATE "MenuItem" SET "Title"='Merchant edit' WHERE "CssClass"='sf-menu-all';
UPDATE "LocaleStringResource" SET "ResourceValue"='Merchant translation' WHERE "ResourceName"='Search' AND "LanguageId"=(SELECT "Id" FROM "Language" WHERE "LanguageCulture"='fa-IR');
SQL
before=$(checksum)
migrate true
test "$before" = "$(checksum)"
migrate false
test "$before" = "$(checksum)"
echo 'PASS: seed and normal redeploy preserve merchant prices, inventory, menus and translations'
query <<'SQL'
UPDATE "Setting" SET "Value"='Boutique' WHERE "Name"='storeinformationsettings.defaultstoretheme';
SQL
before=$(checksum)
if migrate true; then echo 'FAIL: another storefront was accepted' >&2; exit 1; fi
test "$before" = "$(checksum)"
echo 'PASS: another storefront is rejected without database changes'
echo 'ALL DATABASE REGRESSION TESTS PASSED; disposable database will be removed'
