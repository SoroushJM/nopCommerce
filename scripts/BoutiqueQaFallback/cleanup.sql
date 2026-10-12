\set ON_ERROR_STOP on
BEGIN;
DO $$ BEGIN
 IF current_database() <> 'nop_themeqa_boutique_20261012185304' THEN
  RAISE EXCEPTION 'Refusing any database other than exact isolated Boutique QA';
 END IF;
 IF EXISTS(SELECT 1 FROM "Product" WHERE "Sku" IN ('BTQ-QA-FALLBACK-GIFT','BTQ-QA-FALLBACK-RENTAL') AND "AdminComment" IS DISTINCT FROM 'Boutique QA fallback fixture v1') THEN
  RAISE EXCEPTION 'Refusing foreign fixture SKU ownership';
 END IF;
END $$;
SELECT pg_advisory_xact_lock(625020261013);
-- Native soft deletion preserves any references from later QA cart/order/history tests.
UPDATE "UrlRecord" SET "IsActive"=false WHERE "EntityName"='Product' AND "EntityId" IN
 (SELECT "Id" FROM "Product" WHERE "AdminComment"='Boutique QA fallback fixture v1' AND "Sku" IN ('BTQ-QA-FALLBACK-GIFT','BTQ-QA-FALLBACK-RENTAL'));
UPDATE "Product" SET "Published"=false,"Deleted"=true,"ShowOnHomepage"=false,"VisibleIndividually"=false,"UpdatedOnUtc"=timezone('UTC',now())
 WHERE "AdminComment"='Boutique QA fallback fixture v1' AND "Sku" IN ('BTQ-QA-FALLBACK-GIFT','BTQ-QA-FALLBACK-RENTAL');
COMMIT;
