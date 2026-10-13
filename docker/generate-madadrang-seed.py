#!/usr/bin/env python3
"""Generate a transactional seed include from the versioned catalogue/image bundle."""
import base64
import json
from pathlib import Path
import sys


def literal(value):
    return "'" + json.dumps(value, ensure_ascii=False).replace("'", "''") + "'::jsonb"


def generate(manifest):
    if manifest.get("version") != 1 or len(manifest.get("products", [])) != 8:
        raise ValueError("Expected the version-1, eight-product stationery catalogue")
    if {p["id"] for p in manifest["products"]} != set(range(1, 9)):
        raise ValueError("Sample product identifiers must be unique (1..8)")
    for product in manifest["products"]:
        if not base64.b64decode(product["imageBase64"], validate=True).startswith(b"\xff\xd8"):
            raise ValueError(f"Invalid JPEG for sample {product['id']}")
    sql = [r'''
-- Included only with --seed-data, inside the deployment transaction.
-- Fill required database columns with their native zero/false/empty defaults.
-- Supplied product/template/inventory fields below override those defaults.
CREATE FUNCTION pg_temp.madadrang_insert(tab text, doc jsonb) RETURNS integer LANGUAGE plpgsql AS $$
DECLARE c record; defaults jsonb := '{}'::jsonb; cols text; result integer; val jsonb;
BEGIN
 FOR c IN SELECT * FROM information_schema.columns WHERE table_schema='public' AND table_name=tab AND column_name<>'Id' ORDER BY ordinal_position LOOP
  IF c.is_nullable='NO' THEN
   val := CASE
    WHEN c.data_type='boolean' THEN 'false'::jsonb
    WHEN c.data_type IN ('smallint','integer','bigint','numeric','real','double precision') THEN '0'::jsonb
    WHEN c.data_type IN ('text','character varying','character') OR c.udt_name='citext' THEN '""'::jsonb
    WHEN c.data_type IN ('timestamp without time zone','timestamp with time zone','date') THEN to_jsonb(now()::text)
    WHEN c.data_type='bytea' THEN to_jsonb('\x'::text)
    ELSE NULL END;
   IF val IS NULL AND NOT doc ? c.column_name THEN RAISE EXCEPTION 'Unsupported required column %.%',tab,c.column_name; END IF;
   defaults := defaults || jsonb_build_object(c.column_name,val);
  END IF;
 END LOOP;
 SELECT string_agg(format('%I',column_name),',' ORDER BY ordinal_position) INTO cols FROM information_schema.columns
 WHERE table_schema='public' AND table_name=tab AND column_name<>'Id';
 IF cols IS NULL THEN RAISE EXCEPTION 'Required installed nopCommerce table % is missing',tab; END IF;
 EXECUTE format('INSERT INTO %I (%s) SELECT %s FROM jsonb_populate_record(NULL::%I,$1) RETURNING "Id"',tab,cols,cols,tab)
 INTO result USING defaults || doc;
 RETURN result;
END $$;
CREATE TEMP TABLE seeded_categories(name text PRIMARY KEY, id integer);
CREATE TEMP TABLE seeded_brands(name text PRIMARY KEY, id integer);
DO $$ DECLARE n integer; BEGIN
 IF NOT EXISTS (SELECT 1 FROM "Language" WHERE "LanguageCulture"='fa-IR') THEN
  PERFORM pg_temp.madadrang_insert('Language','{"Name":"فارسی","LanguageCulture":"fa-IR","UniqueSeoCode":"fa","FlagImageFileName":"ir.png","Published":true,"Rtl":true,"DisplayOrder":0}'::jsonb);
 END IF;
 IF NOT EXISTS (SELECT 1 FROM "Currency" WHERE "CurrencyCode"='IRT') THEN
  PERFORM pg_temp.madadrang_insert('Currency','{"Name":"تومان","CurrencyCode":"IRT","Rate":1,"Published":true,"DisplayLocale":"en-US","CustomFormatting":"#,##0 تومان","DisplayOrder":-1}'::jsonb);
 END IF;
 IF NOT EXISTS (SELECT 1 FROM "CategoryTemplate" WHERE "Id"=1)
 OR NOT EXISTS (SELECT 1 FROM "ProductTemplate" WHERE "Id"=1)
 OR NOT EXISTS (SELECT 1 FROM "ManufacturerTemplate" WHERE "Id"=1) THEN
  RAISE EXCEPTION 'Standard nopCommerce templates are required; finish installation before seeding';
 END IF;
END $$;
''']
    categories = list(dict.fromkeys(p["category"] for p in manifest["products"]))
    # Match the accepted four-tile order, independent of product ordering.
    categories.sort(key=lambda name: ["دفتر و کاغذ", "نوشت‌افزار", "لوازم مدرسه", "هنر و خلاقیت"].index(name))
    brands = list(dict.fromkeys(p["brand"] for p in manifest["products"]))
    for index, name in enumerate(categories):
        doc = {"Name": name, "Published": True, "CategoryTemplateId": 1, "PageSize": 12,
               "AllowCustomersToSelectPageSize": True, "PageSizeOptions": "12,24,36",
               "ShowOnHomepage": True, "DisplayOrder": index}
        sql.append(f"""DO $$ DECLARE n integer; BEGIN
IF (SELECT count(*) FROM "Category" WHERE "Name"={literal(name)}#>>'{{}}' AND NOT "Deleted")>1 THEN RAISE EXCEPTION 'Duplicate sample category'; END IF;
SELECT "Id" INTO n FROM "Category" WHERE "Name"={literal(name)}#>>'{{}}' AND NOT "Deleted";
IF n IS NULL THEN n:=pg_temp.madadrang_insert('Category',{literal(doc)}); END IF;
INSERT INTO seeded_categories VALUES({literal(name)}#>>'{{}}',n);
END $$;""")
    for name in brands:
        doc = {"Name": name, "Published": True, "ManufacturerTemplateId": 1, "PageSize": 12}
        sql.append(f"""DO $$ DECLARE n integer; BEGIN
SELECT "Id" INTO n FROM "Manufacturer" WHERE "Name"={literal(name)}#>>'{{}}' AND NOT "Deleted" ORDER BY "Id" LIMIT 1;
IF n IS NULL THEN n:=pg_temp.madadrang_insert('Manufacturer',{literal(doc)}); END IF;
INSERT INTO seeded_brands VALUES({literal(name)}#>>'{{}}',n);
END $$;""")
    sql.append(r'''CREATE TEMP TABLE seeded_color AS SELECT "Id" AS id FROM "ProductAttribute" WHERE "Name"='رنگ' ORDER BY "Id" LIMIT 1;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM seeded_color) THEN INSERT INTO seeded_color VALUES(pg_temp.madadrang_insert('ProductAttribute','{"Name":"رنگ"}'::jsonb)); END IF; END $$;''')
    for p in manifest["products"]:
        sku = f"MRG-{p['id']}"
        doc = {"Name": p["name"], "ShortDescription": p["description"], "FullDescription": p["description"],
               "Sku": sku, "Published": True, "VisibleIndividually": True, "ProductTypeId": 5,
               "ProductTemplateId": 1, "Price": p["price"], "OldPrice": p["oldPrice"],
               "OrderMinimumQuantity": 1, "OrderMaximumQuantity": 20, "IsShipEnabled": True,
               "Weight": 0.2, "ManageInventoryMethodId": 2, "StockQuantity": 30,
               "DisableBuyButton": not p["available"], "ShowOnHomepage": p["id"] <= 4,
               "MarkAsNew": p["id"] > 4, "DisplayOrder": p["id"], "AllowCustomerReviews": True}
        sql.append(f"""DO $$ DECLARE pid integer; mapping integer; value_id integer; pic integer; color integer; xml text; BEGIN
-- SKU ownership is stable: skip existing or deliberately deleted products.
IF EXISTS (SELECT 1 FROM "Product" WHERE "Sku"='{sku}') THEN RETURN; END IF;
pid:=pg_temp.madadrang_insert('Product',{literal(doc)});
PERFORM pg_temp.madadrang_insert('Product_Category_Mapping',jsonb_build_object('ProductId',pid,'CategoryId',(SELECT id FROM seeded_categories WHERE name={literal(p['category'])}#>>'{{}}')));
PERFORM pg_temp.madadrang_insert('Product_Manufacturer_Mapping',jsonb_build_object('ProductId',pid,'ManufacturerId',(SELECT id FROM seeded_brands WHERE name={literal(p['brand'])}#>>'{{}}')));
mapping:=pg_temp.madadrang_insert('Product_ProductAttribute_Mapping',jsonb_build_object('ProductId',pid,'ProductAttributeId',(SELECT id FROM seeded_color),'IsRequired',true,'AttributeControlTypeId',40));
""")
        for order, option in enumerate(manifest["options"]):
            opt = {"Name": option["name"], "ColorSquaresRgb": option["color"], "PriceAdjustment": option["adjustment"],
                   "AttributeValueTypeId": 0, "Quantity": 1, "IsPreSelected": order == 0, "DisplayOrder": order + 1}
            combo = {"StockQuantity": 10 if p["available"] and option["available"] else 0,
                     "AllowOutOfStockOrders": False, "Sku": f"{sku}-{order + 1}"}
            sql.append(f"""value_id:=pg_temp.madadrang_insert('ProductAttributeValue',{literal(opt)}||jsonb_build_object('ProductAttributeMappingId',mapping));
xml:='<Attributes><ProductAttribute ID="'||mapping||'"><ProductAttributeValue><Value>'||value_id||'</Value></ProductAttributeValue></ProductAttribute></Attributes>';
PERFORM pg_temp.madadrang_insert('ProductAttributeCombination',{literal(combo)}||jsonb_build_object('ProductId',pid,'AttributesXml',xml));""")
        picture = {"MimeType": "image/jpeg", "SeoFilename": sku.lower(), "AltAttribute": p["name"], "TitleAttribute": p["name"], "IsNew": True}
        image_hex = base64.b64decode(p["imageBase64"]).hex()
        sql.append(f"""pic:=pg_temp.madadrang_insert('Picture',{literal(picture)});
PERFORM pg_temp.madadrang_insert('PictureBinary',jsonb_build_object('PictureId',pic,'BinaryData',decode('{image_hex}','hex')));
PERFORM pg_temp.madadrang_insert('Product_Picture_Mapping',jsonb_build_object('ProductId',pid,'PictureId',pic));
END $$;""")
    sql.append(r'''
-- Native URLs for samples are required even if presentation migration ran earlier.
INSERT INTO "UrlRecord" ("EntityName","Slug","EntityId","IsActive","LanguageId")
SELECT entity,slug,id,true,0 FROM (
 SELECT 'Category' entity,'madadrang-category-'||id slug,id FROM seeded_categories
 UNION ALL SELECT 'Product',lower("Sku"),"Id" FROM "Product" WHERE "Sku" IN ('MRG-1','MRG-2','MRG-3','MRG-4','MRG-5','MRG-6','MRG-7','MRG-8') AND NOT "Deleted"
 UNION ALL SELECT 'Manufacturer','madadrang-brand-'||id,id FROM seeded_brands
) r WHERE NOT EXISTS (SELECT 1 FROM "UrlRecord" u WHERE u."EntityName"=r.entity AND u."EntityId"=r.id AND u."LanguageId"=0 AND u."IsActive");
-- First explicit sample setup also prepares existing MRG catalogue presentation.
-- Repeated --seed-data calls retain merchant edits to existing rows.
DO $$ DECLARE sid integer := (SELECT id FROM deployment_target); fa integer; toman integer; category_row record;
BEGIN
 IF EXISTS (SELECT 1 FROM "GenericAttribute" WHERE "KeyGroup"='Store' AND "EntityId"=sid AND "Key"='Madadrang.SeedVersion' AND "Value"='1') THEN RETURN; END IF;
 SELECT "Id" INTO STRICT fa FROM "Language" WHERE "LanguageCulture"='fa-IR' AND "Published" AND "Rtl";
 SELECT "Id" INTO STRICT toman FROM "Currency" WHERE "CurrencyCode"='IRT' AND "Published" AND "Rate"=1;
 IF EXISTS (SELECT 1 FROM "Product" WHERE "Sku" NOT IN ('MRG-1','MRG-2','MRG-3','MRG-4','MRG-5','MRG-6','MRG-7','MRG-8') AND NOT "Deleted")
 AND EXISTS (SELECT 1 FROM "Setting" WHERE "Name"='currencysettings.primarystorecurrencyid' AND "StoreId"=0 AND "Value"<>toman::text) THEN
  RAISE EXCEPTION 'Existing merchant catalogue uses another primary currency; configure IRT before requesting demo seeding';
 END IF;
 UPDATE "Language" SET "DefaultCurrencyId"=toman WHERE "Id"=fa;
 UPDATE "Currency" SET "DisplayOrder"=-1 WHERE "Id"=toman AND "DisplayOrder">=-1;
 UPDATE "Setting" SET "Value"=toman::text WHERE "StoreId"=0 AND "Name" IN ('currencysettings.primarystorecurrencyid','currencysettings.primaryexchangeratecurrencyid');
 UPDATE "Customer" c SET "CurrencyId"=toman WHERE (c."CurrencyId" IS NULL OR c."CurrencyId"=(SELECT "Id" FROM "Currency" WHERE "CurrencyCode"='USD'))
   AND EXISTS (SELECT 1 FROM "Customer_CustomerRole_Mapping" m JOIN "CustomerRole" r ON r."Id"=m."CustomerRole_Id" WHERE m."Customer_Id"=c."Id" AND r."SystemName"='Guests')
   AND NOT EXISTS (SELECT 1 FROM "Customer_CustomerRole_Mapping" m JOIN "CustomerRole" r ON r."Id"=m."CustomerRole_Id" WHERE m."Customer_Id"=c."Id" AND r."SystemName" IN ('Registered','Administrators'));
 FOR category_row IN SELECT id,name FROM seeded_categories LOOP
  UPDATE "Category" SET "ShowOnHomepage"=true,"DisplayOrder"=array_position(ARRAY['دفتر و کاغذ','نوشت‌افزار','لوازم مدرسه','هنر و خلاقیت'],category_row.name)-1 WHERE "Id"=category_row.id;
  INSERT INTO "GenericAttribute" ("KeyGroup","Key","Value","EntityId","StoreId","CreatedOrUpdatedDateUTC")
  SELECT 'Category','Madadrang.TileStyle',(array_position(ARRAY['دفتر و کاغذ','نوشت‌افزار','لوازم مدرسه','هنر و خلاقیت'],category_row.name)-1)::text,category_row.id,0,now()
  WHERE NOT EXISTS (SELECT 1 FROM "GenericAttribute" WHERE "KeyGroup"='Category' AND "Key"='Madadrang.TileStyle' AND "EntityId"=category_row.id AND "StoreId"=0);
 END LOOP;
 UPDATE "Product" SET "ShowOnHomepage"=true,"DisplayOrder"=substring("Sku" from 5)::integer WHERE "Sku" IN ('MRG-1','MRG-2','MRG-3','MRG-4') AND NOT "Deleted";
 UPDATE "Product" SET "MarkAsNew"=true WHERE "Sku" IN ('MRG-5','MRG-6','MRG-7','MRG-8') AND NOT "Deleted";
 INSERT INTO "GenericAttribute" ("KeyGroup","Key","Value","EntityId","StoreId","CreatedOrUpdatedDateUTC")
 SELECT 'Store',v.key,c.id::text,sid,0,now() FROM (VALUES ('Madadrang.ArtCategoryId','هنر و خلاقیت'),('Madadrang.NotebookCategoryId','دفتر و کاغذ')) v(key,name) JOIN seeded_categories c ON c.name=v.name
 WHERE NOT EXISTS (SELECT 1 FROM "GenericAttribute" a WHERE a."KeyGroup"='Store' AND a."EntityId"=sid AND a."Key"=v.key AND a."StoreId"=0);
 -- If a normal deployment preceded the first seed, extend its owned menus once.
 INSERT INTO "MenuItem" ("MenuId","MenuItemTypeId","EntityId","TemplateId","DisplayOrder","SubjectToAcl","LimitedToStores","Published","ParentId")
 SELECT m."Id",1,c.id,0,array_position(ARRAY['دفتر و کاغذ','نوشت‌افزار','لوازم مدرسه','هنر و خلاقیت'],c.name),false,false,true,0
 FROM "Menu" m CROSS JOIN seeded_categories c WHERE m."CssClass" IN ('madadrang-main','madadrang-footer-shop') AND NOT m."Deleted"
 AND NOT EXISTS (SELECT 1 FROM "MenuItem" i WHERE i."MenuId"=m."Id" AND i."MenuItemTypeId"=1 AND i."EntityId"=c.id);
 INSERT INTO "GenericAttribute" ("KeyGroup","Key","Value","EntityId","StoreId","CreatedOrUpdatedDateUTC") VALUES ('Store','Madadrang.SeedVersion','1',sid,0,now());
END $$;
''')
    return "\n".join(sql) + "\n"


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Usage: generate-madadrang-seed.py MANIFEST OUTPUT.sql")
    manifest = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    Path(sys.argv[2]).write_text(generate(manifest), encoding="utf-8", newline="\n")
