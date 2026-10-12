SELECT current_database();
SELECT table_name,column_name,data_type,is_nullable FROM information_schema.columns WHERE table_name IN ('Product_ProductAttribute_Mapping','Menu','MenuItem','Store','Language','Currency','ProductAttributeValuePicture') ORDER BY table_name,ordinal_position;
SELECT "Id","Name","DisplayOrder","Published","LanguageCulture","Rtl" FROM "Language";
SELECT "Id","Name","DefaultLanguageId" FROM "Store";
SELECT "Id","Name","CurrencyCode","Rate","Published" FROM "Currency";
SELECT "Name","Value","StoreId" FROM "Setting" WHERE lower("Name") LIKE '%currencysettings.primary%' OR lower("Name") LIKE '%boutique%';
SELECT row_to_json(m) FROM "Menu" m;
SELECT row_to_json(m) FROM "MenuItem" m LIMIT 30;
SELECT row_to_json(p) FROM "Product" p WHERE "Id"=1;
