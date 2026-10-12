"""Verify both native admin sessions in one host-scoped cookie jar without cart writes."""
import html.parser
import http.cookiejar
import json
import pathlib
import urllib.parse
import urllib.request

root = pathlib.Path('/home/web/nop-dual-store-20261012')
credentials = json.loads((root / 'admin-credentials.json').read_text())
jar = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), urllib.request.HTTPCookieProcessor(jar))
opener.addheaders = [('User-Agent', 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36')]

class HiddenInputs(html.parser.HTMLParser):
    def __init__(self):
        super().__init__()
        self.values = {}

    def handle_starttag(self, tag, attributes):
        attributes = dict(attributes)
        if tag == 'input' and attributes.get('type') == 'hidden' and attributes.get('name'):
            self.values[attributes['name']] = attributes.get('value', '')

def fetch(base, path):
    with opener.open(base + path, timeout=120) as response:
        return response.status, response.url, response.read().decode('utf-8', 'replace')

records = []
for theme, port in [('madadrang', 9011), ('boutique', 9012)]:
    base = f'http://80.75.5.82:{port}'
    status, url, body = fetch(base, '/login')
    parser = HiddenInputs()
    parser.feed(body)
    form = parser.values
    form.update(Email=credentials[theme]['email'], Password=credentials[theme]['password'], RememberMe='false')
    request = urllib.request.Request(base + '/login?returnUrl=%2FAdmin', data=urllib.parse.urlencode(form).encode(), method='POST')
    with opener.open(request, timeout=120) as response:
        response.read()
    status, url, body = fetch(base, '/Admin')
    assert status == 200 and '/login' not in url.lower() and 'main-sidebar' in body, theme + ' native login failed'
    records.append({'Theme': theme, 'LoginAndAdminStatus': status})

# Revisit both after both logins: a collision would invalidate the first native session.
for theme, port in [('madadrang', 9011), ('boutique', 9012)]:
    base = f'http://80.75.5.82:{port}'
    status, url, body = fetch(base, '/Admin')
    assert status == 200 and '/login' not in url.lower() and 'main-sidebar' in body, theme + ' cross-store session collision'
    status, url, body = fetch(base, '/cart')
    assert status == 200 and 'internal error occurred' not in body.lower()
    records.append({'Theme': theme, 'AdminAfterOtherStoreLoginStatus': 200, 'ReadOnlyCartStatus': status})

cookie_names = sorted({cookie.name for cookie in jar})
for theme in ['madadrang', 'boutique']:
    selected = [name for name in cookie_names if name.startswith('.Nop.' + theme)]
    assert any('Authentication' in name for name in selected), theme + ' auth prefix missing'
    assert any('Antiforgery' in name for name in selected), theme + ' antiforgery prefix missing'
    assert any('Customer' in name for name in selected), theme + ' customer prefix missing'
assert not any(name.startswith('.Nop.') and not name.startswith(('.Nop.madadrang', '.Nop.boutique')) for name in cookie_names), 'Generic native cookie unexpectedly used'
proof = {'SameHostCookieJar': True, 'Records': records, 'CookieNames': cookie_names, 'CartWrites': 0, 'NativeCredentialsVerifiedForBoth': True, 'GuestCsrfProof': 'Use Verify-GuestCookieIsolation.ps1 separately'}
target = root / 'cookie-isolation-verification.json'
target.write_text(json.dumps(proof, indent=2))
target.chmod(0o600)
print(json.dumps(proof, indent=2))
