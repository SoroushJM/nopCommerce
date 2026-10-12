"""Prepare a public, owned native topic with the reviewed photograph attribution."""
import argparse,json,pathlib,html
p=argparse.ArgumentParser();p.add_argument('manifest');p.add_argument('output');a=p.parse_args();m=json.loads(pathlib.Path(a.manifest).read_text(encoding='utf-8-sig'))
def literal(s):return "'"+s.replace("'","''")+"'"
unique={x['sourceUrl']:x for x in [m['hero']]+m['products']}
body='<p>تصاویر برای نمایش در وب به اندازه‌های مناسب تبدیل شده‌اند.</p><ul>'
for url,x in unique.items():
 body+='<li><a href="'+html.escape(url,quote=True)+'">'+html.escape(url.split('/wiki/File:')[-1].replace('_',' '))+'</a> — '+html.escape(x['attribution'])+' — <a href="'+html.escape(x['licenseUrl'],quote=True)+'">'+html.escape(x['license'])+'</a></li>'
body+='</ul>'
sql=f'''\\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout='5s';
DO $$ DECLARE topic_id integer; template_id integer; menu_id integer; BEGIN
 IF current_database()<>'nop_boutique' OR NOT EXISTS(SELECT 1 FROM "Store" WHERE "Id"=1 AND "Url"='http://80.75.5.82:9012/') THEN RAISE EXCEPTION 'Wrong Boutique test deployment'; END IF;
 SELECT "Id" INTO STRICT template_id FROM "TopicTemplate" WHERE "ViewPath"='TopicDetails' AND "Name"='Default template';
 SELECT "Id" INTO STRICT menu_id FROM "Menu" WHERE "CssClass"='boutique-footer-help' AND "Published" AND NOT "Deleted";
 SELECT "Id" INTO topic_id FROM "Topic" WHERE "SystemName"='BoutiqueTestPhotoCredits';
 IF topic_id IS NULL THEN
 INSERT INTO "Topic"("SystemName","IncludeInSitemap","DisplayOrder","AccessibleWhenStoreClosed","IsPasswordProtected","Password","Title","Body","Published","TopicTemplateId","MetaKeywords","MetaDescription","MetaTitle","SubjectToAcl","LimitedToStores","AvailableStartDateTimeUtc","AvailableEndDateTimeUtc") VALUES('BoutiqueTestPhotoCredits',false,100,false,false,NULL,'منابع تصاویر',{literal(body)},true,template_id,NULL,NULL,NULL,false,false,NULL,NULL) RETURNING "Id" INTO topic_id;
 ELSE UPDATE "Topic" SET "Title"='منابع تصاویر',"Body"={literal(body)},"Published"=true WHERE "Id"=topic_id; END IF;
 IF EXISTS(SELECT 1 FROM "UrlRecord" WHERE "Slug"='boutique-photo-credits' AND ("EntityName"<>'Topic' OR "EntityId"<>topic_id)) THEN RAISE EXCEPTION 'Credits slug belongs to another entity'; END IF;
 IF NOT EXISTS(SELECT 1 FROM "UrlRecord" WHERE "EntityName"='Topic' AND "EntityId"=topic_id AND "LanguageId"=0 AND "Slug"='boutique-photo-credits') THEN INSERT INTO "UrlRecord"("EntityId","EntityName","Slug","IsActive","LanguageId") VALUES(topic_id,'Topic','boutique-photo-credits',true,0); END IF;
 IF NOT EXISTS(SELECT 1 FROM "MenuItem" WHERE "MenuId"=menu_id AND "MenuItemTypeId"=5 AND "EntityId"=topic_id) THEN INSERT INTO "MenuItem"("Title","Url","MenuItemTypeId","EntityId","NumberOfSubItemsPerGridElement","NumberOfItemsPerGridRow","MaximumNumberEntities","RouteName","TemplateId","DisplayOrder","CssClass","SubjectToAcl","LimitedToStores","Published","ParentId","MenuId") VALUES('منابع تصاویر',NULL,5,topic_id,NULL,NULL,NULL,NULL,0,100,'sf-menu-boutique-photo-credits',false,false,true,0,menu_id); END IF;
 UPDATE "MenuItem" SET "Published"=true WHERE "MenuId"=menu_id AND "CssClass"='sf-menu-boutique-photo-credits' AND "EntityId"=topic_id;
 RAISE NOTICE 'Owned native credits topic %, unique sources {len(unique)}',topic_id;
END $$;
COMMIT;
'''
pathlib.Path(a.output).write_text(sql,encoding='utf-8');print('Prepared native credits topic:',len(unique),'unique sources')
