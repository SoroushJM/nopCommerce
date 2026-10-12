"""Compose a guarded PostgreSQL transaction from public sample data and original SVGs."""
import json
from pathlib import Path
root=Path(__file__).resolve().parent
items=json.loads((root/'catalog.json').read_text(encoding='utf-8'))
for item in items:
    item['imageHex']=(root/'media'/f"{item['sku'].lower()}.svg").read_bytes().hex()
sql=(root/'seed-template.sql').read_text(encoding='utf-8')
sql=sql.replace('__CATALOG_JSON__',json.dumps(items,ensure_ascii=False)).replace('__HERO_HEX__',(root/'media'/'boutique-hero-sample.svg').read_bytes().hex())
(root/'seed.generated.sql').write_text(sql,encoding='utf-8')
print('Prepared guarded Boutique QA transaction; no database changes performed.')
