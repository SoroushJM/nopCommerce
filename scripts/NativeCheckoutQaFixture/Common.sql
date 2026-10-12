BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='60s';
DO $$ BEGIN
 IF current_database() NOT IN ('nop_themeqa_boutique_20261012185304','nop_themeqa_madadrang_20261012185304') THEN RAISE EXCEPTION 'Wrong isolated QA database'; END IF;
 IF to_regclass('"NativeCheckoutQaFixture"') IS NOT NULL
 AND obj_description(to_regclass('"NativeCheckoutQaFixture"'),'pg_class') IS DISTINCT FROM 'Codex isolated native checkout fixture v1' THEN RAISE EXCEPTION 'Snapshot ownership mismatch'; END IF;
END $$;
SELECT pg_advisory_xact_lock(6512502026);
CREATE TABLE IF NOT EXISTS "NativeCheckoutQaFixture"("Key" text PRIMARY KEY,"Phase" text NOT NULL,"Original" jsonb NOT NULL,"Applied" jsonb);
COMMENT ON TABLE "NativeCheckoutQaFixture" IS 'Codex isolated native checkout fixture v1';
CREATE OR REPLACE FUNCTION pg_temp.qa_owned_setting(n text) RETURNS boolean LANGUAGE sql IMMUTABLE AS $$
 SELECT lower(n) IN ('install.plugin.sample.data','ordersettings.anonymouscheckoutallowed','ordersettings.termsofserviceonshoppingcartpage','ordersettings.termsofserviceonorderconfirmpage','paymentsettings.bypasspaymentmethodselectionifonlyone','shippingsettings.bypassshippingmethodselectionifonlyone','paymentsettings.activepaymentmethodsystemnames','shippingsettings.activeshippingratecomputationmethodsystemnames','ordersettings.checkoutdisabled')
 OR lower(n) LIKE 'fixedbyweightbytotalsettings.%' OR lower(n) LIKE 'checkmoneyorderpaymentsettings.%'
 OR lower(n) LIKE 'shippingratecomputationmethod.fixedbyweightbytotal.%';
$$;
CREATE OR REPLACE FUNCTION pg_temp.qa_capture() RETURNS jsonb LANGUAGE plpgsql AS $$
DECLARE result jsonb; shipping_rows jsonb='[]';
BEGIN
 IF to_regclass('"ShippingByWeightByTotalRecord"') IS NOT NULL THEN
  EXECUTE 'SELECT coalesce(jsonb_agg(to_jsonb(s) ORDER BY "Id"),''[]''::jsonb) FROM "ShippingByWeightByTotalRecord" s' INTO shipping_rows;
 END IF;
 SELECT jsonb_build_object(
  'settings',(SELECT coalesce(jsonb_agg(to_jsonb(s) ORDER BY "Id"),'[]'::jsonb) FROM "Setting" s WHERE pg_temp.qa_owned_setting("Name")),
  'templates',(SELECT coalesce(jsonb_agg(jsonb_build_object('Id',"Id",'IsActive',"IsActive") ORDER BY "Id"),'[]'::jsonb) FROM "MessageTemplate"),
  'emailTasks',(SELECT coalesce(jsonb_agg(jsonb_build_object('Id',"Id",'Enabled',"Enabled") ORDER BY "Id"),'[]'::jsonb) FROM "ScheduleTask" WHERE "Type"='Nop.Services.Messages.QueuedMessagesSendTask, Nop.Services'),
  'locales',(SELECT coalesce(jsonb_agg(to_jsonb(l) ORDER BY "Id"),'[]'::jsonb) FROM "LocaleStringResource" l WHERE lower("ResourceName") LIKE 'plugins.payment.checkmoneyorder.%' OR lower("ResourceName") LIKE 'plugins.shipping.fixedbyweightbytotal.%'),
  'migrations',(SELECT coalesce(jsonb_agg(to_jsonb(m) ORDER BY "Version"),'[]'::jsonb) FROM "MigrationVersionInfo" m WHERE "Description" LIKE 'Shipping.FixedByWeightByTotal%'),
  'shippingTable',to_regclass('"ShippingByWeightByTotalRecord"') IS NOT NULL,'shippingRows',shipping_rows
 ) INTO result;
 RETURN result;
END $$;

