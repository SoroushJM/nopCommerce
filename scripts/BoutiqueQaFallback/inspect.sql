\set ON_ERROR_STOP on
BEGIN READ ONLY;
DO $$ BEGIN
 IF current_database() <> 'nop_themeqa_boutique_20261012185304' THEN
  RAISE EXCEPTION 'Refusing any database other than isolated Boutique QA';
 END IF;
END $$;
SELECT jsonb_pretty(jsonb_agg(to_jsonb(p) ORDER BY p."Id")) FROM "Product" p WHERE "Id" IN (2,3);
SELECT to_jsonb(m) FROM "Product_ProductAttribute_Mapping" m WHERE "ProductId" IN (2,3);
SELECT to_jsonb(v) FROM "ProductAttributeValue" v JOIN "Product_ProductAttribute_Mapping" m ON m."Id"=v."ProductAttributeMappingId" WHERE m."ProductId" IN (2,3);
COMMIT;
