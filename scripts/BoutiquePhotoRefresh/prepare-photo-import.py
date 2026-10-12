"""Build a bounded native-picture transaction from reviewed, locally saved photos."""
import argparse, hashlib, json, pathlib
p=argparse.ArgumentParser(); p.add_argument('manifest'); p.add_argument('output'); a=p.parse_args()
manifest=pathlib.Path(a.manifest).resolve(); data=json.loads(manifest.read_text(encoding='utf-8-sig'))
products=data['products']; expected={f'BTQ-QA-{i:03}' for i in range(1,13)}
if len(products)!=12 or {x['sku'] for x in products}!=expected: raise SystemExit('Exactly the 12 owned Boutique sample SKUs are required')
def literal(value): return "'"+value.replace("'","''")+"'"
def read_photo(entry):
 path=pathlib.Path(entry.get('path',entry.get('localFile'))); path=path if path.is_absolute() else manifest.parent/path
 binary=path.read_bytes()
 if not 1000<=len(binary)<=5_000_000: raise SystemExit('Photo byte size outside allowed sample range: '+path.name)
 if binary.startswith(b'\xff\xd8\xff'): mime='image/jpeg'
 elif binary.startswith(b'\x89PNG\r\n\x1a\n'): mime='image/png'
 elif binary[:4]==b'RIFF' and binary[8:12]==b'WEBP': mime='image/webp'
 else: raise SystemExit('Expected a real JPEG, PNG or WebP photo: '+path.name)
 if entry.get('mimeType',mime)!=mime: raise SystemExit('MIME metadata does not match file signature')
 return binary,mime,hashlib.sha256(binary).hexdigest()
sql=['''\\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout='5s'; SET LOCAL statement_timeout='90s';
SELECT pg_advisory_xact_lock(901290132026);
DO $$ BEGIN
 IF current_database()<>'nop_boutique' OR NOT EXISTS(SELECT 1 FROM "Store" WHERE "Id"=1 AND "Url"='http://80.75.5.82:9012/') THEN RAISE EXCEPTION 'Wrong Boutique test deployment'; END IF;
 IF (SELECT count(*) FROM "Product" WHERE "Sku" LIKE 'BTQ-QA-%' AND NOT "Deleted")<>12 THEN RAISE EXCEPTION 'Owned sample catalog mismatch'; END IF;
 IF EXISTS(SELECT 1 FROM "Setting" WHERE lower("Name")='media.images.storeindb' AND lower("Value")<>'true') THEN RAISE EXCEPTION 'Native picture storage is not database-backed'; END IF;
 IF EXISTS(SELECT 1 FROM "ProductAttributeValue" v JOIN "Product_ProductAttribute_Mapping" m ON m."Id"=v."ProductAttributeMappingId" JOIN "Product" p ON p."Id"=m."ProductId" WHERE p."Sku" LIKE 'BTQ-QA-%' AND v."ImageSquaresPictureId"<>0)
 OR EXISTS(SELECT 1 FROM "ProductAttributeValuePicture" vp JOIN "ProductAttributeValue" v ON v."Id"=vp."ProductAttributeValueId" JOIN "Product_ProductAttribute_Mapping" m ON m."Id"=v."ProductAttributeMappingId" JOIN "Product" p ON p."Id"=m."ProductId" WHERE p."Sku" LIKE 'BTQ-QA-%')
 OR EXISTS(SELECT 1 FROM "ProductAttributeCombinationPicture" cp JOIN "ProductAttributeCombination" c ON c."Id"=cp."ProductAttributeCombinationId" JOIN "Product" p ON p."Id"=c."ProductId" WHERE p."Sku" LIKE 'BTQ-QA-%') THEN RAISE EXCEPTION 'New option-picture references require explicit remapping before photo refresh'; END IF;
END $$;
CREATE TEMP TABLE photo_import_before AS SELECT
 (SELECT jsonb_agg(to_jsonb(p) ORDER BY p."Id") FROM "Product" p WHERE p."Sku" LIKE 'BTQ-QA-%' AND NOT p."Deleted") products,
 (SELECT jsonb_agg(to_jsonb(c) ORDER BY c."Id") FROM "ProductAttributeCombination" c JOIN "Product" p ON p."Id"=c."ProductId" WHERE p."Sku" LIKE 'BTQ-QA-%' AND NOT p."Deleted") combinations;
CREATE TEMP TABLE photo_import_report(sku text, old_picture integer,new_picture integer,sha256 text);
''']
report=[]
for entry in sorted(products,key=lambda x:x['sku']):
 binary,mime,digest=read_photo(entry); sku=entry['sku']; seo=sku.lower()+'-photo-'+digest[:12]
 sql.append(f'''DO $$ DECLARE product_id integer; mapping_id integer; old_picture integer; new_picture integer; product_name text; BEGIN
 SELECT "Id","Name" INTO STRICT product_id,product_name FROM "Product" WHERE "Sku"={literal(sku)} AND NOT "Deleted";
 IF (SELECT count(*) FROM "Product_Picture_Mapping" WHERE "ProductId"=product_id)<>1 THEN RAISE EXCEPTION 'Expected one owned sample photo mapping'; END IF;
 SELECT "Id","PictureId" INTO STRICT mapping_id,old_picture FROM "Product_Picture_Mapping" WHERE "ProductId"=product_id;
 SELECT p."Id" INTO new_picture FROM "Picture" p JOIN "PictureBinary" b ON b."PictureId"=p."Id" WHERE p."Id"=old_picture AND p."MimeType"={literal(mime)} AND encode(digest(b."BinaryData",'sha256'),'hex')={literal(digest)};
 IF new_picture IS NULL THEN
  INSERT INTO "Picture"("MimeType","SeoFilename","AltAttribute","TitleAttribute","IsNew","VirtualPath") VALUES({literal(mime)},{literal(seo)},product_name,product_name,true,NULL) RETURNING "Id" INTO new_picture;
  INSERT INTO "PictureBinary"("PictureId","BinaryData") VALUES(new_picture,decode({literal(binary.hex())},'hex'));
  UPDATE "Product_Picture_Mapping" SET "PictureId"=new_picture WHERE "Id"=mapping_id AND "ProductId"=product_id;
 END IF;
 INSERT INTO photo_import_report VALUES({literal(sku)},old_picture,new_picture,{literal(digest)});
END $$;''')
 report.append({'sku':sku,'sha256':digest,'bytes':len(binary),'mimeType':mime,'sourceUrl':entry.get('sourceUrl'),'licenseUrl':entry.get('licenseUrl'),'license':entry.get('license'),'attribution':entry.get('attribution')})
hero=data['hero']; binary,mime,digest=read_photo(hero)
sql.append(f'''DO $$ DECLARE setting_id integer; old_picture integer; new_picture integer; BEGIN
 SELECT "Id","Value"::integer INTO STRICT setting_id,old_picture FROM "Setting" WHERE "Name"='boutiquethemesettings.heropictureid' AND "StoreId"=0;
 SELECT p."Id" INTO new_picture FROM "Picture" p JOIN "PictureBinary" b ON b."PictureId"=p."Id" WHERE p."Id"=old_picture AND p."MimeType"={literal(mime)} AND encode(digest(b."BinaryData",'sha256'),'hex')={literal(digest)};
 IF new_picture IS NULL THEN
  INSERT INTO "Picture"("MimeType","SeoFilename","AltAttribute","TitleAttribute","IsNew","VirtualPath") VALUES({literal(mime)},{literal('boutique-hero-photo-'+digest[:12])},{literal(hero.get('alt',hero.get('description','تصویر لباس‌های بوتیک')))},{literal(hero.get('alt',hero.get('description','تصویر لباس‌های بوتیک')))},true,NULL) RETURNING "Id" INTO new_picture;
  INSERT INTO "PictureBinary"("PictureId","BinaryData") VALUES(new_picture,decode({literal(binary.hex())},'hex'));
  UPDATE "Setting" SET "Value"=new_picture::text WHERE "Id"=setting_id;
 END IF;
 INSERT INTO photo_import_report VALUES('HERO',old_picture,new_picture,{literal(digest)});
END $$;''')
if hero.get('storeDescription'):
 sql.append('UPDATE "Setting" SET "Value"='+literal(hero['storeDescription'])+' WHERE "Name"=\'boutiquethemesettings.herodescription\' AND "StoreId"=0;')
sql.append('''DO $$ BEGIN
 IF (SELECT products FROM photo_import_before) IS DISTINCT FROM (SELECT jsonb_agg(to_jsonb(p) ORDER BY p."Id") FROM "Product" p WHERE p."Sku" LIKE 'BTQ-QA-%' AND NOT p."Deleted") THEN RAISE EXCEPTION 'Product data changed unexpectedly'; END IF;
 IF (SELECT combinations FROM photo_import_before) IS DISTINCT FROM (SELECT jsonb_agg(to_jsonb(c) ORDER BY c."Id") FROM "ProductAttributeCombination" c JOIN "Product" p ON p."Id"=c."ProductId" WHERE p."Sku" LIKE 'BTQ-QA-%' AND NOT p."Deleted") THEN RAISE EXCEPTION 'Variant stock changed unexpectedly'; END IF;
END $$;
TABLE photo_import_report;
COMMIT;
''')
output=pathlib.Path(a.output); output.write_text('\n'.join(sql),encoding='utf-8')
output.with_suffix('.metadata.json').write_text(json.dumps({'products':report,'hero':{'sha256':digest,'bytes':len(binary),'mimeType':mime,'sourceUrl':hero.get('sourceUrl'),'licenseUrl':hero.get('licenseUrl'),'license':hero.get('license'),'attribution':hero.get('attribution')}},ensure_ascii=False,indent=2),encoding='utf-8')
print('Prepared bounded native-photo transaction:',output,'products',len(report),'hero',1)
