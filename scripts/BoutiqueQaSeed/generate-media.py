"""Create original garment-only SVG illustrations; no photo or human model is used.
The SVG drawings are released under CC0-1.0 for this sample catalogue.
"""
import json
from pathlib import Path

root = Path(__file__).resolve().parent
items = json.loads((root / 'catalog.json').read_text(encoding='utf-8'))
media = root / 'media'
media.mkdir(exist_ok=True)

def garment(kind):
    shapes = {
        'shirt': '<path d="M255 190 L320 160 Q350 205 380 160 L445 190 L520 295 L473 334 L423 264 L429 560 Q350 585 271 560 L277 264 L227 334 L180 295 Z"/><path d="M320 160 L300 215 L344 238 L350 202 L356 238 L400 215 L380 160" class="detail"/><path d="M350 208 L350 568 M205 297 L241 321 M459 321 L495 297" class="seam"/><g class="button"><circle cx="356" cy="267" r="4"/><circle cx="356" cy="322" r="4"/><circle cx="356" cy="377" r="4"/><circle cx="356" cy="432" r="4"/><circle cx="356" cy="487" r="4"/><circle cx="356" cy="542" r="4"/></g><path d="M383 280 L413 280 L413 319 L383 319 Z" class="detail"/>',
        'top': '<path d="M285 182 L321 177 Q350 218 379 177 L415 182 L430 261 L416 535 Q350 560 284 535 L270 261 Z"/><path d="M321 184 Q350 228 379 184 M283 521 Q350 545 417 521" class="seam"/><path d="M295 227 L291 530 M313 241 L309 535 M333 251 L332 539 M350 253 L350 540 M367 250 L369 539 M387 240 L391 535 M406 225 L410 530" class="fabric"/>',
        'knit': '<path d="M255 180 L318 168 Q350 198 382 168 L445 180 L506 280 L491 535 L442 535 L426 300 L431 558 Q350 578 269 558 L274 300 L258 535 L209 535 L194 280 Z"/><path d="M317 171 Q350 212 383 171 M270 530 L430 530 M213 503 L255 503 M446 503 L488 503" class="seam"/><path d="M300 212 L300 532 M325 215 L325 534 M350 218 L350 538 M375 215 L375 534 M400 212 L400 532" class="knit"/>',
        'vest': '<path d="M279 182 L318 170 L350 253 L382 170 L421 182 Q399 255 426 317 L426 540 Q350 565 274 540 L274 317 Q301 255 279 182 Z"/><path d="M314 177 L350 269 L386 177 M277 517 L423 517" class="seam"/><path d="M300 275 L300 518 M325 290 L325 522 M350 299 L350 526 M375 290 L375 522 M400 275 L400 518" class="knit"/>',
        'blazer': '<path d="M258 172 L316 158 L350 182 L384 158 L442 172 L506 273 L485 532 L443 532 L419 299 L432 590 L355 604 L345 604 L268 590 L281 299 L257 532 L215 532 L194 273 Z"/><path d="M316 160 L297 236 L325 225 L314 259 L355 349 L385 259 L375 225 L403 236 L384 160 L351 319 Z" class="detail"/><path d="M351 323 L351 593 M284 428 L326 434 M378 434 L420 428 M279 449 L322 455 M382 455 L425 449" class="seam"/><circle cx="360" cy="381" r="5" class="button"/><circle cx="360" cy="431" r="5" class="button"/>',
        'coat': '<path d="M258 170 L313 150 L350 182 L387 150 L442 170 L500 285 L480 517 L442 517 L423 287 L440 635 L260 635 L277 287 L258 517 L220 517 L200 285 Z"/><path d="M313 151 L292 215 L325 220 L310 249 L350 336 L390 249 L375 220 L408 215 L387 151 L350 292 Z" class="detail"/><path d="M270 376 L430 376 L430 395 L270 395 Z" class="detail"/><path d="M324 385 L363 385 L378 441 L357 446 L346 398 M303 448 L286 496 M397 448 L414 496" class="seam"/><g class="button"><circle cx="324" cy="309" r="5"/><circle cx="377" cy="309" r="5"/><circle cx="324" cy="350" r="5"/><circle cx="377" cy="350" r="5"/></g>',
        'pants': '<path d="M279 156 L421 156 L432 325 L421 635 L361 635 L350 347 L339 635 L279 635 L268 325 Z"/><path d="M280 178 L420 178 M350 178 L350 290 Q351 311 333 316 M297 184 L295 298 M403 184 L405 298 M310 305 L306 615 M390 305 L394 615" class="seam"/><circle cx="353" cy="170" r="4" class="button"/>',
        'jeans': '<path d="M276 156 L424 156 L438 320 L430 635 L363 635 L350 356 L337 635 L270 635 L262 320 Z"/><path d="M278 180 L422 180 M291 181 Q299 241 269 256 M409 181 Q401 241 431 256 M350 183 L350 288 Q350 307 330 310 M285 306 L283 615 M415 306 L417 615 M273 613 L335 613 M365 613 L427 613" class="seam"/><path d="M306 157 L306 185 M394 157 L394 185" class="detail"/><circle cx="353" cy="170" r="4" class="button"/>',
        'bag': '<path d="M248 331 Q255 310 275 310 L425 310 Q445 310 452 331 L468 532 Q350 583 232 532 Z"/><path d="M278 318 Q280 202 350 202 Q420 202 422 318" class="strap"/><path d="M273 326 Q350 354 427 326 L419 398 Q350 427 281 398 Z" class="detail"/><rect x="333" y="386" width="34" height="18" rx="3" class="gold"/><path d="M246 491 Q350 528 454 491 M258 341 L250 518 M442 341 L450 518" class="seam"/>',
        'glasses': '<path d="M170 337 Q244 296 319 329 L309 405 Q246 449 180 407 Z M381 329 Q456 296 530 337 L520 407 Q454 449 391 405 Z" class="lens"/><path d="M170 337 Q244 296 319 329 L309 405 Q246 449 180 407 Z M381 329 Q456 296 530 337 L520 407 Q454 449 391 405 Z M319 336 Q350 322 381 336 M174 345 L132 282 M525 345 L568 282" class="frame"/>'
    }
    return shapes[kind]

def svg(item):
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="700" height="840" viewBox="0 0 700 840" role="img" aria-label="{item['name']} — تصویرسازی نمونه">
<title>{item['name']} — تصویرسازی نمونه، بدون مدل انسانی</title>
<defs><linearGradient id="back" x2="1" y2="1"><stop stop-color="#f5f0e8"/><stop offset="1" stop-color="#e9e2d8"/></linearGradient><linearGradient id="cloth" x1="0" x2="1"><stop stop-color="{item['color']}"/><stop offset=".45" stop-color="{item['color']}"/><stop offset="1" stop-color="#ffffff" stop-opacity=".32"/></linearGradient><filter id="shadow" x="-30%" y="-30%" width="160%" height="170%"><feDropShadow dx="10" dy="18" stdDeviation="13" flood-color="#70604c" flood-opacity=".15"/></filter></defs>
<rect width="700" height="840" fill="url(#back)"/><rect x="32" y="32" width="636" height="776" rx="9" fill="none" stroke="#ffffff" stroke-opacity=".5"/>
<ellipse cx="350" cy="690" rx="190" ry="27" fill="#b6a590" opacity=".12"/>
<style>.detail{{fill:{item['color']};stroke:#584a3e;stroke-opacity:.25;stroke-width:2}}.seam{{fill:none;stroke:#584a3e;stroke-opacity:.33;stroke-width:2}}.fabric{{fill:none;stroke:#fff;stroke-opacity:.24;stroke-width:2}}.knit{{fill:none;stroke:#584a3e;stroke-opacity:.18;stroke-width:7;stroke-dasharray:5 5}}.button{{fill:#efe9db;stroke:#88745d;stroke-width:1}}.strap{{fill:none;stroke:{item['color']};stroke-width:21}}.gold{{fill:#c4a15b}}.lens{{fill:#665d4f;fill-opacity:.85}}.frame{{fill:none;stroke:{item['color']};stroke-width:12;stroke-linejoin:round}}</style>
<g fill="url(#cloth)" stroke="#766450" stroke-opacity=".26" stroke-width="2" filter="url(#shadow)">{garment(item['kind'])}</g>
<path d="M310 738 L390 738" stroke="#ad9679" stroke-width="2"/>
<text x="350" y="772" text-anchor="middle" font-family="Arial,sans-serif" font-size="13" letter-spacing="5" fill="#8c7d6c">BOUTIQUE · SAMPLE</text></svg>'''

for item in items:
    (media / f"{item['sku'].lower()}.svg").write_text(svg(item), encoding='utf-8')
hero = '''<svg xmlns="http://www.w3.org/2000/svg" width="1400" height="1000" viewBox="0 0 1400 1000"><title>چیدمان تصویری مجموعهٔ نمونهٔ بوتیک — بدون مدل انسانی</title><rect width="1400" height="1000" fill="#eae0d2"/><path d="M0 740 L1400 560 L1400 1000 L0 1000" fill="#dfd2c0"/><circle cx="1190" cy="200" r="150" fill="#f2eade"/><g transform="translate(105,5) scale(1.15)" fill="#cdbb9d" stroke="#8b765e" stroke-width="2">'''
hero += garment('blazer') + '</g><g transform="translate(610,230) scale(.83)" fill="#7c96a7" stroke="#556e81" stroke-width="2">' + garment('jeans') + '</g><g transform="translate(760,330) scale(.74)" fill="#946947" stroke="#755539" stroke-width="2">' + garment('bag') + '</g><style>.detail{fill:#bfab8d;stroke:#8b765e;stroke-width:2}.seam{fill:none;stroke:#8b765e;stroke-width:2}.button{fill:#e7dccb}.strap{fill:none;stroke:#946947;stroke-width:22}.gold{fill:#c4a15b}</style></svg>'
(media / 'boutique-hero-sample.svg').write_text(hero, encoding='utf-8')
print(f'Created {len(items)} original product SVGs and one garment-only hero.')
