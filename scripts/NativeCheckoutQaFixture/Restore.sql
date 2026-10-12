DO $$ DECLARE original jsonb; applied jsonb; phase text;
BEGIN
 SELECT "Original","Applied","Phase" INTO original,applied,phase FROM "NativeCheckoutQaFixture" WHERE "Key"='native-checkout-v1';
 IF phase <> 'configured' THEN RAISE EXCEPTION 'Only a completed configured fixture can be restored'; END IF;
 IF pg_temp.qa_capture() IS DISTINCT FROM applied THEN RAISE EXCEPTION 'Fixture-owned data changed; review instead of overwriting'; END IF;
 IF NOT (original->>'shippingTable')::boolean AND applied->'shippingRows' <> '[]'::jsonb THEN RAISE EXCEPTION 'Native shipping table contains additional data; preserve it'; END IF;
 -- No order, cart, customer, address or queued email rows are deleted or edited.
 DELETE FROM "Setting" s WHERE pg_temp.qa_owned_setting(s."Name") AND NOT EXISTS(SELECT 1 FROM jsonb_array_elements(original->'settings') r WHERE (r->>'Id')::int=s."Id");
 UPDATE "Setting" s SET "Name"=r."Name","Value"=r."Value","StoreId"=r."StoreId" FROM jsonb_populate_recordset(NULL::"Setting",original->'settings') r WHERE s."Id"=r."Id";
 INSERT INTO "Setting" OVERRIDING SYSTEM VALUE SELECT r.* FROM jsonb_populate_recordset(NULL::"Setting",original->'settings') r WHERE NOT EXISTS(SELECT 1 FROM "Setting" s WHERE s."Id"=r."Id");
 UPDATE "MessageTemplate" t SET "IsActive"=(r->>'IsActive')::boolean FROM jsonb_array_elements(original->'templates') r WHERE t."Id"=(r->>'Id')::int;
 UPDATE "ScheduleTask" t SET "Enabled"=(r->>'Enabled')::boolean FROM jsonb_array_elements(original->'emailTasks') r WHERE t."Id"=(r->>'Id')::int;
 DELETE FROM "LocaleStringResource" l WHERE (lower(l."ResourceName") LIKE 'plugins.payment.checkmoneyorder.%' OR lower(l."ResourceName") LIKE 'plugins.shipping.fixedbyweightbytotal.%') AND NOT EXISTS(SELECT 1 FROM jsonb_array_elements(original->'locales') r WHERE (r->>'Id')::int=l."Id");
 UPDATE "LocaleStringResource" l SET "LanguageId"=r."LanguageId","ResourceName"=r."ResourceName","ResourceValue"=r."ResourceValue" FROM jsonb_populate_recordset(NULL::"LocaleStringResource",original->'locales') r WHERE l."Id"=r."Id";
 INSERT INTO "LocaleStringResource" OVERRIDING SYSTEM VALUE SELECT r.* FROM jsonb_populate_recordset(NULL::"LocaleStringResource",original->'locales') r WHERE NOT EXISTS(SELECT 1 FROM "LocaleStringResource" l WHERE l."Id"=r."Id");
 IF NOT (original->>'shippingTable')::boolean THEN DROP TABLE "ShippingByWeightByTotalRecord"; END IF;
 DELETE FROM "MigrationVersionInfo" WHERE "Description" LIKE 'Shipping.FixedByWeightByTotal%';
 INSERT INTO "MigrationVersionInfo" SELECT r.* FROM jsonb_populate_recordset(NULL::"MigrationVersionInfo",original->'migrations') r;
 IF pg_temp.qa_capture() IS DISTINCT FROM original THEN RAISE EXCEPTION 'Restore did not reproduce fixture-owned snapshot'; END IF;
 UPDATE "NativeCheckoutQaFixture" SET "Phase"='restored',"Applied"=original WHERE "Key"='native-checkout-v1';
END $$;
SELECT current_database() AS qa_database,'restored' AS phase;
COMMIT;

