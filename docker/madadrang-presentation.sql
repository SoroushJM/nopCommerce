-- Explicit deployment migration for an installed Madadrang stationery store.
-- Sample catalogue is included only when seed_data=true; existing products are retained.
\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '90s';
SELECT pg_advisory_xact_lock(1842020);
CREATE TEMP TABLE deployment_target AS SELECT :store_id::integer AS id;
-- Validate the installation/target before optional seed writes anything.
DO $$ DECLARE sid integer := (SELECT id FROM deployment_target); BEGIN
    IF (SELECT count(*) FROM "Store" WHERE NOT "Deleted")<>1
        OR NOT EXISTS (SELECT 1 FROM "Store" WHERE "Id"=sid AND NOT "Deleted") THEN
        RAISE EXCEPTION 'An installed, separate single-store database and valid store ID are required';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM "Setting" WHERE "Name"='storeinformationsettings.defaultstoretheme'
        AND "StoreId" IN (0,sid) AND "Value" IN ('Madadrang','DefaultClean')) THEN
        RAISE EXCEPTION 'Unexpected storefront theme; refusing to configure another storefront';
    END IF;
END $$;
\if :seed_data
\ir madadrang-seed.sql
\endif
DO $$
DECLARE
    sid integer := (SELECT id FROM deployment_target);
    fa integer;
    mid integer;
    item record;
    cat record;
    menu record;
    wanted jsonb;
    actual jsonb;
BEGIN
    SELECT "Id" INTO fa FROM "Language" WHERE "LanguageCulture"='fa-IR' AND "Published" AND "Rtl";
    IF fa IS NULL THEN RAISE EXCEPTION 'A published RTL fa-IR language is required; configure it or use --seed-data for a test catalogue'; END IF;
    IF EXISTS (SELECT 1 FROM "GenericAttribute" WHERE "KeyGroup"='Store' AND "EntityId"=sid
        AND "Key"='Madadrang.DeploymentPresentationVersion' AND "Value"='1') THEN
        RAISE NOTICE 'Madadrang presentation v1 already applied; merchant edits are preserved';
        RETURN;
    END IF;

    UPDATE "Setting" SET "Value"='Madadrang' WHERE "Name"='storeinformationsettings.defaultstoretheme' AND "StoreId" IN (0,sid);
    UPDATE "Store" SET "DefaultLanguageId"=fa WHERE "Id"=sid;
    -- Existing anonymous sessions also used the installer language. Account preferences stay intact.
    UPDATE "Customer" c SET "LanguageId"=fa
    WHERE (c."LanguageId" IS NULL OR c."LanguageId" IN (SELECT "Id" FROM "Language" WHERE "LanguageCulture" LIKE 'en%'))
      AND EXISTS (SELECT 1 FROM "Customer_CustomerRole_Mapping" m JOIN "CustomerRole" r ON r."Id"=m."CustomerRole_Id"
          WHERE m."Customer_Id"=c."Id" AND r."SystemName"='Guests')
      AND NOT EXISTS (SELECT 1 FROM "Customer_CustomerRole_Mapping" m JOIN "CustomerRole" r ON r."Id"=m."CustomerRole_Id"
          WHERE m."Customer_Id"=c."Id" AND r."SystemName" IN ('Administrators','Registered'));

    -- Only remove Swiper when its configuration is still the exact two installer slides.
    IF EXISTS (SELECT 1 FROM "Setting" WHERE "Name"='swipersettings.slides' AND "StoreId"=0
        AND "Value"::jsonb = jsonb_build_array(
            jsonb_build_object('PictureId',1,'TitleText','iPhone 16','LinkUrl',(SELECT "Url" FROM "Store" WHERE "Id"=sid),'AltText','iPhone 16'),
            jsonb_build_object('PictureId',2,'TitleText','Salaxy S24','LinkUrl',(SELECT "Url" FROM "Store" WHERE "Id"=sid),'AltText','Salaxy S24'))) THEN
        UPDATE "Setting" s SET "Value"=(SELECT coalesce(string_agg(btrim(name),','),'')
            FROM unnest(string_to_array(s."Value",',')) name WHERE btrim(name)<>'Widgets.Swiper')
        WHERE "Name"='widgetsettings.activewidgetsystemnames' AND "StoreId"=0;
    END IF;

    -- Add only missing native routes; retain all existing slugs.
    INSERT INTO "UrlRecord" ("EntityName","Slug","EntityId","IsActive","LanguageId")
    SELECT entity,slug,id,true,0 FROM (
        SELECT 'Category' AS entity,'madadrang-category-'||"Id" AS slug,"Id" AS id FROM "Category"
            WHERE "Published" AND NOT "Deleted"
        UNION ALL SELECT 'Product',CASE WHEN "Sku" IN ('MRG-1','MRG-2','MRG-3','MRG-4','MRG-5','MRG-6','MRG-7','MRG-8') THEN lower("Sku") ELSE 'madadrang-product-'||"Id" END,"Id" FROM "Product" WHERE "Published" AND NOT "Deleted"
        UNION ALL SELECT 'Manufacturer','madadrang-brand-'||m."Id",m."Id" FROM "Manufacturer" m
            WHERE NOT m."Deleted" AND EXISTS (SELECT 1 FROM "Product_Manufacturer_Mapping" pm JOIN "Product" p ON p."Id"=pm."ProductId" WHERE pm."ManufacturerId"=m."Id" AND p."Sku" LIKE 'MRG-%')
    ) routes WHERE NOT EXISTS (SELECT 1 FROM "UrlRecord" u WHERE u."EntityName"=routes.entity AND u."EntityId"=routes.id AND u."LanguageId"=0 AND u."IsActive");

    FOR menu IN SELECT * FROM (VALUES
        ('madadrang-main','فروشگاه',5),('madadrang-footer-shop','دنیای مداد رنگی',10),('madadrang-footer-help','کنار تو هستیم',10)) v(marker,title,kind) LOOP
        IF EXISTS (SELECT 1 FROM "Menu" WHERE "CssClass"=menu.marker) THEN CONTINUE; END IF;
        INSERT INTO "Menu" ("Name","CssClass","MenuTypeId","DisplayOrder","Published","SubjectToAcl","LimitedToStores","Deleted","DisplayAllCategories")
        VALUES (menu.title,menu.marker,menu.kind,-10,true,false,false,false,false) RETURNING "Id" INTO mid;
        IF menu.marker IN ('madadrang-main','madadrang-footer-shop') THEN
            INSERT INTO "MenuItem" ("Title","CssClass","MenuId","Url","MenuItemTypeId","TemplateId","DisplayOrder","SubjectToAcl","LimitedToStores","Published","ParentId")
            VALUES ('همهٔ محصولات','sf-menu-all',mid,'/catalog/all',6,0,0,false,false,true,0);
            INSERT INTO "MenuItem" ("MenuId","MenuItemTypeId","EntityId","TemplateId","DisplayOrder","SubjectToAcl","LimitedToStores","Published","ParentId")
            SELECT mid,1,c."Id",0,row_number() OVER (ORDER BY c."DisplayOrder",c."Id"),false,false,true,0 FROM "Category" c
            WHERE c."Published" AND NOT c."Deleted" AND c."ParentCategoryId"=0;
            IF menu.marker='madadrang-main' THEN
                INSERT INTO "MenuItem" ("Title","CssClass","MenuId","Url","MenuItemTypeId","TemplateId","DisplayOrder","SubjectToAcl","LimitedToStores","Published","ParentId")
                VALUES ('پیشنهادهای رنگی','sf-menu-offer',mid,'/catalog/all?orderby=10',6,0,5,false,false,true,0);
            END IF;
        ELSE
            INSERT INTO "MenuItem" ("Title","MenuId","Url","MenuItemTypeId","TemplateId","DisplayOrder","SubjectToAcl","LimitedToStores","Published","ParentId")
            VALUES ('سبد خرید',mid,'/cart',6,0,0,false,false,true,0),('حساب من',mid,'/customer/info',6,0,1,false,false,true,0);
            INSERT INTO "MenuItem" ("Title","MenuId","RouteName","MenuItemTypeId","TemplateId","DisplayOrder","SubjectToAcl","LimitedToStores","Published","ParentId")
            VALUES ('تماس با ما',mid,'ContactUs',0,0,2,false,false,true,0);
        END IF;
    END LOOP;

    -- Unpublish only complete untouched installer-menu fingerprints.
    FOR menu IN SELECT * FROM "Menu" WHERE "Name" IN ('Menu','Information','Customer service','My account')
        AND "Published" AND NOT "Deleted" AND NOT "SubjectToAcl" AND NOT "LimitedToStores" AND NOT "DisplayAllCategories" AND coalesce("CssClass",'')='' LOOP
        wanted := CASE menu."Name"
            WHEN 'Menu' THEN '[ ["Home page","Homepage",0,null,true],["New products","NewProducts",0,null,true],["Search","ProductSearch",0,null,true],["My account","CustomerInfo",0,null,true],["Blog","Blog",0,null,true],["Contact us","ContactUs",0,null,true] ]'::jsonb
            WHEN 'Customer service' THEN '[ ["Search","ProductSearch",0,null,true],["Blog","Blog",0,null,true],["Recently viewed products","RecentlyViewedProducts",0,null,true],["Compare products list","CompareProducts",0,null,true],["New products","NewProducts",0,null,true] ]'::jsonb
            WHEN 'My account' THEN '[ ["My account","CustomerInfo",0,null,true],["Orders","CustomerOrders",0,null,true],["Withdraw contract","WithdrawalRequestForm",0,null,false],["Addresses","CustomerAddresses",0,null,true],["Shopping cart","ShoppingCart",0,null,true],["Wishlist","Wishlist",0,null,true],["Apply for vendor account","ApplyVendorAccount",0,null,true] ]'::jsonb
            ELSE (SELECT jsonb_agg(entry) FROM (
                SELECT jsonb_build_array('Sitemap','Sitemap',0,NULL,true) entry UNION ALL SELECT jsonb_build_array('Contact us','ContactUs',0,NULL,true)
                UNION ALL SELECT jsonb_build_array('', '',5,"Id",true) FROM "Topic" WHERE "SystemName" IN ('ShippingInfo','PrivacyInfo','ConditionsOfUse','AboutUs')) t)
            END;
        SELECT jsonb_agg(jsonb_build_array(coalesce("Title",''),coalesce("RouteName",''),"MenuItemTypeId","EntityId","Published")) INTO actual FROM "MenuItem" WHERE "MenuId"=menu."Id";
        IF actual IS NOT NULL AND jsonb_array_length(actual)=jsonb_array_length(wanted) AND actual @> wanted AND wanted @> actual
            AND menu."MenuTypeId"=(CASE WHEN menu."Name"='Menu' THEN 5 ELSE 10 END)
            AND menu."DisplayOrder"=(CASE menu."Name" WHEN 'Customer service' THEN 1 WHEN 'My account' THEN 2 ELSE 0 END)
            AND NOT EXISTS (SELECT 1 FROM "MenuItem" WHERE "MenuId"=menu."Id" AND
                ("ParentId"<>0 OR "DisplayOrder"<>0 OR "SubjectToAcl" OR "LimitedToStores" OR coalesce("CssClass",'')<>'' OR coalesce("Url",'')<>'' OR "TemplateId"<>0
                 OR "MaximumNumberEntities" IS NOT NULL OR "NumberOfItemsPerGridRow" IS NOT NULL OR "NumberOfSubItemsPerGridElement" IS NOT NULL))
            AND NOT EXISTS (SELECT 1 FROM "LocalizedProperty" WHERE ("LocaleKeyGroup"='Menu' AND "EntityId"=menu."Id") OR
                ("LocaleKeyGroup"='MenuItem' AND "EntityId" IN (SELECT "Id" FROM "MenuItem" WHERE "MenuId"=menu."Id"))) THEN
            UPDATE "Menu" SET "Published"=false WHERE "Id"=menu."Id";
        ELSE RAISE NOTICE 'Preserved edited installer menu %',menu."Name";
        END IF;
    END LOOP;
    UPDATE "Topic" t SET "Published"=false WHERE "SystemName"='HomepageText' AND "Title"='Welcome to our store'
        AND "Body"='<p>Online shopping is the process consumers go through to purchase products or services over the Internet. You can edit this in the admin site.</p><p>If you have questions, see the <a href="http://docs.nopcommerce.com/">Documentation</a>, or post in the <a href="https://www.nopcommerce.com/boards/">Forums</a> at <a href="https://www.nopcommerce.com">nopCommerce.com</a></p>'
        AND NOT "IsPasswordProtected" AND NOT "SubjectToAcl" AND NOT "LimitedToStores" AND NOT "IncludeInSitemap" AND NOT "AccessibleWhenStoreClosed"
        AND "AvailableStartDateTimeUtc" IS NULL AND "AvailableEndDateTimeUtc" IS NULL AND "DisplayOrder"=1
        AND coalesce("Password",'')='' AND coalesce("MetaTitle",'')='' AND coalesce("MetaDescription",'')='' AND coalesce("MetaKeywords",'')=''
        AND EXISTS (SELECT 1 FROM "TopicTemplate" tt WHERE tt."Id"=t."TopicTemplateId" AND tt."Name"='Default template' AND tt."ViewPath"='TopicDetails')
        AND NOT EXISTS (SELECT 1 FROM "LocalizedProperty" WHERE "LocaleKeyGroup"='Topic' AND "EntityId"=t."Id");
    INSERT INTO "GenericAttribute" ("KeyGroup","Key","Value","EntityId","StoreId","CreatedOrUpdatedDateUTC")
    VALUES ('Store','Madadrang.DeploymentPresentationVersion','1',sid,0,now());
    RAISE NOTICE 'Applied Madadrang presentation v1 to store %',sid;
END $$;
-- nopCommerce's request-culture middleware uses the first published language,
-- independently of Store.DefaultLanguageId. Align both defaults once.
DO $$
DECLARE sid integer := (SELECT id FROM deployment_target); fa integer;
BEGIN
    IF EXISTS (SELECT 1 FROM "GenericAttribute" WHERE "KeyGroup"='Store' AND "EntityId"=sid
        AND "Key"='Madadrang.PublicCultureVersion' AND "Value"='1') THEN RETURN; END IF;
    SELECT "Id" INTO STRICT fa FROM "Language" WHERE "LanguageCulture"='fa-IR' AND "Published" AND "Rtl";
    UPDATE "Language" SET "DisplayOrder"=least(0,(SELECT min("DisplayOrder")-1 FROM "Language" WHERE "Published" AND "Id"<>fa)) WHERE "Id"=fa;
    INSERT INTO "GenericAttribute" ("KeyGroup","Key","Value","EntityId","StoreId","CreatedOrUpdatedDateUTC")
    VALUES ('Store','Madadrang.PublicCultureVersion','1',sid,0,now());
END $$;
-- Missing public translations only; existing manual translations are retained.
\ir madadrang-public-resources.sql
COMMIT;
