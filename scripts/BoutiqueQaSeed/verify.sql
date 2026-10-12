SELECT current_database();
SELECT COUNT(*) AS products,MIN("Id") AS first_id,MAX("Id") AS last_id FROM "Product" WHERE "Sku" LIKE 'BTQ-QA-%' AND "AdminComment"='Boutique QA clothing sample v1';
SELECT p."Id",p."Sku",p."Price",p."ManageInventoryMethodId",p."AllowAddingOnlyExistingAttributeCombinations",COUNT(ac."Id") AS combinations,COUNT(ac."Id") FILTER(WHERE ac."StockQuantity">0) AS available,COUNT(ac."Id") FILTER(WHERE ac."StockQuantity"=0) AS sold_out FROM "Product" p JOIN "ProductAttributeCombination" ac ON ac."ProductId"=p."Id" WHERE p."Sku" LIKE 'BTQ-QA-%' GROUP BY p."Id" ORDER BY p."Sku";
SELECT COUNT(*) AS native_product_picture_links,COUNT(*) FILTER(WHERE pic."MimeType"='image/svg+xml' AND octet_length(pb."BinaryData")>0) AS valid_svg_binary FROM "Product_Picture_Mapping" pp JOIN "Product" p ON p."Id"=pp."ProductId" JOIN "Picture" pic ON pic."Id"=pp."PictureId" JOIN "PictureBinary" pb ON pb."PictureId"=pic."Id" WHERE p."Sku" LIKE 'BTQ-QA-%';
SELECT m."Id",m."Name",mi."Title",mi."Url",mi."MenuItemTypeId",mi."EntityId",c."Name" AS category FROM "Menu" m JOIN "MenuItem" mi ON mi."MenuId"=m."Id" LEFT JOIN "Category" c ON mi."MenuItemTypeId"=1 AND c."Id"=mi."EntityId" WHERE m."Id" IN (6,7,8) AND mi."Published" ORDER BY m."Id",mi."DisplayOrder";
SELECT "Id","Name","DefaultLanguageId","Url","HomepageTitle" FROM "Store" WHERE "Id"=1;
SELECT "Id","Name","LanguageCulture","DisplayOrder","DefaultCurrencyId","Rtl" FROM "Language" WHERE "Published";
SELECT "Id","Name","CurrencyCode","Rate","DisplayLocale","CustomFormatting" FROM "Currency" WHERE "Published";
SELECT "Name","Value" FROM "Setting" WHERE lower("Name") LIKE 'boutiquethemesettings.%' OR lower("Name") LIKE 'currencysettings.primary%';
SELECT COUNT(*) AS stationery_published FROM "Product" WHERE "Sku" LIKE 'MRG-%' AND "Published" AND NOT "Deleted";
SELECT "Id","SystemName","Title",left("Body",150) FROM "Topic" WHERE "SystemName"='HomepageText';
DO $$ DECLARE invalid integer; BEGIN
 SELECT COUNT(*) INTO invalid FROM "ProductAttributeCombination" ac JOIN "Product" p ON p."Id"=ac."ProductId"
 CROSS JOIN LATERAL unnest(xpath('/Attributes/ProductAttribute',xmlparse(document ac."AttributesXml"))) AS node(value)
 LEFT JOIN "Product_ProductAttribute_Mapping" map ON map."Id"=((xpath('string(/ProductAttribute/@ID)',node.value))[1])::text::integer AND map."ProductId"=p."Id"
 LEFT JOIN "ProductAttributeValue" val ON val."Id"=((xpath('string(/ProductAttribute/ProductAttributeValue/Value)',node.value))[1])::text::integer AND val."ProductAttributeMappingId"=map."Id"
 WHERE p."Sku" LIKE 'BTQ-QA-%' AND (map."Id" IS NULL OR val."Id" IS NULL);
 IF invalid<>0 THEN RAISE EXCEPTION 'Invalid native mapping/value XML: %',invalid; END IF;
 IF (SELECT COUNT(*) FROM "Product" WHERE "Sku" LIKE 'BTQ-QA-%')<>12 THEN RAISE EXCEPTION 'Duplicate/missing sample products'; END IF;
 IF (SELECT COUNT(*) FROM "Picture" WHERE "SeoFilename" LIKE 'btq-qa-%' OR "SeoFilename"='boutique-hero-sample')<>13 THEN RAISE EXCEPTION 'Duplicate/missing sample pictures'; END IF;
END $$;
