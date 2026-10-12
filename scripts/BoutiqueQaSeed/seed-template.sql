\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
DO $$ BEGIN
 IF current_database() <> 'nop_themeqa_boutique_20261012185304' THEN
  RAISE EXCEPTION 'Refusing any database other than isolated Boutique QA';
 END IF;
 IF NOT EXISTS(SELECT 1 FROM "Product" WHERE "Id"=1 AND "Sku"='MRG-1') THEN
  RAISE EXCEPTION 'Unexpected QA baseline: sample product 1 missing';
 END IF;
 IF NOT EXISTS(SELECT 1 FROM "Currency" WHERE "CurrencyCode"='IRT' AND "Rate"=1) THEN
  RAISE EXCEPTION 'Expected primary toman currency is missing; do not relabel prices';
 END IF;
 IF NOT EXISTS(SELECT 1 FROM "Setting" WHERE lower("Name")='currencysettings.primarystorecurrencyid' AND "Value"=(SELECT "Id"::text FROM "Currency" WHERE "CurrencyCode"='IRT')) THEN
  RAISE EXCEPTION 'The existing QA database must already use toman as its primary unit';
 END IF;
 IF EXISTS(SELECT 1 FROM "Setting" WHERE lower("Name")='media.images.storeindb' AND lower("Value")<>'true') THEN
  RAISE EXCEPTION 'This seed requires the existing database image mode; it will not change image storage policy';
 END IF;
END $$;
SELECT pg_advisory_xact_lock(625020261012);

-- JSON insertion excludes the identity column; PostgreSQL owns identity sequences.
CREATE FUNCTION pg_temp.seed_insert(table_name text, values_json jsonb) RETURNS integer
LANGUAGE plpgsql AS $$
DECLARE columns_list text; result integer;
BEGIN
 SELECT string_agg(format('%I',column_name),',' ORDER BY ordinal_position) INTO columns_list
 FROM information_schema.columns WHERE table_schema='public' AND information_schema.columns.table_name=seed_insert.table_name AND column_name<>'Id';
 EXECUTE format('INSERT INTO public.%I (%s) SELECT %s FROM jsonb_populate_record(NULL::public.%I, $1) RETURNING "Id"',table_name,columns_list,columns_list,table_name) INTO result USING values_json;
 RETURN result;
END $$;

CREATE FUNCTION pg_temp.seed_setting(setting_name text, setting_value text) RETURNS void
LANGUAGE plpgsql AS $$ BEGIN
 UPDATE "Setting" SET "Value"=setting_value WHERE lower("Name")=lower(setting_name) AND "StoreId" IN (0,1);
 IF NOT FOUND THEN PERFORM pg_temp.seed_insert('Setting',jsonb_build_object('Name',lower(setting_name),'Value',setting_value,'StoreId',0)); END IF;
END $$;

CREATE FUNCTION pg_temp.seed_picture(seo text, alt text, bytes_hex text) RETURNS integer
LANGUAGE plpgsql AS $$
DECLARE picture integer;
BEGIN
 SELECT "Id" INTO picture FROM "Picture" WHERE "SeoFilename"=seo;
 IF picture IS NULL THEN
  picture:=pg_temp.seed_insert('Picture',jsonb_build_object('MimeType','image/svg+xml','SeoFilename',seo,'AltAttribute',alt,'TitleAttribute',alt,'IsNew',true,'VirtualPath',NULL));
  PERFORM pg_temp.seed_insert('PictureBinary',jsonb_build_object('PictureId',picture,'BinaryData','\x'||bytes_hex));
 END IF;
 RETURN picture;
END $$;

DO $seed$
DECLARE
 catalogue jsonb := $catalog$__CATALOG_JSON__$catalog$::jsonb;
 hero_hex text := '__HERO_HEX__';
 entry jsonb; color jsonb; size_entry jsonb;
 product_base jsonb; category_base jsonb;
 category_id integer; product_id integer; picture_id integer; hero_id integer;
 color_attribute integer; size_attribute integer; color_mapping integer; size_mapping integer;
 color_value integer; size_value integer; color_index integer; size_index integer;
 item_index integer:=0; color_ids integer[]; size_ids integer[]; attribute_xml text;
 language_id integer; currency_id integer; menu_id integer; slot integer:=0;
 stamp text := 'Boutique QA clothing sample v1';
BEGIN
 SELECT to_jsonb(p) INTO product_base FROM "Product" p WHERE "Id"=1;
 SELECT to_jsonb(c) INTO category_base FROM "Category" c WHERE "Id"=1;
 SELECT "Id" INTO currency_id FROM "Currency" WHERE "CurrencyCode"='IRT';
 SELECT "Id" INTO language_id FROM "Language" WHERE "LanguageCulture"='fa-IR' AND "Published" LIMIT 1;
 IF language_id IS NULL THEN RAISE EXCEPTION 'Published Persian language missing'; END IF;
 SELECT "Id" INTO color_attribute FROM "ProductAttribute" WHERE "Name"='رنگ' ORDER BY "Id" LIMIT 1;
 IF color_attribute IS NULL THEN color_attribute:=pg_temp.seed_insert('ProductAttribute',jsonb_build_object('Name','رنگ','Description','رنگ پوشاک نمونه')); END IF;
 SELECT "Id" INTO size_attribute FROM "ProductAttribute" WHERE "Name"='سایز' ORDER BY "Id" LIMIT 1;
 IF size_attribute IS NULL THEN size_attribute:=pg_temp.seed_insert('ProductAttribute',jsonb_build_object('Name','سایز','Description','سایز پوشاک نمونه')); END IF;

 FOR entry IN SELECT value FROM jsonb_array_elements(catalogue) LOOP
  item_index:=item_index+1;
  SELECT "EntityId" INTO category_id FROM "UrlRecord" WHERE "EntityName"='Category' AND "Slug"=entry->>'categorySlug' AND "IsActive";
  IF category_id IS NULL THEN
   category_id:=pg_temp.seed_insert('Category',category_base||jsonb_build_object('Name',entry->>'category','Description','<p>انتخاب‌های مجموعهٔ نمونهٔ بوتیک؛ تصاویر لباس، تصویرسازی آزمایشی هستند.</p>','Published',true,'Deleted',false,'ParentCategoryId',0,'PictureId',0,'ShowOnHomepage',true,'DisplayOrder',item_index,'PageSize',12,'PageSizeOptions','12,24,36','AllowCustomersToSelectPageSize',true,'CreatedOnUtc',timezone('UTC',now()),'UpdatedOnUtc',timezone('UTC',now())));
   PERFORM pg_temp.seed_insert('UrlRecord',jsonb_build_object('EntityName','Category','Slug',entry->>'categorySlug','EntityId',category_id,'IsActive',true,'LanguageId',0));
  END IF;
  SELECT "Id" INTO product_id FROM "Product" WHERE "Sku"=entry->>'sku';
  IF product_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "Product" WHERE "Id"=product_id AND "AdminComment"=stamp) THEN
   RAISE EXCEPTION 'Seed SKU collision: %',entry->>'sku';
  END IF;
  picture_id:=pg_temp.seed_picture(lower(entry->>'sku'),(entry->>'name')||' — تصویرسازی نمونه بدون مدل انسانی',entry->>'imageHex');
  IF product_id IS NULL THEN
   product_id:=pg_temp.seed_insert('Product',product_base||jsonb_build_object(
    'Name',entry->>'name','Sku',entry->>'sku','ShortDescription',entry->>'description',
    'FullDescription','<p>'||(entry->>'description')||'</p><p>کاتالوگ آزمایشی پوشاک برای ارزیابی قالب. تصویر، تصویرسازی اختصاصی نمونه است و عکس کالای واقعی نیست.</p><p>رنگ و سایز را پیش از افزودن به سبد انتخاب کنید. شست‌وشوی ملایم در دمای ۳۰ درجه پیشنهاد می‌شود.</p>',
    'AdminComment',stamp,'Price',(entry->>'price')::numeric,'OldPrice',(entry->>'oldPrice')::numeric,
    'Published',true,'Deleted',false,'VisibleIndividually',true,'ShowOnHomepage',true,'DisplayOrder',item_index,
    'AllowCustomerReviews',true,'ApprovedRatingSum',0,'ApprovedTotalReviews',0,'NotApprovedRatingSum',0,'NotApprovedTotalReviews',0,
    'ManageInventoryMethodId',2,'AllowAddingOnlyExistingAttributeCombinations',true,'StockQuantity',0,
    'DisplayStockAvailability',true,'DisplayStockQuantity',false,'BackorderModeId',0,
    'DisableBuyButton',false,'DisableWishlistButton',false,'MarkAsNew',true,'OrderMinimumQuantity',1,'OrderMaximumQuantity',5,
    'SubjectToAcl',false,'LimitedToStores',false,'VendorId',0,'IsGiftCard',false,'IsRental',false,'CustomerEntersPrice',false,
    'IsDownload',false,'IsRecurring',false,'RequireOtherProducts',false,'IsShipEnabled',true,'Weight',0.45,
    'CreatedOnUtc',timezone('UTC',now()),'UpdatedOnUtc',timezone('UTC',now())));
   PERFORM pg_temp.seed_insert('UrlRecord',jsonb_build_object('EntityName','Product','Slug',entry->>'slug','EntityId',product_id,'IsActive',true,'LanguageId',0));
   PERFORM pg_temp.seed_insert('Product_Category_Mapping',jsonb_build_object('ProductId',product_id,'CategoryId',category_id,'IsFeaturedProduct',false,'DisplayOrder',item_index));
   PERFORM pg_temp.seed_insert('Product_Picture_Mapping',jsonb_build_object('ProductId',product_id,'PictureId',picture_id,'DisplayOrder',0));
   color_mapping:=pg_temp.seed_insert('Product_ProductAttribute_Mapping',jsonb_build_object('ProductId',product_id,'ProductAttributeId',color_attribute,'TextPrompt','رنگ را انتخاب کنید','IsRequired',true,'AttributeControlTypeId',40,'DisplayOrder',0));
   size_mapping:=pg_temp.seed_insert('Product_ProductAttribute_Mapping',jsonb_build_object('ProductId',product_id,'ProductAttributeId',size_attribute,'TextPrompt','سایز را انتخاب کنید','IsRequired',true,'AttributeControlTypeId',CASE WHEN item_index % 2=0 THEN 2 ELSE 1 END,'DisplayOrder',1));
   color_ids:=ARRAY[]::integer[]; size_ids:=ARRAY[]::integer[]; color_index:=0; size_index:=0;
   FOR color IN SELECT value FROM jsonb_array_elements(entry->'colors') LOOP
    color_index:=color_index+1;
    color_value:=pg_temp.seed_insert('ProductAttributeValue',jsonb_build_object('ProductAttributeMappingId',color_mapping,'Name',color->>0,'ColorSquaresRgb',color->>1,'AttributeValueTypeId',0,'AssociatedProductId',0,'ImageSquaresPictureId',0,'PriceAdjustment',0,'PriceAdjustmentUsePercentage',false,'WeightAdjustment',0,'Cost',0,'CustomerEntersQty',false,'Quantity',1,'IsPreSelected',color_index=1,'DisplayOrder',color_index));
    color_ids:=array_append(color_ids,color_value);
   END LOOP;
   FOR size_entry IN SELECT value FROM jsonb_array_elements(entry->'sizes') LOOP
    size_index:=size_index+1;
    size_value:=pg_temp.seed_insert('ProductAttributeValue',jsonb_build_object('ProductAttributeMappingId',size_mapping,'Name',size_entry#>>'{}','AttributeValueTypeId',0,'AssociatedProductId',0,'ImageSquaresPictureId',0,'PriceAdjustment',0,'PriceAdjustmentUsePercentage',false,'WeightAdjustment',0,'Cost',0,'CustomerEntersQty',false,'Quantity',1,'IsPreSelected',size_index=1,'DisplayOrder',size_index));
    size_ids:=array_append(size_ids,size_value);
   END LOOP;
   FOR color_index IN 1..array_length(color_ids,1) LOOP
    FOR size_index IN 1..array_length(size_ids,1) LOOP
     attribute_xml:=format('<Attributes><ProductAttribute ID="%s"><ProductAttributeValue><Value>%s</Value></ProductAttributeValue></ProductAttribute><ProductAttribute ID="%s"><ProductAttributeValue><Value>%s</Value></ProductAttributeValue></ProductAttribute></Attributes>',color_mapping,color_ids[color_index],size_mapping,size_ids[size_index]);
     PERFORM pg_temp.seed_insert('ProductAttributeCombination',jsonb_build_object('ProductId',product_id,'AttributesXml',attribute_xml,'StockQuantity',CASE WHEN (item_index=1 AND color_index=2 AND size_index=3) OR (item_index=8 AND color_index=2 AND size_index=4) THEN 0 ELSE 8 END,'AllowOutOfStockOrders',false,'Sku',(entry->>'sku')||'-C'||color_index||'-S'||size_index,'NotifyAdminForQuantityBelow',0,'MinStockQuantity',1));
    END LOOP;
   END LOOP;
  END IF;
  UPDATE "Category" SET "PictureId"=picture_id WHERE "Id"=category_id AND "PictureId"=0;
 END LOOP;

 -- Keep source fixtures and their original rows; only remove stationery from the public catalogue.
 UPDATE "Product" SET "Published"=false,"ShowOnHomepage"=false,"UpdatedOnUtc"=timezone('UTC',now()) WHERE "Sku" IN ('MRG-1','MRG-2','MRG-3','MRG-4','MRG-5','MRG-6','MRG-7','MRG-8');
 UPDATE "Category" SET "Published"=false,"ShowOnHomepage"=false WHERE "Id" IN (1,2,3,4) AND "Name" IN ('دفتر و کاغذ','نوشت‌افزار','لوازم مدرسه','هنر و خلاقیت');
 UPDATE "Manufacturer" SET "Published"=false WHERE "Id" IN (SELECT pm."ManufacturerId" FROM "Product_Manufacturer_Mapping" pm JOIN "Product" p ON p."Id"=pm."ProductId" WHERE p."Sku" LIKE 'MRG-%');
 UPDATE "Menu" SET "Name"='دنیای بوتیک',"CssClass"='boutique-footer-shop' WHERE "Id"=7 AND NOT "Deleted";
 UPDATE "Menu" SET "CssClass"='boutique-main' WHERE "Id"=6 AND NOT "Deleted";
 UPDATE "Menu" SET "CssClass"='boutique-footer-help' WHERE "Id"=8 AND NOT "Deleted";
 UPDATE "MenuItem" SET "Url"=replace(replace(replace("Url",'/stationery/catalog','/boutique/catalog'),'/stationery/cart','/cart'),'/stationery/login','/login'),"Title"=CASE WHEN "Title"='پیشنهادهای رنگی' THEN 'انتخاب‌های ویژه' ELSE "Title" END WHERE "MenuId" IN (6,7,8) AND "Url" LIKE '%/stationery/%';
 FOR category_id IN SELECT u."EntityId" FROM "UrlRecord" u WHERE u."EntityName"='Category' AND u."Slug" IN ('boutique-tops','boutique-knitwear','boutique-outerwear','boutique-trousers','boutique-accessories') AND u."IsActive" ORDER BY u."Id" LOOP
  slot:=slot+1;
  IF slot<=4 THEN UPDATE "MenuItem" SET "EntityId"=category_id WHERE "MenuItemTypeId"=1 AND "MenuId" IN (6,7) AND "EntityId"=slot;
  ELSE
   FOR menu_id IN SELECT "Id" FROM "Menu" WHERE "Id" IN (6,7) AND NOT "Deleted" LOOP
    IF NOT EXISTS(SELECT 1 FROM "MenuItem" WHERE "MenuId"=menu_id AND "MenuItemTypeId"=1 AND "EntityId"=category_id) THEN
     PERFORM pg_temp.seed_insert('MenuItem',jsonb_build_object('MenuId',menu_id,'MenuItemTypeId',1,'EntityId',category_id,'TemplateId',0,'DisplayOrder',5,'SubjectToAcl',false,'LimitedToStores',false,'Published',true,'ParentId',0));
    END IF;
   END LOOP;
  END IF;
 END LOOP;
 UPDATE "Store" SET "Name"='فروشگاه لباس',"DefaultLanguageId"=language_id,"Url"='http://127.0.0.1:6250/',"HomepageTitle"='فروشگاه لباس | استایل مینیمال زنانه',"HomepageDescription"='مجموعهٔ نمونهٔ پوشاک زنانه؛ شومیز، بافت، کت، شلوار و اکسسوری در رنگ‌های خنثی',"DefaultTitle"='فروشگاه لباس' WHERE "Id"=1;
 UPDATE "Language" SET "DisplayOrder"=CASE WHEN "Id"=language_id THEN 0 ELSE 10+"Id" END;
 UPDATE "Language" SET "DefaultCurrencyId"=currency_id WHERE "Id"=language_id;
 UPDATE "Currency" SET "Published"=false WHERE "CurrencyCode"='USD';
 UPDATE "Currency" SET "DisplayLocale"='fa-IR',"CustomFormatting"='#,##0 تومان',"Published"=true WHERE "Id"=currency_id;
 UPDATE "Topic" SET "Title"='مجموعهٔ تازهٔ فروشگاه لباس',"Body"='<p>فرم‌های ساده، رنگ‌های آرام و انتخاب‌های کاربردی برای استایل روزمرهٔ تو.</p><p>این کاتالوگ آزمایشی است؛ تصاویر، تصویرسازی اختصاصی لباس هستند.</p>' WHERE "SystemName"='HomepageText';
 UPDATE "LocalizedProperty" SET "LocaleValue"=CASE WHEN "LocaleKey"='Title' THEN 'مجموعهٔ تازهٔ فروشگاه لباس' ELSE '<p>فرم‌های ساده، رنگ‌های آرام و انتخاب‌های کاربردی برای استایل روزمرهٔ تو.</p><p>این کاتالوگ آزمایشی است؛ تصاویر، تصویرسازی اختصاصی لباس هستند.</p>' END WHERE "LocaleKeyGroup"='Topic' AND "EntityId" IN (SELECT "Id" FROM "Topic" WHERE "SystemName"='HomepageText') AND "LocaleKey" IN ('Title','Body');
 PERFORM pg_temp.seed_setting('storeinformationsettings.logopictureid','0');
 hero_id:=pg_temp.seed_picture('boutique-hero-sample','چیدمان تصویری لباس‌های نمونهٔ بوتیک — بدون مدل انسانی',hero_hex);
 PERFORM pg_temp.seed_setting('boutiquethemesettings.heropictureid',hero_id::text);
 PERFORM pg_temp.seed_setting('boutiquethemesettings.heroheading','استایل ساده، انتخابِ تو');
 PERFORM pg_temp.seed_setting('boutiquethemesettings.herodescription','مجموعهٔ نمونهٔ لباس با رنگ‌های کرم، نود و موکا؛ تصویرسازی اختصاصی برای پیش‌نمایش قالب');
END $seed$;

COMMIT;
SELECT "Id","Name","Sku","Price","Published" FROM "Product" WHERE "Sku" LIKE 'BTQ-QA-%' ORDER BY "Sku";
SELECT c."Id",c."Name",c."PictureId",COUNT(pm."Id") AS products FROM "Category" c JOIN "Product_Category_Mapping" pm ON pm."CategoryId"=c."Id" JOIN "Product" p ON p."Id"=pm."ProductId" WHERE p."Sku" LIKE 'BTQ-QA-%' GROUP BY c."Id",c."Name",c."PictureId" ORDER BY c."Id";
SELECT COUNT(*) AS combinations,COUNT(*) FILTER (WHERE ac."StockQuantity">0) AS in_stock,COUNT(*) FILTER (WHERE ac."StockQuantity"=0) AS out_of_stock FROM "ProductAttributeCombination" ac JOIN "Product" p ON p."Id"=ac."ProductId" WHERE p."Sku" LIKE 'BTQ-QA-%';
