# Storefront TUnit tests

این پروژهٔ مستقل با .NET 10 و TUnit 1.73.19 اجرا می‌شود. تست‌های NUnit اصلی nopCommerce و runner سراسری مخزن تغییر نکرده‌اند. مطابق [راهنمای TUnit](https://tunit.dev/docs/getting-started/installation/)، entry point توسط بسته تولید می‌شود و پروژه نیازی به `Microsoft.NET.Test.Sdk` ندارد.

ابتدا فروشگاه محلی را با پلاگین، Theme فعال Madadrang، داده‌های نمونه و محیط `Development` اجرا کنید:

```powershell
./scripts/run-storefront.ps1
```

سپس در ترمینال دیگر، از ریشهٔ مخزن:

```powershell
dotnet run --project src/Tests/Nop.Storefront.Tests -- --report-trx
```

تست‌های HTTP به‌صورت پیش‌فرض به `http://localhost:5090/` و محصول نمونهٔ `1` متصل می‌شوند. تغییر آدرس یا محصول از متغیرهای زیر ممکن است:

```powershell
$env:STOREFRONT_TEST_URL = 'http://localhost:5090/'
$env:STOREFRONT_TEST_PRODUCT_ID = '1'
```

فقط HTTP روی loopback پذیرفته می‌شود؛ این مجموعه برای stage یا production طراحی نشده است. وب‌سایت را خود تست اجرا نمی‌کند. fixture محصول باید ترکیب موجود و ناموجود داشته باشد؛ تست API قدیمی به انتخاب‌های نمونهٔ سبز و نارنجی نیاز دارد. SMS واقعی در این آزمایش استفاده نمی‌شود. سناریوهای عادی به credential ادمین یا اتصال مستقیم پایگاه‌داده نیاز ندارند؛ سه سناریوی مدیریتی زیر فقط با فعال‌سازی صریح اجرا می‌شوند.

- `NativeProductTests`: تمام بررسی‌های `test-native-product.ps1` شامل SSR، WASM، antiforgery، قیمت بومی، رد موجودی صفر و سبد مهمان پایدار.
- `StorefrontApiTests`: تمام رفتارهای `test-storefront.ps1` شامل قیمت ویژگی، موجودی، OTP و cooldown/replay، مهاجرت و ادغام سبد، مالکیت، تغییر تعداد، حذف و Swagger/Scalar.
- `PersianAdminLocalizationTests`: تمام قراردادهای `validate-admin-localization.py` شامل کلیدها، placeholderها، ترتیب HTML، tokenها و پوشش منابع؛ XMLها در خروجی پروژه کپی می‌شوند و اجرا به cwd وابسته نیست.
- `NativeHomepageTests`: HTML اولیهٔ بومی، لینک‌های محصول و دسته، تصاویر و جلوگیری از اجرای repair توسط مهمان.
- `NativeCategoryAdministrationTests`، `NativeMenuAdministrationTests` و `NativeFixtureRepairTests`: ایجاد/ویرایش/لغو انتشار دسته و منو از ادمین بومی، پاک‌سازی رکوردهای ساخته‌شده و idempotence عملیات repair.

برای سه سناریوی مدیریتی، `STOREFRONT_TEST_ADMIN_FIXTURE=1` را تنظیم کنید. helper اطلاعات محلی را از `%LOCALAPPDATA%/MadadrangDev/local-settings.json` می‌خواند؛ این فایل خارج از Git است. قبل از هر تغییر، وجود فرم اختصاصی repair را در پاسخ ادمین بررسی می‌کند. سرور این فرم را فقط در Development، روی loopback و با پایگاه مؤثر PostgreSQL به نام `nop_stationery_dev` نشان می‌دهد. این سناریوها بدون فعال‌سازی صریح skip می‌شوند؛ مجموعه در مجموع ۲۱ سناریو دارد. ورود و تغییرات از HTTP بومی ادمین انجام می‌شوند و تست‌ها مستقیماً به پایگاه‌داده وصل نمی‌شوند.

سناریوهای HTTP دارای session مستقل هستند و با کلید `storefront-http` هم‌زمان اجرا نمی‌شوند. سبدهای آزمایشی در `finally` پاک می‌شوند. آزمون OTP برای یک شمارهٔ تصادفی `0900…` حساب mock می‌سازد؛ حساب نمونه پس از آزمون در پایگاه توسعه باقی می‌ماند و سبدش خالی می‌شود. این همان اثر تست پیشین است؛ حذف حساب یا seed مجدد خودکار انجام نمی‌شود.

گزارش TRX و HTML در `bin/<configuration>/net10.0/TestResults` تولید می‌شود. برای اجرای فقط اعتبارسنجی ترجمه‌ها، بدون وب‌سایت:

```powershell
dotnet run --project src/Tests/Nop.Storefront.Tests -- --treenode-filter '/*/*/PersianAdminLocalizationTests/*'
```

بررسی‌های بصری و کلیک‌های دسکتاپ/موبایل با Luna و computer-use جداگانه ادامه دارند؛ انتقال فعلی شامل تست‌های خودکار اسکریپتی است. ابزارهای اجرا، قالب‌بندی و seed همچنان PowerShell هستند.
