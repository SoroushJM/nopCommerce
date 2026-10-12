\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout='5s';
DO $$ BEGIN
 IF current_database()<>'nop_boutique' OR NOT EXISTS(SELECT 1 FROM "Store" WHERE "Id"=1 AND "Url"='http://80.75.5.82:9012/') THEN RAISE EXCEPTION 'Wrong Boutique test deployment'; END IF;
 IF (SELECT count(*) FROM "Product" WHERE "Sku" LIKE 'BTQ-QA-%' AND NOT "Deleted")<>12 THEN RAISE EXCEPTION 'Owned sample catalog mismatch'; END IF;
 IF NOT EXISTS(SELECT 1 FROM "Setting" WHERE "Name"='blogsettings.enabled' AND "StoreId"=0 AND lower("Value")='true') THEN RAISE EXCEPTION 'Blog is disabled; preserve merchant setting'; END IF;
 IF (SELECT count(*) FROM "Menu" WHERE "CssClass" IN ('boutique-main','boutique-footer-help') AND "Published" AND NOT "Deleted")<>2 THEN RAISE EXCEPTION 'Expected owned main and help menus'; END IF;
END $$;
SELECT pg_advisory_xact_lock(901290132026);
INSERT INTO "MenuItem"("Title","Url","MenuItemTypeId","EntityId","NumberOfSubItemsPerGridElement","NumberOfItemsPerGridRow","MaximumNumberEntities","RouteName","TemplateId","DisplayOrder","CssClass","SubjectToAcl","LimitedToStores","Published","ParentId","MenuId")
SELECT 'وبلاگ',NULL,0,NULL,NULL,NULL,NULL,'Blog',0,COALESCE((SELECT max(i."DisplayOrder")+1 FROM "MenuItem" i WHERE i."MenuId"=m."Id"),0),'sf-menu-boutique-blog',false,false,true,0,m."Id"
FROM "Menu" m WHERE m."CssClass" IN ('boutique-main','boutique-footer-help') AND m."Published" AND NOT m."Deleted" AND NOT EXISTS(SELECT 1 FROM "MenuItem" i WHERE i."MenuId"=m."Id" AND i."RouteName"='Blog' AND i."ParentId"=0);
UPDATE "MenuItem" SET "Title"='وبلاگ',"Published"=true WHERE "CssClass"='sf-menu-boutique-blog' AND "RouteName"='Blog' AND "MenuId" IN (SELECT "Id" FROM "Menu" WHERE "CssClass" IN ('boutique-main','boutique-footer-help'));
SELECT m."CssClass",i."Id",i."Title",i."RouteName",i."Published" FROM "MenuItem" i JOIN "Menu" m ON m."Id"=i."MenuId" WHERE i."RouteName"='Blog' AND m."CssClass" IN ('boutique-main','boutique-footer-help');
COMMIT;
