# انتشار فروشگاه با یک دستور

این راهنما مخصوص سرور آزمایشی موجود با دو سرویس است. برای پروژه‌های مستقل self-host و اسکریپت‌های هر فروشگاه، [راهنمای جدید](smsir-selfhost.md) را ببینید. برای سرور موجود از ریشهٔ مخزن اجرا کنید:

```powershell
pwsh -File ./scripts/deploy-storefront.ps1 -Theme Madadrang
```

```powershell
pwsh -File ./scripts/deploy-storefront.ps1 -Theme MishaShop
```

نام فنی «میشا شاپ» در کد `MishaShop` است. شناسهٔ سرویس نصب فعلی در Docker Compose برای سازگاری `boutique` باقی می‌ماند.
این دستورها برای سرور موجود با Compose شامل `madadrang`، `boutique` و `database`
هستند؛ سرور خالی را provision نمی‌کنند.

## مراحل خودکار

1. ابزارها و تنظیمات خصوصی بررسی می‌شوند. اگر Docker Desktop خاموش باشد، اسکریپت
   آن را با مهلت محدود راه‌اندازی می‌کند. موتور Linux amd64 لازم است.
2. از فایل‌های فعلی checkout، شامل تغییرات commit نشده و فایل‌های جدیدِ ignore نشده،
   یک کپی مستقل ساخته می‌شود. build و publish در آن کپی با `linux-x64` انجام می‌شود.
   این کار `pull`، `merge`، `commit` یا `push` انجام نمی‌دهد؛ ابتدا تغییرات موردنظر
   را در همین checkout داشته باشید. فایل‌های ignore شده وارد بسته نمی‌شوند.
3. image فقط برای برند انتخاب‌شده روی همین سیستم ساخته و با `docker image save`
   بسته‌بندی می‌شود. برچسب یکتا، commit و SHA256 محتوای snapshot ثبت می‌شوند.
4. helper از SSH با host key شناخته‌شده استفاده می‌کند، lock انتشار سرور را می‌گیرد،
   archive را منتقل می‌کند، SHA256 را تطبیق می‌دهد و image را با Docker بارگذاری می‌کند.
5. image و تنظیمات سرویس دیگر حفظ می‌شوند. فقط سرویس انتخاب‌شده با Compose و
   `--no-build --no-deps` دوباره راه‌اندازی می‌شود؛ volumeهای فعلی استفاده می‌شوند.
6. وضعیت Running، شمارندهٔ restart، ثبات پردازه و LISTEN بودن پورت داخلی 8080 بررسی
   می‌شود. پس از موفقیت، image جدید در override اصلی ثبت می‌شود و image قبلی همان
   فروشگاه، در صورت بی‌استفاده بودن، حذف می‌شود. فایل archive موقت سرور پاک می‌شود.

بررسی startup بالا **تست HTTP، مرورگر یا صحت فرایند خرید نیست**. هیچ درخواست HTTP،
ورود ادمین، SQL اصلاحی یا نصب دادهٔ نمونه در این مسیر اجرا نمی‌شود. فرایند عادی خود
برنامه هنگام startup همچنان طبق رفتار nopCommerce اجرا می‌شود.

اگر راه‌اندازی نسخهٔ جدید شکست بخورد، helper تلاش می‌کند image قبلی و override قبلی
را برگرداند و با exit code ناموفق خارج می‌شود. image قبلی پیش از تأیید startup
حذف نمی‌شود. این بازگشت مربوط به کانتینر و تنظیمات Compose است؛ تغییرات احتمالی
داده یا schema توسط خود برنامه را برنمی‌گرداند.

حذف image با ID دقیق، بدون `--force` و بدون `prune` است. image مورد استفادهٔ هر
کانتینر، حتی کانتینر متوقف، یا دارای tagهای دیگر حذف نمی‌شود و دلیل در گزارش ثبت
می‌شود. دیتابیس، volumeها، imageهای GitLab و نسخه‌های قدیمی نامرتبط در این پاک‌سازی
هدف قرار نمی‌گیرند. این انتشار کوتاه‌مدت سرویس انتخاب‌شده را recreate می‌کند و
انتشار بدون وقفه نیست.

## تنظیم اولیه

روی سیستم فعلی، فایل خصوصی موجود در مسیر زیر خودکار پیدا می‌شود و لازم نیست
برای هر اجرا مسیر یا رمز را دوباره بدهید:

```text
../dual-store-deployment-20261012/deploy.private.json
```

در checkout دیگر، فایل نمونهٔ
`scripts/DeployTestServer/image-config.example.json` را خارج مخزن در مسیر زیر کپی
و تنظیم کنید؛ این مسیر نسبت به فایل خصوصی موجود اولویت دارد:

```text
%LOCALAPPDATA%/NopStorefrontDeploy/deploy.private.json
```

تنظیمات شامل `host`، `username`، `remoteRoot`، `knownHostsPath` و یکی از
`handoffPath` یا `keyFilename` است. `port` اختیاری و پیش‌فرض آن 22 است.
`imageRepository` اختیاری و پیش‌فرض آن `nop-dual` است. مسیرهای نسبی فایل‌های خصوصی
نسبت به پوشهٔ config تفسیر می‌شوند. `handoffPath` به فایل خصوصی فعلی SSH اشاره
می‌کند که خط `Password: ...` دارد؛ رمز را داخل مخزن یا فایل نمونه قرار ندهید.
`knownHostsPath` باید قبلاً با host key تأییدشدهٔ سرور پر شده باشد؛ کلید ناشناس
خودکار پذیرفته نمی‌شود.

ابزارهای لازم: PowerShell 7.4 یا جدیدتر، Git، SDK سازگار با `global.json`،
Node.js/npm و Docker Desktop با موتور Linux amd64.

تمام هماهنگی انتشار در PowerShell انجام می‌شود. انتقال archive با SCP و دسترسی به فایل‌های تنظیمات با SFTP از کتابخانهٔ
[SSH.NET](https://www.nuget.org/packages/SSH.NET/2026.0.0) در همان runtime داخلی
.NET استفاده می‌کند. نسخه‌های کتابخانه و وابستگی‌هایش ثابت‌اند؛ اولین اجرا آن‌ها
را از NuGet رسمی دریافت و SHA256 را بررسی می‌کند و اجراهای بعدی از cache استفاده
می‌کنند. نصب ماژول PowerShell، Python یا Paramiko لازم نیست.

سرور به PowerShell یا Python نیاز ندارد؛ اسکریپت محلی فرمان‌های عادی Docker
Compose و ابزارهای استاندارد Linux مانند `flock`، `sha256sum`، `df` و `stat` را
از SSH اجرا می‌کند. فایل اسکریپت Python یا Bash روی سرور ارسال نمی‌شود.
آدرس سرور و پورت‌ها از config و Compose موجود گرفته می‌شوند. فایل‌های خصوصی
شامل credential و host key بیرون Git می‌مانند.

## ساختار خوانای کد

دو فایل `deploy-madadrang.ps1` و `deploy-mishashop.ps1` مسیر جدید پروژه‌های مستقل را اجرا می‌کنند.
جریان ساخت و انتشار سرور آزمایشی موجود در `deploy-storefront.ps1` با تابع‌های مستقل خوانده می‌شود:

- `Get-DeploymentConfiguration`: پیدا کردن و بررسی تنظیمات خصوصی.
- `Start-LocalDocker`: آماده کردن موتور محلی Docker.
- `New-StorefrontPackage`: ساخت بسته از snapshot فایل‌های کاری.
- `New-StorefrontImage`: ساخت image دارای شناسهٔ سورس.
- `Remove-LocalBuildArtifacts`: پاک کردن خروجی موقت همان اجرا.

`DeployTestServer/Deploy-Image.ps1` مسئول انتقال، قفل سرور، جابه‌جایی نسخه، بازگشت
در خطا و پاک‌سازی image قبلی است. `Import-StorefrontSsh.ps1` فقط کتابخانهٔ اتصال
را آماده می‌کند. پارامترهای طولانی به صورت hashtable با نام‌های واضح فرستاده
می‌شوند؛ جزئیات هر مرحله داخل تابع مربوط به خودش قرار دارند.

## گزارش و گزینه‌ها

گزارش هر اجرا در پوشهٔ یکتای زیر نگهداری می‌شود:

```text
%LOCALAPPDATA%/NopStorefrontDeploy/runs/<theme>-<time>-<id>/
```

`deployment.log` خروجی مراحل، `deployment.json` نتیجهٔ سرور و cleanup، و
`package.json` شناسهٔ بسته و fingerprint سورس را نگه می‌دارند. در صورت موفقیت،
snapshot، app منتشرشده، archive و tag موقت image روی سیستم محلی پاک می‌شوند.
در صورت شکست، فایل‌ها برای بررسی می‌مانند. خروجی‌های گزارش را پیش از اشتراک‌گذاری
بازبینی کنید؛ ابزارهای build ممکن است اطلاعات محیط را در log بنویسند.

نمونهٔ تعیین مسیر خصوصی، مهلت startup و حفظ فایل‌های build:

```powershell
pwsh -File ./scripts/deploy-storefront.ps1 -Theme MishaShop `
    -ConfigPath 'C:/private/deploy.private.json' `
    -OutputDirectory 'D:/storefront-deployments' `
    -StartupTimeout 240 `
    -KeepBuildArtifacts
```

دو اسکریپت از یک orchestrator و یک helper مشترک استفاده می‌کنند تا منطق انتشار
دو بار نوشته نشود. قفل محلی از اجرای هم‌زمان دو انتشار روی این سیستم جلوگیری
می‌کند و قفل سرور جلوی تداخل با انتشارهای همکار را می‌گیرد.
