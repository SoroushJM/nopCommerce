BEGIN READ ONLY;
DO $$ BEGIN IF current_database() <> 'nop_themeqa_boutique_20261012185304' THEN RAISE EXCEPTION 'Wrong QA'; END IF; END $$;
SELECT table_name,column_name,data_type FROM information_schema.columns
 WHERE table_name IN ('VersionInfo','ShippingByWeightByTotalRecord','MessageTemplate','Setting') ORDER BY table_name,ordinal_position;
SELECT "Name", "Value", "StoreId" FROM "Setting" WHERE lower("Name") LIKE 'shippingratecomputationmethod.fixedbyweightbytotal.%';
SELECT count(*) FROM "LocaleStringResource" WHERE lower("ResourceName") LIKE 'plugins.payment.checkmoneyorder.%' OR lower("ResourceName") LIKE 'plugins.shipping.fixedbyweightbytotal.%';
COMMIT;
