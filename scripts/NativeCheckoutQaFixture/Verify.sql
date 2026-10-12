BEGIN READ ONLY;
DO $$ BEGIN
 IF current_database() NOT IN ('nop_themeqa_boutique_20261012185304','nop_themeqa_madadrang_20261012185304') THEN RAISE EXCEPTION 'Wrong isolated QA database'; END IF;
 IF NOT EXISTS(SELECT 1 FROM "NativeCheckoutQaFixture" WHERE "Key"='native-checkout-v1' AND "Phase"='configured') THEN RAISE EXCEPTION 'Fixture is not configured'; END IF;
 IF EXISTS(SELECT 1 FROM "MessageTemplate" WHERE "IsActive") OR EXISTS(SELECT 1 FROM "ScheduleTask" WHERE "Type"='Nop.Services.Messages.QueuedMessagesSendTask, Nop.Services' AND "Enabled") THEN RAISE EXCEPTION 'Email safeguard failed'; END IF;
END $$;
SELECT current_database() AS qa_database,(SELECT count(*) FROM "MessageTemplate" WHERE "IsActive") AS active_email_templates,(SELECT count(*) FROM "ScheduleTask" WHERE "Type"='Nop.Services.Messages.QueuedMessagesSendTask, Nop.Services' AND "Enabled") AS enabled_email_tasks,(SELECT count(*) FROM "QueuedEmail" WHERE "SentOnUtc" IS NULL) AS pending_email_count;
SELECT "Name","Value","StoreId" FROM "Setting" WHERE lower("Name") IN ('ordersettings.anonymouscheckoutallowed','ordersettings.termsofserviceonshoppingcartpage','ordersettings.termsofserviceonorderconfirmpage','paymentsettings.activepaymentmethodsystemnames','shippingsettings.activeshippingratecomputationmethodsystemnames','paymentsettings.bypasspaymentmethodselectionifonlyone') OR lower("Name") LIKE 'shippingratecomputationmethod.fixedbyweightbytotal.rate.%' ORDER BY "Name","StoreId";
SELECT "Description" FROM "MigrationVersionInfo" WHERE "Description" LIKE 'Shipping.FixedByWeightByTotal%';
COMMIT;
