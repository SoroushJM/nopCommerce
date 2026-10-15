"""Transfer one locally built Docker image and replace only its existing storefront.

No application HTTP calls, database commands, account provisioning or broad cleanup.
The remote worker holds the existing deployment flock through upload and retirement.
"""
import argparse
import hashlib
import json
import pathlib
import re
import shlex
import sys
import tarfile

import paramiko


REMOTE_WORKER = r'''
import datetime,fcntl,hashlib,json,os,pathlib,re,shutil,stat,subprocess,sys,time,uuid

class DeploymentError(Exception): pass
options=json.loads(sys.argv[1]);theme=options['theme'];other='boutique' if theme=='madadrang' else 'madadrang'
root=pathlib.Path(options['root']).resolve();release=None;receipt={};before=None;original=None;switched=False;committed=False;exit_code=0;message=None;rollback=None

def emit(kind,**data):
 print(json.dumps(dict(type=kind,**data)),flush=True)

def command(args,required=True,timeout=30):
 try:result=subprocess.run(args,cwd=root,capture_output=True,text=True,timeout=timeout)
 except subprocess.TimeoutExpired:raise DeploymentError(args[0]+' '+args[1]+' exceeded its bounded timeout.')
 if required and result.returncode:
  # Do not echo command output: compose/plugin errors can contain private settings.
  raise DeploymentError(args[0]+' '+args[1]+' failed (exit '+str(result.returncode)+').')
 return result

def compose(extra=None):
 args=['docker','compose','-f',str(root/'compose.yml'),'-f',str(root/'images.override.yml')]
 if extra:args+=['-f',str(extra)]
 return args

def inspect(service):
 cid=command(compose()+['ps','--all','-q',service]).stdout.strip().splitlines()
 if len(cid)!=1:raise DeploymentError('Expected one existing container for '+service+'.')
 x=json.loads(command(['docker','inspect',cid[0]]).stdout)[0]
 return dict(Id=x['Id'],ImageId=x['Image'],Image=x['Config']['Image'],Running=x['State']['Running'],
             StartedAt=x['State']['StartedAt'],RestartCount=x['RestartCount'])

def protect_others():
 for service in (other,'database'):
  if inspect(service)!=before[service]:raise DeploymentError('Protected service changed: '+service+'.')

def atomic_write(path,data,mode):
 temporary=path.with_name(path.name+'.'+uuid.uuid4().hex+'.tmp')
 fd=os.open(temporary,os.O_WRONLY|os.O_CREAT|os.O_EXCL,mode)
 try:
  with os.fdopen(fd,'wb') as f:f.write(data);f.flush();os.fsync(f.fileno())
  os.replace(temporary,path)
 finally:
  if temporary.exists():temporary.unlink()

def updated_override(data,new_image):
 """Preserve every unrelated YAML byte; ambiguous/flow YAML fails closed."""
 bom=b'\xef\xbb\xbf' if data.startswith(b'\xef\xbb\xbf') else b''
 text=data.decode('utf-8-sig')
 if text.lstrip().startswith('{'):
  doc=json.loads(text);value=doc['services'][theme]['image']
  if not isinstance(value,str):raise DeploymentError('Override image must be a scalar.')
  doc['services'][theme]['image']=new_image
  return bom+(json.dumps(doc,indent=2)+'\n').encode(),value
 lines=text.splitlines(keepends=True);services=False;service_indent=None;service=False;field_indent=None;matches=[]
 for i,line in enumerate(lines):
  stripped=line.strip()
  if not stripped or stripped.startswith('#'):continue
  if line.startswith('services:'):
   if services:raise DeploymentError('Duplicate services key in image override.')
   if not re.fullmatch(r'services:\s*(?:#.*)?',line.rstrip('\r\n')):
    raise DeploymentError('Use block-style services in the image override.')
   services=True;continue
  if not services:continue
  if not line[0].isspace():break
  if '\t' in line[:len(line)-len(line.lstrip())]:raise DeploymentError('Tab-indented image override is unsupported.')
  indent=len(line)-len(line.lstrip())
  mapping=re.fullmatch(r'\s*([A-Za-z0-9_.-]+):\s*(?:#.*)?',line.rstrip('\r\n'))
  if service_indent is None:
   if not mapping:raise DeploymentError('Use block-style service mappings in the image override.')
   service_indent=indent
  if indent==service_indent:
   if not mapping:raise DeploymentError('Unsupported service mapping in image override.')
   service=mapping.group(1)==theme;field_indent=None;continue
  if service and field_indent is None:field_indent=indent
  if service and indent==field_indent and re.match(r'^\s*image\s*:',line):
   match=re.fullmatch(r'(\s*image\s*:\s*)([^\r\n]*?)(\r?\n)?',line)
   raw=match.group(2);comment=''
   if ' #' in raw:raw,comment=raw.split(' #',1);comment=' #'+comment
   value=raw.strip()
   if len(value)>1 and value[0]==value[-1] and value[0] in ('"',"'"):value=value[1:-1]
   if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._:/@+-]*',value):raise DeploymentError('Image override scalar is not a literal Docker reference.')
   matches.append((i,match.group(1)+new_image+comment+(match.group(3) or ''),value))
 if len(matches)!=1:raise DeploymentError('Expected one literal selected-service image in override.')
 i,line,value=matches[0];lines[i]=line
 return bom+''.join(lines).encode(),value

def image_info(reference):
 return json.loads(command(['docker','image','inspect',reference]).stdout)[0]

def verify_image(info):
 labels=info['Config'].get('Labels') or {}
 expected={'com.nop.storefront.theme':options['brand'],'org.opencontainers.image.revision':options['revision'],
           'com.nop.storefront.source-sha256':options['sourceHash']}
 if info.get('Os')!='linux' or info.get('Architecture')!='amd64':raise DeploymentError('Loaded image must be linux/amd64.')
 if any(labels.get(k)!=v for k,v in expected.items()):raise DeploymentError('Loaded image identity labels do not match requested source/theme.')

def wait_stable(extra=None,expected_id=None):
 deadline=time.monotonic()+options['timeout'];stable_since=None;container_id=None
 while time.monotonic()<deadline:
  current=inspect(theme)
  if not current['Running'] or current['RestartCount']:
   raise DeploymentError('Selected container exited or restarted during startup.')
  if expected_id and current['ImageId']!=expected_id:raise DeploymentError('Selected container runs an unexpected image.')
  if container_id and current['Id']!=container_id:raise DeploymentError('Selected container changed during stability window.')
  container_id=current['Id']
  result=command(['docker','exec',container_id,'cat','/proc/net/tcp','/proc/net/tcp6'],required=False)
  listening=result.returncode==0 and any(len(x.split())>3 and x.split()[1].endswith(':1F90') and x.split()[3]=='0A' for x in result.stdout.splitlines())
  if listening:
   if stable_since is None:stable_since=time.monotonic()
   if time.monotonic()-stable_since>=5:return current
  else:stable_since=None
  time.sleep(1)
 raise DeploymentError('Startup timeout: no stable passive TCP 8080 listener.')

def save_receipt():
 if release:atomic_write(release/'receipt.json',(json.dumps(receipt,indent=2)+'\n').encode(),0o600)

def retire_previous():
 old=before[theme]['ImageId'];new=inspect(theme)['ImageId']
 if old==new:return dict(Status='skipped',Reason='Previous and current image IDs are equal.')
 ids=command(['docker','ps','-aq','--no-trunc']).stdout.split()
 for cid in ids:
  used=json.loads(command(['docker','inspect',cid]).stdout)[0]['Image']
  if used==old:return dict(Status='skipped',Reason='A running or stopped container still references the previous image.')
 info=image_info(old);tags=info.get('RepoTags') or []
 prefix=options['imageRepository']+':'+theme+'-'
 if not tags or any(not x.startswith(prefix) for x in tags):
  return dict(Status='skipped',Reason='Previous image has missing or foreign ownership tags.')
 if len(tags)!=1:return dict(Status='skipped',Reason='Previous image has multiple tags; no tag or image was removed.')
 protect_others()
 # The ID and all container references are checked again immediately before deletion.
 if inspect(theme)['ImageId']==old:return dict(Status='skipped',Reason='Previous image became current.')
 for cid in command(['docker','ps','-aq','--no-trunc']).stdout.split():
  if json.loads(command(['docker','inspect',cid]).stdout)[0]['Image']==old:
   return dict(Status='skipped',Reason='A container acquired the previous image before retirement.')
 fresh=image_info(old)
 if fresh.get('RepoTags')!=tags:return dict(Status='skipped',Reason='Image tags changed before retirement.')
 result=command(['docker','image','rm',old],required=False)
 if result.returncode:return dict(Status='skipped',Reason='Docker refused unforced image removal; previous image retained.')
 return dict(Status='removed',ImageId=old,Tags=tags)

try:
 if not root.is_dir() or not (root/'compose.yml').is_file() or not (root/'images.override.yml').is_file():
  raise DeploymentError('Existing deployment root/compose/image override is missing.')
 lock=os.open(root/'.deploy.lock',os.O_RDWR|os.O_CREAT,0o600)
 try:fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
 except BlockingIOError:raise DeploymentError('Another deployment holds the server lock.')
 before={k:inspect(k) for k in (theme,other,'database')}
 if not all(x['Running'] for x in before.values()):raise DeploymentError('Existing stores/database must be running before deployment.')
 original=(root/'images.override.yml').read_bytes();mode=(root/'images.override.yml').stat().st_mode&0o777
 replacement,old_reference=updated_override(original,options['image'])
 if image_info(old_reference)['Id']!=before[theme]['ImageId']:raise DeploymentError('Override does not match current selected image.')
 if command(['docker','image','inspect',options['image']],required=False).returncode==0:
  raise DeploymentError('Requested new image tag already exists on server; build a fresh unique tag.')
 docker_root=pathlib.Path(command(['docker','info','--format','{{.DockerRootDir}}']).stdout.strip())
 image_required=2*options['expandedBytes']+256*1024*1024
 archive_required=options['archiveBytes']+64*1024*1024
 same_device=root.stat().st_dev==docker_root.stat().st_dev
 required=image_required+archive_required if same_device else archive_required
 if shutil.disk_usage(root).free<required or (not same_device and shutil.disk_usage(docker_root).free<image_required):
  raise DeploymentError('Insufficient space for archive plus Docker load; nothing uploaded or switched.')
 stamp=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
 release=root/'releases'/(stamp+'-'+theme+'-image-'+uuid.uuid4().hex[:8]);release.mkdir(mode=0o700,parents=True)
 receipt=dict(Theme=options['brand'],SourceCommit=options['revision'],SourceSha256=options['sourceHash'],
              ArchiveSha256=options['archiveHash'],Release=str(release),Before=before,status='uploading',
              NoHttpChecks=True,NoDatabaseOrAdminCommands=True)
 save_receipt()
 emit('ready',archive=str(release/'image.tar.part'),freeBytes=shutil.disk_usage(root).free,requiredBytes=required)
 if sys.stdin.readline().strip()!='uploaded':raise DeploymentError('Image upload was cancelled or connection closed.')
 archive=release/'image.tar.part'
 if archive.stat().st_size!=options['archiveBytes']:raise DeploymentError('Uploaded image archive length differs.')
 digest=hashlib.sha256()
 with archive.open('rb') as f:
  for chunk in iter(lambda:f.read(8*1024*1024),b''):digest.update(chunk)
 if digest.hexdigest()!=options['archiveHash']:raise DeploymentError('Uploaded image SHA256 differs.')
 archive.rename(release/'image.tar');archive=release/'image.tar'
 if command(['docker','image','inspect',options['image']],required=False).returncode==0:
  raise DeploymentError('Requested new tag appeared during upload; refusing to overwrite it.')
 emit('progress',stage='Loading selected image; existing stores remain running.')
 command(['docker','load','--input',str(archive)],timeout=1800);loaded=image_info(options['image']);verify_image(loaded)
 receipt['NewImageId']=loaded['Id'];receipt['NewImage']=options['image']
 for k in before:
  if inspect(k)!=before[k]:raise DeploymentError('Container state changed while image was uploaded/loaded: '+k+'.')
 if (root/'images.override.yml').read_bytes()!=original:raise DeploymentError('Canonical override changed during upload/load.')
 atomic_write(release/'images.override.before.yml',original,0o600)
 selected=release/'selected.override.json'
 atomic_write(selected,json.dumps({'services':{theme:{'image':options['image']}}}).encode(),0o600)
 emit('progress',stage='Replacing only '+theme+'; protected services are not recreated.')
 switched=True
 command(compose(selected)+['up','-d','--no-build','--no-deps','--force-recreate',theme],timeout=options['timeout']+60)
 receipt['Current']=wait_stable(selected,loaded['Id']);protect_others()
 if (root/'images.override.yml').read_bytes()!=original:raise DeploymentError('Canonical override changed before commit.')
 atomic_write(root/'images.override.yml',replacement,mode)
 receipt['status']='started';receipt['After']={k:inspect(k) for k in before};save_receipt()
 # Once startup and its durable receipt succeed, cleanup failure must not roll back a live service.
 committed=True
 emit('progress',stage='Selected container stable; retiring only the immediately previous unused image.')
 try:receipt['PreviousImageCleanup']=retire_previous()
 except Exception:receipt['PreviousImageCleanup']=dict(Status='unconfirmed',Reason='Cleanup could not be confirmed; no forced retry was made.')
 protect_others();receipt['After']={k:inspect(k) for k in before}
 receipt['DiskFreeBytes']=shutil.disk_usage(root).free;receipt['status']='success';save_receipt()
except BaseException as error:
 exit_code=1
 message=str(error) if isinstance(error,DeploymentError) else 'Deployment failed; inspect the retained private release receipt.'
 rollback=None
 if switched and not committed:
  try:
   atomic_write(root/'images.override.yml',original,mode)
   command(compose()+['up','-d','--no-build','--no-deps','--force-recreate',theme],timeout=options['timeout']+60)
   restored=wait_stable(expected_id=before[theme]['ImageId']);protect_others()
   rollback=dict(Status='restored',Current=restored)
  except BaseException:rollback=dict(Status='failed',Reason='Manual recovery required; previous image and original override backup retained.')
 if receipt:
  receipt['status']='rolled_back' if rollback and rollback['Status']=='restored' else 'failed';receipt['Error']=message;receipt['Rollback']=rollback
  try:save_receipt()
  except BaseException:pass
finally:
 if release:
  removed=[];warnings=[]
  for name in ('image.tar','image.tar.part'):
   candidate=release/name
   try:
    if candidate.exists() or candidate.is_symlink():
     if candidate.parent.resolve()!=release.resolve() or not stat.S_ISREG(candidate.lstat().st_mode):
      warnings.append('Archive path was not a regular release-owned file; retained.')
     else:candidate.unlink();removed.append(name)
   except OSError:warnings.append('Release-owned transfer archive cleanup could not be completed.')
  if receipt:
   receipt['TransferArchiveCleanup']=dict(Removed=removed,Warnings=warnings)
   receipt['DiskFreeBytes']=shutil.disk_usage(root).free
   try:save_receipt()
   except BaseException:exit_code=1;message='Durable receipt could not be finalized; inspect retained release metadata.'
 if exit_code:emit('failed',message=message,rollback=rollback,receipt=receipt or None)
 else:emit('receipt',receipt=receipt)
 sys.exit(exit_code)
'''


def archive_identity(path, image, expected):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(8 * 1024 * 1024), b''):
            digest.update(chunk)
    with tarfile.open(path, 'r:*') as archive:
        manifest_file = archive.getmember('manifest.json')
        if manifest_file.size > 1024 * 1024:
            raise ValueError('Image archive manifest is too large.')
        manifest = json.load(archive.extractfile(manifest_file))
        if len(manifest) != 1 or manifest[0].get('RepoTags') != [image]:
            raise ValueError('Archive must contain exactly the requested single Docker image/tag.')
        config_file = archive.getmember(manifest[0]['Config'])
        if config_file.size > 8 * 1024 * 1024:
            raise ValueError('Image config is too large.')
        config = json.load(archive.extractfile(config_file))
        labels = config.get('config', {}).get('Labels') or {}
        if config.get('os') != 'linux' or config.get('architecture') != 'amd64':
            raise ValueError('Saved image must target linux/amd64.')
        if any(labels.get(key) != value for key, value in expected.items()):
            raise ValueError('Saved image labels do not match the selected source/theme.')
        expanded = sum(member.size for member in archive.getmembers() if member.isfile())
    return digest.hexdigest(), expanded


def arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--theme', choices=['madadrang', 'boutique'], required=True)
    parser.add_argument('--image', required=True)
    parser.add_argument('--image-size', type=int, required=True,
                        help='Actual Docker image inspect Size in bytes.')
    parser.add_argument('--archive', type=pathlib.Path, required=True)
    parser.add_argument('--config', type=pathlib.Path, required=True)
    parser.add_argument('--receipt', type=pathlib.Path, required=True)
    parser.add_argument('--source-commit', required=True)
    parser.add_argument('--source-hash', required=True)
    parser.add_argument('--startup-timeout', type=int, default=120)
    result = parser.parse_args()
    if result.image_size <= 0:
        parser.error('--image-size must be a positive Docker image size in bytes.')
    if not re.fullmatch(r'[a-f0-9]{40}', result.source_commit):
        parser.error('--source-commit must be a full lowercase Git SHA.')
    if not re.fullmatch(r'[a-f0-9]{64}', result.source_hash):
        parser.error('--source-hash must be a lowercase SHA256.')
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._:/-]*:[A-Za-z0-9_][A-Za-z0-9_.-]*', result.image):
        parser.error('--image must be an explicit Docker repository:tag.')
    if not 30 <= result.startup_timeout <= 1800:
        parser.error('--startup-timeout must be between 30 and 1800 seconds.')
    return result


def main():
    args = arguments()
    brand = 'Madadrang' if args.theme == 'madadrang' else 'Boutique'
    expected = {'com.nop.storefront.theme': brand,
                'org.opencontainers.image.revision': args.source_commit,
                'com.nop.storefront.source-sha256': args.source_hash}
    archive_hash, expanded = archive_identity(args.archive, args.image, expected)
    config = json.loads(args.config.read_text(encoding='utf-8-sig'))
    for key in ('knownHostsPath', 'handoffPath', 'keyFilename'):
        if config.get(key):
            configured = pathlib.Path(config[key]).expanduser()
            config[key] = str(configured if configured.is_absolute() else args.config.resolve().parent / configured)
    password = None
    if not config.get('keyFilename') and config.get('handoffPath'):
        match = re.search(r'^Password: (.+)$', pathlib.Path(config['handoffPath']).read_text(encoding='utf-8-sig'), re.M)
        if match:
            password = match.group(1).strip()
    if not password and not config.get('keyFilename'):
        raise ValueError('Configure handoffPath or keyFilename for pinned SSH authentication.')
    options = dict(theme=args.theme, brand=brand, root=config['remoteRoot'], image=args.image,
                   revision=args.source_commit, sourceHash=args.source_hash, archiveHash=archive_hash,
                   archiveBytes=args.archive.stat().st_size, expandedBytes=max(expanded, args.image_size),
                   timeout=args.startup_timeout, imageRepository=config.get('imageRepository', 'nop-dual'))
    client = paramiko.SSHClient()
    client.load_system_host_keys()
    client.load_host_keys(config['knownHostsPath'])
    client.set_missing_host_key_policy(paramiko.RejectPolicy())
    received = None
    try:
        client.connect(config['host'], port=int(config.get('port', 22)), username=config['username'],
                       password=password, key_filename=config.get('keyFilename'),
                       look_for_keys=False, allow_agent=False, timeout=20)
        client.get_transport().set_keepalive(15)
        command = 'python3 -u -c ' + shlex.quote(REMOTE_WORKER) + ' ' + shlex.quote(json.dumps(options))
        stdin, stdout, stderr = client.exec_command(command, timeout=max(1800, args.startup_timeout + 1200))
        stdout.channel.set_combine_stderr(True)
        while True:
            line = stdout.readline()
            if not line:
                break
            try:
                event = json.loads(line)
            except json.JSONDecodeError:
                # Never echo unexpected remote output; it might contain private configuration.
                continue
            kind = event.get('type')
            if kind == 'ready':
                print('Remote lock acquired; uploading selected image. Disk free/required:',
                      event['freeBytes'], event['requiredBytes'], flush=True)
                with client.open_sftp() as sftp:
                    sftp.get_channel().settimeout(1800)
                    with sftp.file(event['archive'], 'wb') as destination:
                        sftp.chmod(event['archive'], 0o600)
                        destination.set_pipelined(True)
                        transferred = 0
                        next_progress = 64 * 1024 * 1024
                        with args.archive.open('rb') as source:
                            while chunk := source.read(1024 * 1024):
                                destination.write(chunk)
                                transferred += len(chunk)
                                if transferred >= next_progress:
                                    print('Image upload:', transferred // (1024 * 1024), 'MiB queued of', args.archive.stat().st_size // (1024 * 1024), 'MiB', flush=True)
                                    next_progress += 64 * 1024 * 1024
                stdin.write('uploaded\n')
                stdin.flush()
            elif kind == 'progress':
                print(event['stage'], flush=True)
            elif kind in ('receipt', 'failed'):
                received = event.get('receipt')
                if received:
                    args.receipt.parent.mkdir(parents=True, exist_ok=True)
                    args.receipt.write_text(json.dumps(received, indent=2) + '\n', encoding='utf-8')
                if kind == 'failed':
                    print(event['message'], file=sys.stderr, flush=True)
                    if event.get('rollback'):
                        print('Rollback:', event['rollback']['Status'], file=sys.stderr, flush=True)
        status = stdout.channel.recv_exit_status()
        if status or not received or received.get('status') != 'success':
            raise RuntimeError('Image deployment did not complete; review the private receipt if present.')
        print('Deployed', brand, args.image, 'source', args.source_commit, flush=True)
        cleanup = received['PreviousImageCleanup']
        print('Previous image:', cleanup['Status'], cleanup.get('Reason', cleanup.get('ImageId', '')), flush=True)
        print('Receipt:', args.receipt, flush=True)
    finally:
        client.close()


if __name__ == '__main__':
    try:
        main()
    except Exception:
        # Do not print arbitrary exception strings from SSH/config/compose providers.
        print('Image deployment failed. No credentials were printed; review the receipt and configuration.', file=sys.stderr)
        sys.exit(1)
