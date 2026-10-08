"""Check the administrator language pack against nopCommerce's current English contracts."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
directory = root / "src/Presentation/Nop.Web/App_Data/Localization"
source = {node.attrib["Name"]: node.findtext("Value") or ""
          for node in ET.parse(directory / "defaultResources.nopres.xml").getroot()}
custom = {node.attrib["Name"]: node.findtext("Value") or ""
          for node in ET.parse(directory / "PersianAdmin/en-US.admin.xml").getroot()}
source.update(custom)
nodes = list(ET.parse(directory / "PersianAdmin/fa-IR.admin.xml").getroot())
translated = {node.attrib["Name"]: node.findtext("Value") or "" for node in nodes}
errors = []
if len(nodes) != len(translated):
    errors.append("Duplicate resource names")
for name, value in translated.items():
    if name not in source:
        errors.append(f"Unknown source: {name}")
        continue
    english = source[name]
    for label, pattern in [("format placeholders", r"\{\d+(?:[^}]*)\}"),
                           ("HTML tags", r"<[^>]+>"),
                           ("message tokens", r"%[A-Za-z][A-Za-z0-9_.]*%")]:
        expected = re.findall(pattern, english)
        actual = re.findall(pattern, value)
        if (sorted(expected) if label != "HTML tags" else expected) != (
                sorted(actual) if label != "HTML tags" else actual):
            errors.append(f"Changed {label}: {name}")
    if not value.strip():
        errors.append(f"Empty value: {name}")
    if "QQTOKEN" in value or "ZQZ" in value:
        errors.append(f"Unrestored translation token: {name}")
for name in source:
    if name.startswith(("Admin.", "Enums.", "ActivityLog.", "Pdf.", "PDFInvoice.",
                        "Security.Permission.", "Common.", "Plugins.FriendlyName.", "Account.Login.")):
        if name not in translated:
            errors.append(f"Untranslated required resource: {name}")
if errors:
    raise SystemExit("\n".join(errors))
print(f"Validated {len(translated)} Persian resources, placeholders, markup, and required coverage.")
