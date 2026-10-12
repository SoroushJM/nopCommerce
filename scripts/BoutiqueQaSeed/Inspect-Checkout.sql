BEGIN READ ONLY;
DO $$ BEGIN
 IF current_database() <> 'nop_themeqa_boutique_20261012185304' THEN RAISE EXCEPTION 'Wrong QA database'; END IF;
END $$;
SELECT "Name", "Value", "StoreId" FROM "Setting"
WHERE lower("Name") IN ('paymentsettings.activepaymentmethodsystemnames','shippingsettings.activeshippingratecomputationmethodsystemnames','ordersettings.onepagecheckoutenabled','ordersettings.anonymouscheckoutallowed','ordersettings.checkoutdisabled','ordersettings.termsofserviceonshoppingcartpage','ordersettings.termsofserviceonorderconfirmpage','shippingsettings.bypassshippingmethodselectionifonlyone','paymentsettings.bypasspaymentmethodselectionifonlyone','shoppingcartsettings.minishoppingcartenabled','rewardpointssettings.enabled')
 OR lower("Name") LIKE 'fixedbyweightbytotalsettings.%'
 OR lower("Name") LIKE 'checkmoneyorderpaymentsettings.%' AND lower("Name") NOT LIKE '%description%'
ORDER BY "Name";
SELECT "Id", "Name", "Type", "Enabled" FROM "ScheduleTask" ORDER BY "Id";
SELECT count(*) AS active_email_templates FROM "MessageTemplate" WHERE "IsActive";
SELECT count(*) AS pending_email_count FROM "QueuedEmail" WHERE "SentOnUtc" IS NULL;
SELECT "Id", "Name", "DisplayOrder" FROM "ShippingMethod" ORDER BY "Id";
SELECT "Id", "Name", "TwoLetterIsoCode", "AllowsBilling", "AllowsShipping", "Published" FROM "Country"
 WHERE "TwoLetterIsoCode" IN ('IR','US');
SELECT "Id", "Name", "CountryId" FROM "StateProvince" WHERE "CountryId" IN (SELECT "Id" FROM "Country" WHERE "TwoLetterIsoCode" = 'IR') LIMIT 10;
SELECT "Id", "Sku", "Published", "IsShipEnabled", "Weight", "ManageInventoryMethodId" FROM "Product" WHERE "Id"=3583;
COMMIT;
