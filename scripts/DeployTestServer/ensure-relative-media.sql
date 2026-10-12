\set ON_ERROR_STOP on
BEGIN;
DO $$ BEGIN
 IF current_database() NOT IN ('nop_madadrang','nop_boutique') THEN RAISE EXCEPTION 'Wrong test database'; END IF;
 IF NOT EXISTS(SELECT 1 FROM "Store" WHERE "Id"=1 AND "Url"=CASE current_database() WHEN 'nop_boutique' THEN 'http://80.75.5.82:9012/' ELSE 'http://80.75.5.82:9011/' END) THEN RAISE EXCEPTION 'Wrong test store'; END IF;
END $$;
UPDATE "Setting" SET "Value"='False' WHERE lower("Name")='mediasettings.useabsoluteimagepath';
INSERT INTO "Setting"("Name","Value","StoreId") SELECT 'mediasettings.useabsoluteimagepath','False',0 WHERE NOT EXISTS(SELECT 1 FROM "Setting" WHERE lower("Name")='mediasettings.useabsoluteimagepath');
COMMIT;
