"""Provision only the requested admin using nopCommerce's configured salted hash.

Matches EncryptionService.CreatePasswordHash and CreateSaltKey(5); no HTTP calls.
"""
import argparse, base64, hashlib, json, os, pathlib, secrets, subprocess, uuid
p=argparse.ArgumentParser()
p.add_argument('theme',choices=['madadrang','boutique'])
p.add_argument('--local-app'); p.add_argument('--credentials'); a=p.parse_args()
root=pathlib.Path('/home/web/nop-dual-store-20261012')
def literal(s): return "'"+s.replace("'","''")+"'"
if a.local_app:
 config=json.loads((pathlib.Path(a.local_app)/'App_Data/appsettings.json').read_text(encoding='utf-8-sig'))['ConnectionStrings']
 if config['DataProvider'].lower()!='postgresql': raise SystemExit('Configured provider must be PostgreSQL')
 connection=dict(part.split('=',1) for part in config['ConnectionString'].split(';') if '=' in part)
 connection={key.strip().lower():value.strip() for key,value in connection.items()}
 db=connection['database']; expected='nop_stationery_dev' if a.theme=='madadrang' else 'nop_themeqa_boutique_20261012185304'
 if db!=expected or connection.get('host','').lower() not in ['localhost','127.0.0.1']: raise SystemExit('Refusing database outside designated local storefront')
 private=json.loads(pathlib.Path(a.credentials).read_text(encoding='utf-8-sig'))
 credential={'email':private['AdminEmail'],'password':private['AdminPassword']}
 command=['psql','-h',connection['host'],'-p',connection.get('port','5432'),'-U',connection.get('username',connection.get('user id','postgres')),'-d',db]
 environment=dict(os.environ,PGPASSWORD=connection.get('password',''))
else:
 db='nop_'+a.theme
 credential=json.loads((root/'admin-credentials.json').read_text())[a.theme]
 cid=subprocess.check_output(['docker','compose','-f',str(root/'compose.yml'),'ps','-q','database'],cwd=root,text=True).strip()
 command=['docker','exec','-i',cid,'psql','-U','deployment_bootstrap','-d',db]; environment=None
def psql(sql):
 result=subprocess.run(command+['-X','-v','ON_ERROR_STOP=1','-tA'],input=sql,text=True,capture_output=True,env=environment,encoding='utf-8')
 if result.returncode: raise RuntimeError('Scoped native admin provisioning failed; credentials and hash context are not printed')
 return result.stdout.strip()
if not a.local_app:
 expected_url='http://80.75.5.82:'+('9011' if a.theme=='madadrang' else '9012')+'/'
 if psql('SELECT "Url" FROM "Store" WHERE "Id"=1;')!=expected_url: raise SystemExit('Wrong remote store address')
email=literal(credential['email'])
algorithm=psql("SELECT \"Value\" FROM \"Setting\" WHERE lower(\"Name\")='customersettings.hashedpasswordformat' AND \"StoreId\"=0;").lower()
if algorithm not in ['sha1','sha256','sha384','sha512']: raise SystemExit('Unknown native hash algorithm; refusing to guess')
psql(f'''BEGIN;
SELECT pg_advisory_xact_lock(713013);
DO $$ DECLARE customer_id integer; role_id integer; matched integer; BEGIN
 IF current_database()<>{literal(db)} THEN RAISE EXCEPTION 'Wrong database'; END IF;
 SELECT count(*) INTO matched FROM "Customer" WHERE lower("Email")=lower({email});
 IF matched>1 THEN RAISE EXCEPTION 'Duplicate requested account'; END IF;
 IF matched=0 THEN
  INSERT INTO "Customer"("CustomerGuid","Email","Username","CountryId","StateProvinceId","PhoneSmsVerified","VatNumberStatusId","IsTaxExempt","AffiliateId","VendorId","HasShoppingCartItems","RequireReLogin","FailedLoginAttempts","Active","Deleted","IsSystemAccount","CreatedOnUtc","LastActivityDateUtc","RegisteredInStoreId","MustChangePassword")
  VALUES({literal(str(uuid.uuid4()))}::uuid,{email},{email},0,0,false,0,false,0,0,false,false,0,true,false,false,now() AT TIME ZONE 'UTC',now() AT TIME ZONE 'UTC',1,false);
 END IF;
 SELECT "Id" INTO STRICT customer_id FROM "Customer" WHERE lower("Email")=lower({email});
 IF EXISTS(SELECT 1 FROM "Customer" WHERE "Id"=customer_id AND "IsSystemAccount") THEN RAISE EXCEPTION 'Cannot provision system account'; END IF;
 FOR role_id IN SELECT "Id" FROM "CustomerRole" WHERE "SystemName" IN ('Registered','Administrators') AND "Active" LOOP
  IF NOT EXISTS(SELECT 1 FROM "Customer_CustomerRole_Mapping" WHERE "Customer_Id"=customer_id AND "CustomerRole_Id"=role_id) THEN
   INSERT INTO "Customer_CustomerRole_Mapping"("Customer_Id","CustomerRole_Id") VALUES(customer_id,role_id);
  END IF;
 END LOOP;
 IF (SELECT count(*) FROM "CustomerRole" WHERE "SystemName" IN ('Registered','Administrators') AND "Active")<>2 THEN RAISE EXCEPTION 'Native roles missing'; END IF;
 DELETE FROM "Customer_CustomerRole_Mapping" WHERE "Customer_Id"=customer_id AND "CustomerRole_Id" IN (SELECT "Id" FROM "CustomerRole" WHERE "SystemName"='Guests');
 UPDATE "Customer" SET "Active"=true,"Deleted"=false,"RequireReLogin"=false,"MustChangePassword"=false,"FailedLoginAttempts"=0,"CannotLoginUntilDateUtc"=NULL WHERE "Id"=customer_id;
END $$; COMMIT;''')
previous=psql(f'''SELECT row_to_json(p) FROM "CustomerPassword" p JOIN "Customer" c ON c."Id"=p."CustomerId" WHERE lower(c."Email")=lower({email}) ORDER BY p."CreatedOnUtc" DESC,p."Id" DESC LIMIT 1;''')
old=json.loads(previous) if previous else None
matches=old and old['PasswordFormatId']==1 and hashlib.new(algorithm,(credential['password']+(old['PasswordSalt'] or '')).encode('utf-8')).hexdigest().upper()==old['Password']
if not matches:
 salt=base64.b64encode(secrets.token_bytes(5)).decode()
 digest=hashlib.new(algorithm,(credential['password']+salt).encode('utf-8')).hexdigest().upper()
 psql(f'''INSERT INTO "CustomerPassword"("CustomerId","Password","PasswordFormatId","PasswordSalt","CreatedOnUtc") SELECT "Id",{literal(digest)},1,{literal(salt)},now() AT TIME ZONE 'UTC' FROM "Customer" WHERE lower("Email")=lower({email}) AND NOT "Deleted";''')
metadata=psql(f'''SELECT json_build_object('email',c."Email",'active',c."Active",'deleted',c."Deleted",'roles',(SELECT string_agg(r."SystemName",', ' ORDER BY r."SystemName") FROM "Customer_CustomerRole_Mapping" m JOIN "CustomerRole" r ON r."Id"=m."CustomerRole_Id" WHERE m."Customer_Id"=c."Id" AND r."Active")) FROM "Customer" c WHERE lower(c."Email")=lower({email});''')
print('Native administrator provisioned:',a.theme,'database:',db,metadata)
print('Configured salted password:', 'already matched' if matches else 'applied')
