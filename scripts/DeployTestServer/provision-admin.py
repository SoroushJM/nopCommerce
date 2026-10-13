"""Apply designated test-admin provisioning without rebuilding or HTTP checks."""
import argparse, json, pathlib, re, shlex
import paramiko
p=argparse.ArgumentParser()
p.add_argument('--config',required=True)
p.add_argument('--theme',choices=['madadrang','boutique','both'],default='both')
a=p.parse_args(); c=json.loads(pathlib.Path(a.config).read_text(encoding='utf-8-sig'))
password=re.search(r'^Password: (.+)$',pathlib.Path(c['handoffPath']).read_text(encoding='utf-8-sig'),re.M).group(1).strip()
client=paramiko.SSHClient(); client.load_system_host_keys(); client.load_host_keys(c['knownHostsPath']); client.set_missing_host_key_policy(paramiko.RejectPolicy())
client.connect(c['host'],username=c['username'],password=password,look_for_keys=False,allow_agent=False,timeout=20)
try:
 root=c['remoteRoot']
 with client.open_sftp() as s:
  for source,destination in [(pathlib.Path(__file__).with_name('native-admin.py'),root+'/native-admin.py'),(pathlib.Path(c['adminCredentialsPath']),root+'/admin-credentials.json')]:
   s.put(str(source),destination); s.chmod(destination,0o600)
 themes=['madadrang','boutique'] if a.theme=='both' else [a.theme]
 command='cd '+shlex.quote(root)+' && '+ ' && '.join('python3 native-admin.py '+theme for theme in themes)
 _,out,err=client.exec_command(command,timeout=120)
 for line in out: print(line.replace(password,'[redacted]'),end='',flush=True)
 if out.channel.recv_exit_status(): raise SystemExit('Scoped remote administrator provisioning failed; credentials are not printed')
finally:
 client.close()
