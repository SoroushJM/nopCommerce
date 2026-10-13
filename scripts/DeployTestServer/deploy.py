"""Update exactly one existing test application; never recreate PostgreSQL or reseed."""
import argparse, hashlib, json, pathlib, re, shlex, subprocess, sys, tarfile, tempfile, time
import paramiko
sys.stdout.reconfigure(encoding='utf-8')
p=argparse.ArgumentParser(); p.add_argument('--theme',choices=['madadrang','boutique'],required=True); p.add_argument('--app',required=True); p.add_argument('--config',required=True); p.add_argument('--resume-release',help='Resume this previously uploaded release directory after a safe pre-update failure'); p.add_argument('--skip-http-verification',action='store_true',help='Use process/database metadata only; no HTTP requests'); a=p.parse_args()
c=json.loads(pathlib.Path(a.config).read_text(encoding='utf-8-sig')); root=c['remoteRoot']; app=pathlib.Path(a.app).resolve(); here=pathlib.Path(__file__).resolve().parent
brand=a.theme.title(); brand='Madadrang' if a.theme=='madadrang' else 'Boutique'; opposite='Boutique' if brand=='Madadrang' else 'Madadrang'
for required in ['Nop.Web.dll','Nop.Docker.Views.dll','libSkiaSharp.so','Microsoft.OpenApi.dll','Scalar.AspNetCore.dll','Swashbuckle.AspNetCore.Swagger.dll','Swashbuckle.AspNetCore.SwaggerGen.dll','Swashbuckle.AspNetCore.SwaggerUI.dll']:
 if not (app/required).is_file(): raise SystemExit('Missing Linux release dependency: '+required)
files=list(app.rglob('*'))
if not (app/'Themes'/brand).is_dir() or not any(x.is_file() for x in [app/('Storefront.UI.'+brand+'.dll'), app/'Plugins/Misc.PersianStorefront'/('Storefront.UI.'+brand+'.dll')]) or not list((app/'Plugins/Misc.PersianStorefront/Content/Client/wwwroot/_framework').glob('Storefront.UI.'+brand+'*.wasm')): raise SystemExit('Selected native theme, UI assembly or browser module is missing')
if any(x.is_file() and (x.name.startswith('Storefront.UI.'+opposite) or '/Storefront.UI.'+opposite+'/' in x.as_posix() or '/Themes/'+opposite+'/' in x.as_posix()) for x in files): raise SystemExit('Package contains opposite brand')
package_manifest=app.parent/'package.json'
package_revision=json.loads(package_manifest.read_text(encoding='utf-8-sig')).get('SourceCommit') if package_manifest.is_file() else None
if package_revision and not re.fullmatch('[a-f0-9]{40}',package_revision): raise SystemExit('Invalid published source revision')
stamp=time.strftime('%Y%m%dT%H%M%S',time.gmtime()); rev=package_revision[:12] if package_revision else subprocess.check_output(['git','-C',str(here.parent.parent),'rev-parse','--short=12','HEAD'],text=True).strip(); tag=f'nop-dual:{a.theme}-{rev}-{stamp}'; remote=f'{root}/releases/{stamp}-{a.theme}'
if a.resume_release:
 if not re.fullmatch(r'\d{8}T\d{6}-'+a.theme,a.resume_release): raise SystemExit('Invalid scoped release directory')
 remote=root+'/releases/'+a.resume_release
password=re.search(r'^Password: (.+)$',pathlib.Path(c['handoffPath']).read_text(encoding='utf-8-sig'),re.M).group(1).strip()
client=paramiko.SSHClient(); client.load_system_host_keys(); client.load_host_keys(c['knownHostsPath']); client.set_missing_host_key_policy(paramiko.RejectPolicy()); client.connect(c['host'],username=c['username'],password=password,look_for_keys=False,allow_agent=False,timeout=20)
# Persist only an already trusted key; never accept an unknown server automatically.
client.get_host_keys().add(c['host'],client.get_transport().get_remote_server_key().get_name(),client.get_transport().get_remote_server_key()); client.save_host_keys(c['knownHostsPath'])
def run(script):
 stdin,out,err=client.exec_command('bash -se 2>&1',timeout=1200); stdin.write(script); stdin.channel.shutdown_write()
 for line in out: print(line.replace(password,'[redacted]'),end='',flush=True)
 error=err.read().decode('utf-8','replace').replace(password,'[redacted]')
 if error: print(error,file=sys.stderr)
 if out.channel.recv_exit_status(): raise RuntimeError('Scoped deployment command failed')
q=shlex.quote
try:
 with tempfile.TemporaryDirectory(prefix='nop-server-deploy-') as td:
  archive=pathlib.Path(td)/'app.tar.gz'
  unpacked_bytes=0
  with tarfile.open(archive,'w:gz',compresslevel=3) as tar:
   for item in files:
    if not item.is_file(): continue
    rel=item.relative_to(app); parts=rel.parts
    if item.suffix.lower() in ['.pdb','.pfx','.key','.pem'] or item.name.startswith('.env') or 'logs' in parts or 'DataProtectionKeys' in parts or 'ref' in parts or (parts[0]=='App_Data' and item.suffix=='.json'): continue
    unpacked_bytes+=item.stat().st_size
    tar.add(item,arcname='app/'+rel.as_posix(),recursive=False)
  # Reserve archive, extracted files, BuildKit input/COPY layers and bounded headroom.
  # Fail before upload or data changes; capacity maintenance is explicitly scoped.
  required_free=3*unpacked_bytes+archive.stat().st_size+256*1024*1024
  run(f'''python3 - {q(root)} {required_free} <<'PY'
import pathlib,shutil,sys
target=pathlib.Path(sys.argv[1]); required=int(sys.argv[2]); free=shutil.disk_usage(target).free
print('Deployment disk preflight: free bytes',free,'required bytes',required,flush=True)
if free<required: raise SystemExit('Insufficient deployment capacity. Preserve application/database volumes and use verified task-owned release/cache maintenance before retrying; no upload or service update was started.')
PY
''')
  digest=hashlib.sha256(archive.read_bytes()).hexdigest(); run(f'umask 077\nmkdir -p {q(remote)}\n')
  with client.open_sftp() as s:
   uploads=[(here/'Dockerfile',remote+'/Dockerfile'),(here/'native-admin.py',root+'/native-admin.py'),(here/'verify-native-http.py',root+'/verify-native-http.py'),(here/'ensure-relative-media.sql',root+'/ensure-relative-media.sql'),(pathlib.Path(c['adminCredentialsPath']),root+'/admin-credentials.json')]
   if not a.resume_release: uploads.insert(0,(archive,remote+'/app.tar.gz'))
   for source,dest in uploads: s.put(str(source),dest); s.chmod(dest,0o600)
  if a.resume_release:
   _,out,err=client.exec_command('sha256sum '+q(remote+'/app.tar.gz')); digest=out.read().decode().split()[0]
   if out.channel.recv_exit_status() or not re.fullmatch('[a-f0-9]{64}',digest): raise RuntimeError('Uploaded release hash unavailable')
  script=f'''umask 077
cd {q(root)}
exec 9>.deploy.lock
flock -n 9 || {{ echo 'Another deployment is active'; exit 1; }}
python3 - {q(a.theme)} {q(tag)} {q(remote)} {q(digest)} <<'PY'
import sys,json,subprocess,pathlib,hashlib,shutil,tarfile
theme,tag,release,digest=sys.argv[1:]; root=pathlib.Path.cwd()
def inspect(service):
 cid=subprocess.check_output(['docker','compose','ps','-q',service],text=True).strip()
 x=json.loads(subprocess.check_output(['docker','inspect',cid],text=True))[0]
 return {{'Id':x['Id'],'StartedAt':x['State']['StartedAt'],'Image':x['Config']['Image']}}
before={{s:inspect(s) for s in ['madadrang','boutique','database']}}
assert hashlib.sha256((pathlib.Path(release)/'app.tar.gz').read_bytes()).hexdigest()==digest
backup=root/'backups'/pathlib.Path(release).name; backup.mkdir(parents=True,exist_ok=True)
with (backup/(theme+'.sql')).open('wb') as f: subprocess.run(['docker','exec',before['database']['Id'],'pg_dump','-U','deployment_bootstrap','-d','nop_'+theme],stdout=f,check=True)
with (backup/'App_Data.tar.gz').open('wb') as f: subprocess.run(['docker','exec',before[theme]['Id'],'tar','-czf','-','-C','/app','App_Data'],stdout=f,check=True)
subprocess.run(['docker','cp',str(root/'ensure-relative-media.sql'),before['database']['Id']+':/tmp/ensure-relative-media.sql'],check=True)
subprocess.run(['docker','exec',before['database']['Id'],'psql','-X','-v','ON_ERROR_STOP=1','-U','deployment_bootstrap','-d','nop_'+theme,'-f','/tmp/ensure-relative-media.sql'],check=True)
subprocess.run(['tar','-xzf','app.tar.gz'],cwd=release,check=True)
brand='Madadrang' if theme=='madadrang' else 'Boutique'; css='storefront.css' if theme=='madadrang' else 'boutique.css'
content=pathlib.Path(release)/'app/Plugins/Misc.PersianStorefront/Content'; canonical=content/('Storefront.UI.'+brand)/css
for target in [pathlib.Path(release)/'app/wwwroot/_content'/('Storefront.UI.'+brand)/css,content/'Client/wwwroot/_content'/('Storefront.UI.'+brand)/css]:
 if target.exists(): shutil.copyfile(canonical,target)
subprocess.run(['docker','build','-t',tag,'.'],cwd=release,check=True)
override=root/'images.override.yml'
images={{s:before[s]['Image'] for s in ['madadrang','boutique']}}; images[theme]=tag
override.write_text('services:\\n'+''.join('  '+s+':\\n    image: '+images[s]+'\\n    environment:\\n      NOP_COOKIE_PREFIX: .Nop.'+s+'\\n' for s in images))
subprocess.run(['docker','compose','-f','compose.yml','-f',str(override),'up','-d','--no-build','--no-deps','--force-recreate',theme],check=True)
native_icons=pathlib.Path(release)/'app/wwwroot/icons/icons_0'
if native_icons.is_dir():
 icons_archive=pathlib.Path(release)/'native-default-icons.tar.gz'
 with tarfile.open(icons_archive,'w:gz') as tar: tar.add(native_icons,arcname='icons/icons_0')
 app_id=inspect(theme)['Id']
 subprocess.run(['docker','cp',str(icons_archive),app_id+':/tmp/native-default-icons.tar.gz'],check=True)
 subprocess.run(['docker','exec',app_id,'tar','--skip-old-files','-xzf','/tmp/native-default-icons.tar.gz','-C','/app/wwwroot'],check=True)
(pathlib.Path(release)/'before.json').write_text(json.dumps(before,indent=2))
PY
python3 native-admin.py {q(a.theme)}
if [ {q('skip' if a.skip_http_verification else 'run')} = run ]; then
python3 - {q(a.theme)} <<'PY'
import sys,time,urllib.request
port='9011' if sys.argv[1]=='madadrang' else '9012'; public='http://80.75.5.82:'+port
for attempt in range(60):
 try:
  with urllib.request.build_opener(urllib.request.ProxyHandler({{}})).open(public+'/',timeout=10) as r:
   if r.status==200: break
 except Exception: time.sleep(2)
else: raise SystemExit('Application readiness failed')
PY
python3 verify-native-http.py {q(a.theme)}
else
echo 'HTTP readiness and login checks omitted by request'
docker compose logs --tail=12 {q(a.theme)}
fi
python3 - {q(remote)} {q(a.theme)} <<'PY'
import sys,pathlib,json,subprocess
release=pathlib.Path(sys.argv[1]); theme=sys.argv[2]; before=json.loads((release/'before.json').read_text())
for service in ['database','boutique' if theme=='madadrang' else 'madadrang']:
 cid=subprocess.check_output(['docker','compose','ps','-q',service],text=True).strip(); x=json.loads(subprocess.check_output(['docker','inspect',cid],text=True))[0]
 assert (x['Id'],x['State']['StartedAt'])==(before[service]['Id'],before[service]['StartedAt']),service+' changed unexpectedly'
 print('Unchanged service verified:',service)
print('Scoped deployment verified:',theme)
PY
'''
  run(script)
finally: client.close()


