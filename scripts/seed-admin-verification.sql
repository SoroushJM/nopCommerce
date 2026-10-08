-- Explicit development fixture for invoice/report download verification.
-- No checkout, payment, stock mutation, email or SMS is executed.
DO $fixture$
DECLARE
    field record;
    names text := '';
    vals text := '';
    item_names text := '';
    item_vals text := '';
    order_id integer;
    product_id integer;
    price numeric;
    value_sql text;
BEGIN
    IF current_database() <> 'nop_stationery_dev' THEN
        RAISE EXCEPTION 'Verification fixture is restricted to nop_stationery_dev';
    END IF;
    SELECT "Id" INTO order_id FROM "Order" WHERE "CustomOrderNumber" = 'DEV-FA-0001';
    IF order_id IS NOT NULL THEN RETURN; END IF;
    SELECT "Id", "Price" INTO product_id, price FROM "Product" WHERE "Sku" = 'MRG-1' LIMIT 1;
    IF product_id IS NULL THEN
        SELECT "Id", "Price" INTO product_id, price FROM "Product" WHERE "Sku" LIKE 'MRG-%' ORDER BY "Id" LIMIT 1;
    END IF;
    IF product_id IS NULL THEN RAISE EXCEPTION 'Seeded stationery product required'; END IF;
    FOR field IN SELECT column_name, data_type FROM information_schema.columns
        WHERE table_schema='public' AND table_name='Order' AND is_nullable='NO' AND column_name<>'Id'
        ORDER BY ordinal_position LOOP
        value_sql := CASE field.data_type WHEN 'boolean' THEN 'false' WHEN 'uuid' THEN 'gen_random_uuid()'
            WHEN 'timestamp without time zone' THEN '''2026-10-08 09:30:00''::timestamp' ELSE '0' END;
        value_sql := CASE field.column_name
            WHEN 'CustomOrderNumber' THEN '''DEV-FA-0001'''
            WHEN 'BillingAddressId' THEN '(SELECT min("Id") FROM "Address")'
            WHEN 'CustomerId' THEN '(SELECT "Id" FROM "Customer" WHERE "Email"=''admin@madadrang.local'')'
            WHEN 'StoreId' THEN '(SELECT min("Id") FROM "Store")'
            WHEN 'CustomerLanguageId' THEN '(SELECT "Id" FROM "Language" WHERE "LanguageCulture"=''fa-IR'')'
            WHEN 'OrderStatusId' THEN '10' WHEN 'PaymentStatusId' THEN '10' WHEN 'ShippingStatusId' THEN '10'
            WHEN 'CurrencyRate' THEN '1'
            WHEN 'OrderSubtotalInclTax' THEN price::text WHEN 'OrderSubtotalExclTax' THEN price::text
            WHEN 'OrderTotal' THEN price::text ELSE value_sql END;
        names := names || CASE WHEN names='' THEN '' ELSE ',' END || quote_ident(field.column_name);
        vals := vals || CASE WHEN vals='' THEN '' ELSE ',' END || value_sql;
    END LOOP;
    EXECUTE 'INSERT INTO "Order" ('||names||',"CustomerCurrencyCode","TaxRates") VALUES ('||vals||',''IRT'',''0:0;'') RETURNING "Id"' INTO order_id;
    FOR field IN SELECT column_name, data_type FROM information_schema.columns
        WHERE table_schema='public' AND table_name='OrderItem' AND is_nullable='NO' AND column_name<>'Id'
        ORDER BY ordinal_position LOOP
        value_sql := CASE field.data_type WHEN 'boolean' THEN 'false' WHEN 'uuid' THEN 'gen_random_uuid()' ELSE '0' END;
        value_sql := CASE field.column_name
            WHEN 'OrderId' THEN order_id::text WHEN 'ProductId' THEN product_id::text WHEN 'Quantity' THEN '1'
            WHEN 'UnitPriceInclTax' THEN price::text WHEN 'UnitPriceExclTax' THEN price::text
            WHEN 'PriceInclTax' THEN price::text WHEN 'PriceExclTax' THEN price::text ELSE value_sql END;
        item_names := item_names || CASE WHEN item_names='' THEN '' ELSE ',' END || quote_ident(field.column_name);
        item_vals := item_vals || CASE WHEN item_vals='' THEN '' ELSE ',' END || value_sql;
    END LOOP;
    EXECUTE 'INSERT INTO "OrderItem" ('||item_names||') VALUES ('||item_vals||')';
    RAISE NOTICE 'Development verification order DEV-FA-0001 created';
END $fixture$;

-- Add an unshipped packaging-slip fixture only to the explicit development order.
-- No service events, inventory movements, payments or customer notifications run.
DO $packaging_fixture$
DECLARE
    order_id integer;
    order_item_id integer;
    shipment_id integer;
BEGIN
    IF current_database() <> 'nop_stationery_dev' THEN
        RAISE EXCEPTION 'Packaging verification fixture is restricted to nop_stationery_dev';
    END IF;
    SELECT "Id" INTO order_id FROM "Order" WHERE "CustomOrderNumber" = 'DEV-FA-0001';
    IF order_id IS NULL THEN RAISE EXCEPTION 'DEV-FA-0001 verification order is required'; END IF;
    SELECT "Id" INTO order_item_id FROM "OrderItem" WHERE "OrderId" = order_id ORDER BY "Id" LIMIT 1;
    IF order_item_id IS NULL THEN RAISE EXCEPTION 'Verification order requires an item'; END IF;
    SELECT "Id" INTO shipment_id FROM "Shipment"
        WHERE "OrderId" = order_id AND "TrackingNumber" = 'DEV-FA-PDF-0001' LIMIT 1;
    IF shipment_id IS NULL THEN
        INSERT INTO "Shipment" ("OrderId", "TrackingNumber", "AdminComment", "CreatedOnUtc")
        VALUES (order_id, 'DEV-FA-PDF-0001', 'Development packaging PDF verification only', '2026-10-08 09:30:00')
        RETURNING "Id" INTO shipment_id;
    END IF;
    INSERT INTO "ShipmentItem" ("ShipmentId", "OrderItemId", "Quantity", "WarehouseId")
    SELECT shipment_id, order_item_id, 1, 0
    WHERE NOT EXISTS (SELECT 1 FROM "ShipmentItem" WHERE "ShipmentId" = shipment_id AND "OrderItemId" = order_item_id);
    RAISE NOTICE 'Development packaging verification shipment % ready (unshipped)', shipment_id;
END $packaging_fixture$;
