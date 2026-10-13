#!/usr/bin/env python3
"""Verify rendered production presentation, rather than static container health."""
import argparse
from html.parser import HTMLParser
from html import unescape
from urllib.parse import urljoin, urlparse
from urllib.request import build_opener, HTTPCookieProcessor, Request
from http.cookiejar import CookieJar


class Page(HTMLParser):
    def __init__(self, html):
        super().__init__()
        self.tags = []
        self.product_links = []
        self.await_product_link = False
        self.feed(html)

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        self.tags.append((tag, attrs))
        if "data-productid" in attrs:
            self.await_product_link = True
        elif tag == "a" and self.await_product_link and attrs.get("href"):
            self.product_links.append(attrs["href"])
            self.await_product_link = False


def verify(base, expect_seed_data=False):
    opener = build_opener(HTTPCookieProcessor(CookieJar()))

    def get(path):
        with opener.open(Request(urljoin(base, path), headers={"User-Agent": "Madadrang-Deployment-Check"}), timeout=60) as response:
            if response.status != 200:
                raise RuntimeError(f"HTTP {response.status}: {path}")
            return response.read(), response.headers.get("Content-Type", "")

    raw, _ = get(base)
    html = unescape(raw.decode("utf-8"))
    page = Page(html)
    root = next(attrs for tag, attrs in page.tags if tag == "html")
    if not root.get("lang", "").startswith("fa") or root.get("dir") != "rtl":
        raise RuntimeError(f"Wrong public language/direction: {root}")
    for text in ("iPhone 16", "Salaxy S24", "Welcome to our store"):
        if text in html:
            raise RuntimeError(f"Installer content is still visible: {text}")
    if not any("storefront" in attrs.get("class", "").split() for _, attrs in page.tags):
        raise RuntimeError("The Madadrang storefront layout is missing")
    tiles = [attrs for tag, attrs in page.tags if tag == "a" and "category-tile" in attrs.get("class", "").split()]
    products = [attrs for tag, attrs in page.tags if "data-productid" in attrs]
    if expect_seed_data and (len(tiles) < 4 or len({attrs["data-productid"] for attrs in products}) < 8):
        raise RuntimeError(f"Incomplete homepage: {len(tiles)} categories, {len(products)} product elements")
    if expect_seed_data and "تومان" not in html:
        raise RuntimeError("Sample prices are not displayed in toman")
    print(f"Checking assets and native routes: {len(tiles)} categories, {len(products)} product elements", flush=True)
    assets = {attrs.get("src") for tag, attrs in page.tags if tag == "img" and attrs.get("src")}
    assets |= {attrs["href"] for tag, attrs in page.tags if tag == "link" and attrs.get("rel") == "stylesheet"}
    for path in sorted(assets):
        if urlparse(urljoin(base, path)).netloc != urlparse(base).netloc:
            continue
        data, kind = get(path)
        if not data or ("text/html" in kind and not path.endswith(".html")):
            raise RuntimeError(f"Invalid homepage asset: {path} ({kind})")
    for tile in tiles:
        get(tile["href"])
    get("/catalog/all")
    if page.product_links:
        get(page.product_links[0])
    print(f"PASS: Persian RTL, no installer content, {len(tiles)} categories, {len(products)} product elements, {len(assets)} assets, native routes")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("base_url")
    parser.add_argument("--expect-seed-data", action="store_true")
    args = parser.parse_args()
    verify(args.base_url.rstrip("/") + "/", args.expect_seed_data)
