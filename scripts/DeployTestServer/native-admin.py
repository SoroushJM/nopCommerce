"""Maintain only the explicitly requested admin using nopCommerce's native salted hash."""
import pathlib,json,subprocess,hashlib,secrets,base64,sys
root=pathlib.Path('/home/web/nop-dual-store-20261012')
theme=sys.argv[1]
if theme not in ['madadrang','boutique']: raise SystemExit('Unknown service')
db='nop_'+theme
credential=json.loads((root/'admin-credentials.json').read_text())[theme]
cid=subprocess.check_output(['docker','compose','-f',str(root/'compose.yml'),'ps','-q','database'],cwd=root,text=True).strip()
def psql(sql):
 result=subprocess.run(['docker','exec','-i',cid,'psql','-X','-v','ON_ERROR_STOP=1','-tA','-U','deployment_bootstrap','-d',db],input=sql,text=True,capture_output=True)
 if result.returncode: raise RuntimeError('Native admin database maintenance failed; inspect scoped database state')
 return result.stdout.strip()
def literal(s): return "'"+s.replace("'","''")+"'"
email=literal(credential['email'])
expected_url='http://80.75.5.82:'+('9011' if theme=='madadrang' else '9012')+'/'
if psql('SELECT "Url" FROM "Store" WHERE "Id"=1;')!=expected_url: raise SystemExit('Wrong store address')
algorithm=psql("SELECT \"Value\" FROM \"Setting\" WHERE lower(\"Name\")='customersettings.hashedpasswordformat' AND \"StoreId\"=0;").strip().lower()
if algorithm not in ['sha1','sha256','sha384','sha512']: raise SystemExit('Unknown native hash setting; refusing to guess')
previous=psql(f'''SELECT row_to_json(p) FROM "CustomerPassword" p JOIN "Customer" c ON c."Id"=p."CustomerId" WHERE lower(c."Email")=lower({email}) AND NOT c."Deleted" AND EXISTS(SELECT 1 FROM "Customer_CustomerRole_Mapping" m JOIN "CustomerRole" r ON r."Id"=m."CustomerRole_Id" WHERE m."Customer_Id"=c."Id" AND r."SystemName"='Administrators' AND r."Active") ORDER BY p."CreatedOnUtc" DESC,p."Id" DESC LIMIT 1;''')
if not previous: raise SystemExit('Designated installed administrator is missing; refusing to modify another account')
old=json.loads(previous)
psql(f'''BEGIN;
DO $$ DECLARE customer_id integer; registered_role integer; BEGIN
 SELECT "Id" INTO STRICT customer_id FROM "Customer" WHERE lower("Email")=lower({email}) AND NOT "Deleted";
 SELECT "Id" INTO STRICT registered_role FROM "CustomerRole" WHERE "SystemName"='Registered' AND "Active";
 IF NOT EXISTS(SELECT 1 FROM "Customer_CustomerRole_Mapping" WHERE "Customer_Id"=customer_id AND "CustomerRole_Id"=registered_role) THEN INSERT INTO "Customer_CustomerRole_Mapping"("Customer_Id","CustomerRole_Id") VALUES(customer_id,registered_role); END IF;
 UPDATE "Customer" SET "Active"=true,"RequireReLogin"=false,"MustChangePassword"=false,"FailedLoginAttempts"=0,"CannotLoginUntilDateUtc"=NULL WHERE "Id"=customer_id;
END $$; COMMIT;''')
if old['PasswordFormatId']==1 and hashlib.new(algorithm,(credential['password']+old['PasswordSalt']).encode()).hexdigest().upper()==old['Password']:
 print('Requested native admin credentials already match:',theme); sys.exit(0)
# Native EncryptionService.CreatePasswordHash: uppercase hex hash of UTF8(password + Base64(random5bytes)).
salt=base64.b64encode(secrets.token_bytes(5)).decode()
digest=hashlib.new(algorithm,(credential['password']+salt).encode('utf-8')).hexdigest().upper()
sql=f'''BEGIN;
DO $$ DECLARE customer_id integer; old_password record; BEGIN
 IF current_database()<>{literal(db)} OR NOT EXISTS(SELECT 1 FROM "Store" WHERE "Id"=1 AND "Url"={literal(expected_url)}) THEN RAISE EXCEPTION 'Wrong deployment'; END IF;
 SELECT "Id" INTO STRICT customer_id FROM "Customer" WHERE lower("Email")=lower({email}) AND NOT "Deleted";
 IF NOT EXISTS(SELECT 1 FROM "Customer_CustomerRole_Mapping" m JOIN "CustomerRole" r ON r."Id"=m."CustomerRole_Id" WHERE m."Customer_Id"=customer_id AND r."SystemName"='Administrators' AND r."Active") THEN RAISE EXCEPTION 'Specified account is not the installed administrator'; END IF;
 INSERT INTO "CustomerPassword"("CustomerId","Password","PasswordFormatId","PasswordSalt","CreatedOnUtc") VALUES(customer_id,{literal(digest)},1,{literal(salt)},now());
 UPDATE "Customer" SET "Active"=true,"RequireReLogin"=false,"MustChangePassword"=false,"FailedLoginAttempts"=0,"CannotLoginUntilDateUtc"=NULL WHERE "Id"=customer_id;
END $$;
COMMIT;'''
psql(sql)
print('Native salted administrator credential applied:',theme)

