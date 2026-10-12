\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout='5s';
DO $$ BEGIN
 IF current_database()<>'nop_boutique' OR NOT EXISTS(SELECT 1 FROM "Store" WHERE "Id"=1 AND "Url"='http://80.75.5.82:9012/') THEN RAISE EXCEPTION 'Wrong Boutique test deployment'; END IF;
 IF (SELECT count(*) FROM "Product" WHERE "Sku" LIKE 'BTQ-QA-%' AND NOT "Deleted")<>12 THEN RAISE EXCEPTION 'Owned sample catalog mismatch'; END IF;
 IF EXISTS(SELECT 1 FROM "Setting" WHERE "Name" IN ('catalogsettings.searchpagepricefrom','catalogsettings.productsbytagpricefrom') AND "StoreId"=0 AND "Value"::numeric<>0) OR EXISTS(SELECT 1 FROM "Setting" WHERE "Name" IN ('catalogsettings.searchpagepriceto','catalogsettings.productsbytagpriceto') AND "StoreId"=0 AND "Value"::numeric<>10000) THEN RAISE EXCEPTION 'Custom merchant range; refuse to overwrite'; END IF;
END $$;
UPDATE "Setting" SET "Value"='False' WHERE "StoreId"=0 AND "Name" IN ('catalogsettings.searchpagemanuallypricerange','catalogsettings.productsbytagmanuallypricerange');
SELECT "Name","Value" FROM "Setting" WHERE "Name" IN ('catalogsettings.searchpagemanuallypricerange','catalogsettings.productsbytagmanuallypricerange','catalogsettings.searchpagepricerangefiltering','catalogsettings.productsbytagpricerangefiltering','catalogsettings.enablepricerangefiltering');
COMMIT;
