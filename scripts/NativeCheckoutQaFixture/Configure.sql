DO $$ BEGIN
 IF (SELECT "Phase" FROM "NativeCheckoutQaFixture" WHERE "Key"='native-checkout-v1') NOT IN ('prepared','configured') THEN RAISE EXCEPTION 'Fixture phase mismatch'; END IF;
 IF (SELECT "Phase"='configured' AND "Applied" IS DISTINCT FROM pg_temp.qa_capture() FROM "NativeCheckoutQaFixture" WHERE "Key"='native-checkout-v1') THEN RAISE EXCEPTION 'Fixture-owned data changed since configure'; END IF;
 IF to_regclass('"ShippingByWeightByTotalRecord"') IS NULL THEN RAISE EXCEPTION 'Native shipping installation/migration is incomplete'; END IF;
 IF NOT EXISTS(SELECT 1 FROM "Currency" c JOIN "Setting" s ON s."Value"=c."Id"::text WHERE lower(s."Name")='currencysettings.primarystorecurrencyid' AND s."StoreId"=0 AND c."CurrencyCode"='IRT' AND c."Rate"=1) THEN RAISE EXCEPTION 'Fixture shipping amounts require primary toman units'; END IF;
 IF (SELECT count(*) FROM "ShippingMethod" WHERE "Id" IN (1,2,3)) <> 3 THEN RAISE EXCEPTION 'Expected native shipping methods missing'; END IF;
 IF EXISTS(SELECT 1 FROM "MessageTemplate" WHERE "IsActive") OR EXISTS(SELECT 1 FROM "ScheduleTask" WHERE "Type"='Nop.Services.Messages.QueuedMessagesSendTask, Nop.Services' AND "Enabled") THEN RAISE EXCEPTION 'Email guard changed'; END IF;
END $$;
CREATE TEMP TABLE qa_values(name text PRIMARY KEY,value text);
INSERT INTO qa_values VALUES
 ('ordersettings.anonymouscheckoutallowed','True'),
 ('ordersettings.checkoutdisabled','False'),
 ('ordersettings.termsofserviceonshoppingcartpage','False'),
 ('ordersettings.termsofserviceonorderconfirmpage','False'),
 ('paymentsettings.bypasspaymentmethodselectionifonlyone','False'),
 ('shippingsettings.bypassshippingmethodselectionifonlyone','False'),
 ('paymentsettings.activepaymentmethodsystemnames','Payments.CheckMoneyOrder'),
 ('shippingsettings.activeshippingratecomputationmethodsystemnames','Shipping.FixedByWeightByTotal'),
 ('fixedbyweightbytotalsettings.shippingbyweightbytotalenabled','False'),
 ('fixedbyweightbytotalsettings.limitmethodstocreated','False'),
 ('fixedbyweightbytotalsettings.loadallrecord','True'),
 ('checkmoneyorderpaymentsettings.descriptiontext','<p>پرداخت آفلاین صرفاً آزمایشی QA؛ هیچ پرداخت، انتقال وجه یا ارسال واقعی انجام نمی‌شود. Offline QA test only; do not send money.</p>'),
 ('checkmoneyorderpaymentsettings.additionalfee','0'),
 ('checkmoneyorderpaymentsettings.additionalfeepercentage','False'),
 ('checkmoneyorderpaymentsettings.shippableproductrequired','False'),
 ('shippingratecomputationmethod.fixedbyweightbytotal.rate.shippingmethodid1','65000'),
 ('shippingratecomputationmethod.fixedbyweightbytotal.rate.shippingmethodid2','120000'),
 ('shippingratecomputationmethod.fixedbyweightbytotal.rate.shippingmethodid3','85000'),
 ('shippingratecomputationmethod.fixedbyweightbytotal.transitdays.shippingmethodid1','3'),
 ('shippingratecomputationmethod.fixedbyweightbytotal.transitdays.shippingmethodid2','1'),
 ('shippingratecomputationmethod.fixedbyweightbytotal.transitdays.shippingmethodid3','2');
-- All existing QA store overrides are included in the before snapshot and restored later.
UPDATE "Setting" s SET "Value"=v.value FROM qa_values v WHERE lower(s."Name")=v.name;
INSERT INTO "Setting"("Name","Value","StoreId") SELECT v.name,v.value,0 FROM qa_values v WHERE NOT EXISTS(SELECT 1 FROM "Setting" s WHERE lower(s."Name")=v.name AND s."StoreId"=0);
UPDATE "NativeCheckoutQaFixture" SET "Phase"='configured',"Applied"=pg_temp.qa_capture() WHERE "Key"='native-checkout-v1';
SELECT current_database() AS qa_database,'configured' AS phase,(SELECT count(*) FROM "MessageTemplate" WHERE "IsActive") AS active_templates,(SELECT count(*) FROM "QueuedEmail" WHERE "SentOnUtc" IS NULL) AS preserved_pending_emails;
SELECT "Name","Value","StoreId" FROM "Setting" WHERE lower("Name") IN ('ordersettings.anonymouscheckoutallowed','ordersettings.termsofserviceonshoppingcartpage','paymentsettings.activepaymentmethodsystemnames','shippingsettings.activeshippingratecomputationmethodsystemnames') OR lower("Name") LIKE 'shippingratecomputationmethod.fixedbyweightbytotal.rate.%' ORDER BY "Name","StoreId";
COMMIT;
