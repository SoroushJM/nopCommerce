"""Check custom changes against the reviewed upstream nopCommerce baseline."""

import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
# Advance only after reviewing and merging the corresponding upstream changes.
BASE = "77d112f365e210ebdfa2f9790c8202a0e3b1c74e"
NATIVE_ROOTS = ("src/Libraries/", "src/Presentation/Nop.Web/", "src/Presentation/Nop.Web.Framework/", "src/Tests/Nop.Tests/")
CODE_EXTENSIONS = {".cs", ".cshtml", ".css", ".js", ".csproj", ".props", ".targets", ".sln"}
CUSTOM_ROOTS = ("src/Presentation/Nop.Web/Themes/Madadrang/", "src/Presentation/Nop.Web/Themes/MishaShop/")
ADMIN_ASSETS = ("src/Presentation/Nop.Web/wwwroot/lib/jalalidatepicker/",)
# These native changes implement Persian admin transfer/PDF presentation only.
ADMIN_FILES = {
    "src/Libraries/Nop.Services/Common/Pdf/InvoiceDocument.cs",
    "src/Libraries/Nop.Services/Common/Pdf/PdfDocument.cs",
    "src/Libraries/Nop.Services/Common/PdfService.cs",
    "src/Libraries/Nop.Services/ExportImport/ExportManager.cs",
    "src/Libraries/Nop.Services/ExportImport/TransferCalendar.cs",
    "src/Presentation/Nop.Web/Views/Customer/Login.cshtml",
    "src/Presentation/Nop.Web/wwwroot/css/admin/persian-admin.css",
    "src/Presentation/Nop.Web/wwwroot/js/persian-admin.js",
    "src/Tests/Nop.Tests/Nop.Services.Tests/ExportImport/TransferCalendarTests.cs",
}


def git(*args):
    return subprocess.check_output(["git", "-C", str(ROOT), *args], stderr=subprocess.PIPE).decode("utf-8")


def main():
    original = set(git("ls-tree", "-r", "--name-only", BASE).splitlines())
    changed = set(git("diff", "--name-only", BASE).splitlines())
    changed.update(git("ls-files", "--others", "--exclude-standard").splitlines())
    failures = []
    for name in sorted(changed):
        if name in {"src/Directory.Build.props", "src/NopCommerce.sln"}:
            failures.append(name)
            continue
        if not name.startswith(NATIVE_ROOTS) or Path(name).suffix not in CODE_EXTENSIONS:
            continue
        if "/Areas/Admin/" in name or name in ADMIN_FILES or name.startswith(ADMIN_ASSETS):
            continue
        if name not in original and (name.startswith(CUSTOM_ROOTS) or name.startswith("src/Presentation/Nop.Web/wwwroot/css/storefront-native-base")):
            continue
        failures.append(name)
    if failures:
        print("Original nopCommerce changes outside Persian admin localization:")
        print("\n".join(failures))
        return 1
    print("PASS: original nopCommerce source differs only for Persian admin localization.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
