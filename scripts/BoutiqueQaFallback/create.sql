\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
DO $$ BEGIN
 IF current_database() <> 'nop_themeqa_boutique_20261012185304' THEN
  RAISE EXCEPTION 'Refusing any database other than exact isolated Boutique QA';
 END IF;
 IF NOT EXISTS(SELECT 1 FROM "Product" WHERE "Id"=2 AND "Sku"='MRG-2' AND "IsGiftCard")
 OR NOT EXISTS(SELECT 1 FROM "Product" WHERE "Id"=3 AND "Sku"='MRG-3' AND "IsRental") THEN
  RAISE EXCEPTION 'Unexpected native gift/rental baseline';
 END IF;
END $$;
SELECT pg_advisory_xact_lock(625020261013);

DO $fixture$
DECLARE
 source_id integer; product_id integer; sku text; slug text; label text;
 values_json jsonb; columns_list text;
 stamp constant text := 'Boutique QA fallback fixture v1';
BEGIN
 FOR source_id IN 2..3 LOOP
  sku:=CASE WHEN source_id=2 THEN 'BTQ-QA-FALLBACK-GIFT' ELSE 'BTQ-QA-FALLBACK-RENTAL' END;
  slug:=CASE WHEN source_id=2 THEN 'boutique-qa-gift-card' ELSE 'boutique-qa-rental' END;
  label:=CASE WHEN source_id=2 THEN 'کارت هدیهٔ آزمون بوتیک' ELSE 'اجارهٔ پوشاک — نمونهٔ آزمون' END;
  SELECT "Id" INTO product_id FROM "Product" WHERE "Sku"=sku;
  IF product_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "Product" WHERE "Id"=product_id AND "AdminComment"=stamp) THEN
   RAISE EXCEPTION 'Foreign SKU collision: %',sku;
  END IF;
  IF EXISTS(SELECT 1 FROM "UrlRecord" WHERE "Slug"=slug AND ("EntityName"<>'Product' OR "EntityId" IS DISTINCT FROM product_id)) THEN
   RAISE EXCEPTION 'Foreign slug collision: %',slug;
  END IF;
  IF product_id IS NULL THEN
   SELECT to_jsonb(p) INTO values_json FROM "Product" p WHERE "Id"=source_id;
   values_json:=values_json||jsonb_build_object(
    'Name',label,'Sku',sku,'AdminComment',stamp,'Published',true,'Deleted',false,
    'VisibleIndividually',false,'ShowOnHomepage',false,'ParentGroupedProductId',0,
    'ShortDescription','محصول پنهان آزمایشی برای بررسی فرم‌های بومی بوتیک؛ در فهرست فروشگاه نمایش داده نمی‌شود.',
    'FullDescription','<p>محصول پنهان آزمایشی برای بررسی فرم‌های بومی کارت هدیه یا اجارهٔ پوشاک؛ کالای واقعی برای فروش نیست.</p>',
    'MetaTitle',label,'MetaDescription',NULL,'MetaKeywords',NULL,
    'Price',CASE WHEN source_id=2 THEN 500000 ELSE 100000 END,'OldPrice',0,
    'ManageInventoryMethodId',1,'StockQuantity',8,'AllowAddingOnlyExistingAttributeCombinations',false,
    'DisableBuyButton',false,'DisableWishlistButton',false,'OrderMinimumQuantity',1,'OrderMaximumQuantity',5,
    'SubjectToAcl',false,'LimitedToStores',false,'VendorId',0,'IsShipEnabled',source_id=3,
    'IsRecurring',false,'IsDownload',false,'CustomerEntersPrice',false,'RequireOtherProducts',false,
    'AvailableStartDateTimeUtc',NULL,'AvailableEndDateTimeUtc',NULL,
    'CreatedOnUtc',timezone('UTC',now()),'UpdatedOnUtc',timezone('UTC',now()));
   SELECT string_agg(format('%I',column_name),',' ORDER BY ordinal_position) INTO columns_list
   FROM information_schema.columns WHERE table_schema='public' AND table_name='Product' AND column_name<>'Id';
   EXECUTE format('INSERT INTO "Product" (%s) SELECT %s FROM jsonb_populate_record(NULL::"Product",$1) RETURNING "Id"',columns_list,columns_list)
    INTO product_id USING values_json;
   INSERT INTO "UrlRecord" ("EntityName","Slug","EntityId","IsActive","LanguageId") VALUES ('Product',slug,product_id,true,0);
  ELSIF EXISTS(SELECT 1 FROM "Product" WHERE "Id"=product_id AND ("Deleted" OR NOT "Published")) THEN
   RAISE EXCEPTION 'Fixture was cleaned up; do not silently reactivate it';
  END IF;
 END LOOP;
END $fixture$;
COMMIT;
\pset tuples_only on
\pset format unaligned
SELECT jsonb_build_object('database',current_database(),'ownerStamp','Boutique QA fallback fixture v1',
 'baseSignatures',(SELECT jsonb_agg(jsonb_build_object('id',"Id",'sku',"Sku",'md5',md5(to_jsonb(p)::text)) ORDER BY "Id") FROM "Product" p WHERE "Id" IN (2,3)),
 'fixtures',(SELECT jsonb_agg(jsonb_build_object('id',p."Id",'sku',p."Sku",'name',p."Name",'slug',u."Slug",'published',p."Published",'visibleIndividually',p."VisibleIndividually",'isGiftCard',p."IsGiftCard",'isRental',p."IsRental",'stock',p."StockQuantity") ORDER BY p."Id") FROM "Product" p JOIN "UrlRecord" u ON u."EntityName"='Product' AND u."EntityId"=p."Id" AND u."IsActive" WHERE p."AdminComment"='Boutique QA fallback fixture v1' AND p."Sku" IN ('BTQ-QA-FALLBACK-GIFT','BTQ-QA-FALLBACK-RENTAL')));
