\set ON_ERROR_STOP on
-- Temporary isolated-QA acceptance window. Native ProductController redirects
-- VisibleIndividually=false products without a grouped parent to Home.
-- These two fixtures can appear in native all-products/search until cleanup.sql.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SELECT pg_advisory_xact_lock(625020261013);
DO $$ BEGIN
 IF current_database() <> 'nop_themeqa_boutique_20261012185304' THEN
  RAISE EXCEPTION 'Refusing any database other than exact isolated Boutique QA';
 END IF;
 IF (SELECT count(*) FROM "Product" WHERE "AdminComment"='Boutique QA fallback fixture v1'
     AND "Sku" IN ('BTQ-QA-FALLBACK-GIFT','BTQ-QA-FALLBACK-RENTAL')
     AND "Published" AND NOT "Deleted" AND "ParentGroupedProductId"=0) <> 2 THEN
  RAISE EXCEPTION 'Expected exactly two owned, uncleaned fixtures';
 END IF;
END $$;
UPDATE "Product" SET "VisibleIndividually"=true,"UpdatedOnUtc"=timezone('UTC',now())
 WHERE "AdminComment"='Boutique QA fallback fixture v1'
 AND "Sku" IN ('BTQ-QA-FALLBACK-GIFT','BTQ-QA-FALLBACK-RENTAL');
COMMIT;
