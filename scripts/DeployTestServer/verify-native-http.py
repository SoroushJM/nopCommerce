import urllib.request,urllib.parse,http.cookiejar,html.parser,pathlib,json,sys,re
root=pathlib.Path('/home/web/nop-dual-store-20261012'); theme=sys.argv[1]
if theme not in ['madadrang','boutique']: raise SystemExit('Unknown deployment')
public=sys.argv[2] if len(sys.argv)>2 else None
base=public or ('http://80.75.5.82:9011' if theme=='madadrang' else 'http://80.75.5.82:9012')
jar=http.cookiejar.CookieJar(); opener=urllib.request.build_opener(urllib.request.ProxyHandler({}),urllib.request.HTTPCookieProcessor(jar))
opener.addheaders=[('User-Agent','Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36')]
def get(path):
 with opener.open(base+path,timeout=120) as r: return r.status,r.url,r.read().decode('utf-8','replace')
class Inputs(html.parser.HTMLParser):
 def __init__(self): super().__init__(); self.hidden={}; self.styles=[]
 def handle_starttag(self,tag,attrs):
  a=dict(attrs)
  if tag=='input' and a.get('name') and a.get('type')=='hidden': self.hidden[a['name']]=a.get('value','')
  if tag=='link' and a.get('rel')=='stylesheet': self.styles.append(a.get('href',''))
records=[]
slug='/mrg-1' if theme=='madadrang' else '/boutique-linen-shirt'
for path in ['/','/catalog/all',slug,'/cart','/login','/register','/search?advs=true']:
 status,url,html=get(path); assert 'internal error occurred' not in html.lower(), 'Native error page rendered: '+path
 records.append({'path':path,'status':status,'bytes':len(html.encode())})
 if path=='/':
  selected='Madadrang' if theme=='madadrang' else 'Boutique'; opposite='Boutique' if theme=='madadrang' else 'Madadrang'
  assert f'_content/Storefront.UI.{selected}/' in html
  assert f'_content/Storefront.UI.{opposite}/' not in html
  assert 'dir="rtl"' in html
  assert not re.search(r'https?://(?:172\.(?:1[6-9]|2\d|3[01])\.|10\.|192\.168\.)[^\s"\']*/images/',html), 'Rendered media URL exposes an internal address'
  links=Inputs(); links.feed(html)
  matches=[x for x in links.styles if f'/_content/Storefront.UI.{selected}/' in x]
  cssurl=matches[0] if matches else f'/_content/Storefront.UI.{selected}/'+('storefront.css' if selected=='Madadrang' else 'boutique.css')
  if cssurl.startswith('http'): cssurl=urllib.parse.urlsplit(cssurl).path+'?'+urllib.parse.urlsplit(cssurl).query
  status,_,body=get(cssurl); assert len(body)>1000; records.append({'path':cssurl,'status':status,'bytes':len(body)})
  for linked in links.styles:
   if linked.startswith('/'):
    status,_,body=get(linked); assert body; records.append({'path':linked,'status':status,'bytes':len(body)})
cred=json.loads((root/'admin-credentials.json').read_text())[theme]
_,_,login=get('/login'); parser=Inputs(); parser.feed(login)
form=parser.hidden; form.update(Email=cred['email'],Password=cred['password'],RememberMe='false')
req=urllib.request.Request(base+'/login?returnUrl=%2FAdmin',data=urllib.parse.urlencode(form).encode(),method='POST')
with opener.open(req,timeout=120) as response: login_result=response.read().decode('utf-8','replace'); login_url=response.url
status,url,admin=get('/Admin')
if '/login' in url.lower() or 'main-sidebar' not in admin:
 print('Native authorization diagnostics:',theme,'login_final_url',login_url,'admin_final_url',url,'admin_title',re.findall(r'<title>(.*?)</title>',admin,re.S))
 print('Cookies after native login:',[(c.name,c.domain,c.path,c.secure) for c in jar])
 print('Decoded login validation:',re.findall(r'<div[^>]*validation-summary[^>]*>(.*?)</div>',__import__('html').unescape(login_result),re.S))
 errors=re.findall(r'<(?:li|span)[^>]*>([^<]+)</(?:li|span)>',login_result)
 print('Login text markers:',[x for x in errors if any(w in x.lower() for w in ['login','incorrect','ورود','اشتباه','رمز','کاربر'])][:6])
assert '/login' not in url.lower() and 'main-sidebar' in admin, 'Native admin login or authorization failed'
records.append({'path':'/Admin','status':status,'authenticated':True,'bytes':len(admin)})
out=root/f'{theme}-http-verification.json'; out.write_text(json.dumps(records,indent=2)); out.chmod(0o600)
print(theme,json.dumps(records,ensure_ascii=False))


