SELECT current_database();
SELECT table_name,column_name,data_type,is_nullable,column_default FROM information_schema.columns
WHERE table_name IN ('Product','Category','ProductAttribute','ProductAttributeMapping','ProductAttributeValue','ProductAttributeCombination','Picture','PictureBinary','Product_Picture_Mapping','Product_Category_Mapping','UrlRecord','Setting') ORDER BY table_name,ordinal_position;
SELECT "Id","Name","Sku","Published","Deleted","Price" FROM "Product" WHERE NOT "Deleted" ORDER BY "Id";
SELECT "Id","Name","Published","Deleted" FROM "Category" WHERE NOT "Deleted" ORDER BY "Id";
SELECT "Name","Value" FROM "Setting" WHERE "Name" LIKE '%media.images%' OR "Name" LIKE '%boutique%';
SELECT tablename,columnname FROM pg_catalog.pg_tables CROSS JOIN LATERAL (SELECT column_name columnname FROM information_schema.columns WHERE table_name=tablename AND column_name='Id' AND is_identity='YES') c WHERE schemaname='public';
SELECT pg_get_serial_sequence('"Product"','Id'), pg_get_serial_sequence('"Picture"','Id');
SELECT "Name","Value" FROM "Setting" WHERE lower("Name") LIKE '%images.storeindb%' OR lower("Name") LIKE '%boutique%';
