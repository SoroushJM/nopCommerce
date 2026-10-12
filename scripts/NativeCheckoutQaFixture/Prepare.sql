INSERT INTO "NativeCheckoutQaFixture"("Key","Phase","Original") VALUES('native-checkout-v1','prepared',pg_temp.qa_capture()) ON CONFLICT("Key") DO NOTHING;
DO $$ BEGIN
 IF (SELECT "Phase" FROM "NativeCheckoutQaFixture" WHERE "Key"='native-checkout-v1') <> 'prepared' THEN RAISE EXCEPTION 'Fixture is not in prepare phase'; END IF;
END $$;
UPDATE "Setting" SET "Value"='False' WHERE lower("Name")='install.plugin.sample.data';
UPDATE "MessageTemplate" SET "IsActive"=false WHERE "IsActive";
UPDATE "ScheduleTask" SET "Enabled"=false WHERE "Type"='Nop.Services.Messages.QueuedMessagesSendTask, Nop.Services';
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM "MessageTemplate" WHERE "IsActive") OR EXISTS(SELECT 1 FROM "ScheduleTask" WHERE "Type"='Nop.Services.Messages.QueuedMessagesSendTask, Nop.Services' AND "Enabled") THEN RAISE EXCEPTION 'Email safety guard failed'; END IF;
END $$;
UPDATE "NativeCheckoutQaFixture" SET "Applied"=pg_temp.qa_capture() WHERE "Key"='native-checkout-v1';
SELECT current_database() AS qa_database,'prepared' AS phase,(SELECT count(*) FROM "MessageTemplate" WHERE "IsActive") AS active_templates;
COMMIT;

