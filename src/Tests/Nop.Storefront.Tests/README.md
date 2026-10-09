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

فقط HTTP روی loopback پذیرفته می‌شود؛ این مجموعه برای stage یا production طراحی نشده است. وب‌سایت را خود تست اجرا نمی‌کند. fixture محصول باید ترکیب موجود و ناموجود داشته باشد؛ تست API قدیمی به انتخاب‌های نمونهٔ سبز و نارنجی نیاز دارد. SMS واقعی در این آزمایش استفاده نمی‌شود. سناریوهای عادی به credential ادمین یا اتصال مستقیم پایگاه‌داده نیاز ندارند؛ سیزده سناریوی مدیریتی زیر فقط با فعال‌سازی صریح اجرا می‌شوند.

- `NativeProductTests`: تمام بررسی‌های `test-native-product.ps1` شامل SSR، WASM، antiforgery، قیمت بومی، رد موجودی صفر و سبد مهمان پایدار؛ بازیابی انتخاب‌های ویرایش و ادغام بومی دو ردیف هم‌انتخاب.
- `NativeProductPresentationTests`: ایجاد محصول و دو تصویر از ادمین بومی، گالری/SEO/حداقل/قیمت پلکانی، ویرایش و مالکیت سبد، رد موجودی و حداکثر تعداد؛ پاک‌کردن تصاویر/سبد/محصول و بازگرداندن تنظیم zoom در `finally`.
- `NativeProductCombinationPresentationTests`: محصول موقت با سه تصویر و ترکیب دارای اطلاعات/خالی/بدون ترکیب؛ پاسخ SKU/MPN/GTIN، fallback به محصول پایه، PictureId و URL واقعی، flag قرارداد WASM و قیمت پایهٔ غیرخالی/`null` بدون reload فروشگاه. ترکیب‌ها قبل از mapping و تصاویر پاک می‌شوند؛ هیچ تنظیم عمومی یا gate مرورگری ندارد. پاسخ HTTP مدرک تغییر DOM نیست.
- `NativeProductPolicyTests`: سه روش آزمون برای تخفیف واقعی/CallForPrice، مخفی‌شدن قیمت و tier با مجوز بومی و بازگرداندن دقیق نقش‌ها، و هفت حالت fallback پس از نقد با عنوان خالی. هفت حالت داخل یک روش اجرا می‌شوند؛ خرید موفق در همهٔ آن‌ها یا حفظ متن نقد ادعا نمی‌شود.
- `StorefrontApiTests`: تمام رفتارهای `test-storefront.ps1` شامل قیمت ویژگی، موجودی، OTP و cooldown/replay، مهاجرت و ادغام سبد، مالکیت، تغییر تعداد، حذف و Swagger/Scalar.
- `PersianAdminLocalizationTests`: تمام قراردادهای `validate-admin-localization.py` شامل کلیدها، placeholderها، ترتیب HTML، tokenها و پوشش منابع؛ XMLها در خروجی پروژه کپی می‌شوند و اجرا به cwd وابسته نیست.
- `NativeHomepageTests`: HTML اولیهٔ بومی، لینک‌های محصول و دسته، تصاویر و جلوگیری از اجرای repair توسط مهمان.
- `NativeCategoryAdministrationTests`، `NativeMenuAdministrationTests` و `NativeFixtureRepairTests`: ایجاد/ویرایش/لغو انتشار دسته و منو از ادمین بومی، پاک‌سازی رکوردهای ساخته‌شده و idempotence عملیات repair.
- `NativeCatalogTests`: نتایج SSR، برابری جست‌وجوی بومی و فهرست عمومی، قیمت، کنترل‌های نامعتبر و اتصال مسیرهای اصلی/قدیمی به صفحات بومی.
- `NativeCatalogAdministrationTests`: ساخت ۲۰۱ محصول و دو دسته از ادمین بومی، صفحهٔ آخر، جست‌وجوی SKU آخر، مرتب‌سازی قیمت، محصول چنددسته‌ای و برابری فیلترهای ترکیبی؛ حذف دسته‌ها و محصولات آزمون از ادمین.
- `NativeCatalogSettingsTests`: خاموش‌کردن فیلترهای قیمت/برند/مشخصات از تنظیمات بومی، حذف کنترل‌ها و بازگرداندن همهٔ مقادیر اصلی در `finally`.
- `NativeCartTests`: تغییر تعداد و حذف بومی، رد تعداد نامعتبر بدون تغییر سبد ذخیره‌شده، مالکیت مشتری و CSRF، برابری قیمت‌های فرمت‌شدهٔ پاسخ پنل با HTML بومی، حفظ هشدار تلاش ناموفق و تبدیل‌نشدن پاسخ کوپن به JSON.
- `NativeCheckoutAttributesTests`: ساخت ویژگی‌های checkbox، تاریخ، متن، فایل و فقط‌خواندنی از ادمین بومی، ذخیرهٔ انتخاب‌ها، تغییر تعداد با فیلدهای پنل و بررسی حفظ همهٔ انتخاب‌ها؛ پاک‌کردن سبد مهمان و ویژگی‌های ساخته‌شده در `finally`.
- `NativeCartSettingsTests`: تغییر تنظیمات بومی محدودیت تعداد ردیف پنل، تصاویر، روشن‌بودن mini-cart و checkout از ادمین؛ بررسی حفظ تعداد/زیرجمع در cap صفر و وجود سبد کامل هنگام خاموش‌بودن پنل؛ بازگرداندن همهٔ تنظیمات در `finally`.
- `NativeCartAllowedQuantityTests`: ساخت محصول موقت با تعدادهای مجاز ۲/۴ و موجودی ۳، رد انتخاب ۴ و حفظ تعداد ۲/مبلغ/انتخاب ذخیره‌شده؛ حذف سبد و محصول آزمون از مسیر بومی در `finally`.

برای سیزده سناریوی مدیریتی، `STOREFRONT_TEST_ADMIN_FIXTURE=1` را تنظیم کنید. helper اطلاعات محلی را از `%LOCALAPPDATA%/MadadrangDev/local-settings.json` می‌خواند؛ این فایل خارج از Git است. قبل از هر تغییر، وجود فرم اختصاصی repair را در پاسخ ادمین بررسی می‌کند. سرور این فرم را فقط در Development، روی loopback و با پایگاه مؤثر PostgreSQL به نام `nop_stationery_dev` نشان می‌دهد. این سناریوها بدون فعال‌سازی صریح skip می‌شوند؛ مجموعه در مجموع ۳۹ سناریو دارد. ورود و تغییرات از HTTP بومی ادمین انجام می‌شوند و تست‌ها مستقیماً به پایگاه‌داده وصل نمی‌شوند. مجموعهٔ کامل در ۲۰۲۶-۱۰-۱۰ به وقت تهران با ۳۹ تست موفق و بدون شکست یا skip در ۳۲٫۶۳۹ ثانیه اجرا شد. فایل بارگذاری‌شدهٔ آزمون checkout یک رکورد Download آزمایشی در پایگاه توسعه باقی می‌گذارد؛ خود ویژگی و سبد پاک می‌شوند.

برای بررسی بصری Select یا گالری با محصول موقت همان آزمون، فقط در اجرای هدفمند `NativeCartAllowedQuantityTests` یا `NativeProductPresentationTests` می‌توان `STOREFRONT_TEST_UI_GATE` را به یک پیشوند فایل تازه در پوشهٔ Temp محلی تنظیم کرد. آزمون پس از بررسی HTTP، شناسهٔ محصول را در `<prefix>.ready` می‌نویسد و حداکثر هشت دقیقه منتظر `<prefix>.done` می‌ماند؛ پس از حذف ردیف موقت از سبد مرورگر، فایل done را ایجاد کنید تا پاک‌سازی ادمین اجرا شود. اجرای عادی هیچ مکثی ندارد. اطلاعات ورود در این فایل‌ها نوشته نمی‌شوند.

دو روش `AppliedDiscountAndCallForPriceKeepNativePurchaseRules` و `GuestPricePermissionHidesPricesAndRestoresExactRoleMappings` نیز gate بصری دارند. هر بار فقط یک روش هدفمند را با پیشوند Temp تازه اجرا کنید؛ فایل done روش قبلی نباید مکث روش بعدی را آزاد کند. آزمون مجوز، پس از اثبات HTTP برای Guests، فقط در gate اختیاری نقش Registered را نیز از DisplayPrices حذف می‌کند تا حساب عادی موجود در مرورگر آزمایش شود. Administrator و سایر نقش‌ها حفظ می‌شوند؛ کل مجموعهٔ نقش‌های اصلی در `finally` دقیقاً بازگردانده و بررسی می‌شود.

سناریوهای HTTP دارای session مستقل هستند و با کلید `storefront-http` هم‌زمان اجرا نمی‌شوند. سبدهای آزمایشی در `finally` پاک می‌شوند. آزمون OTP برای یک شمارهٔ تصادفی `0900…` حساب mock می‌سازد؛ حساب نمونه پس از آزمون در پایگاه توسعه باقی می‌ماند و سبدش خالی می‌شود. این همان اثر تست پیشین است؛ حذف حساب یا seed مجدد خودکار انجام نمی‌شود.

گزارش TRX و HTML در `bin/<configuration>/net10.0/TestResults` تولید می‌شود. برای اجرای فقط اعتبارسنجی ترجمه‌ها، بدون وب‌سایت:

```powershell
dotnet run --project src/Tests/Nop.Storefront.Tests -- --treenode-filter '/*/*/PersianAdminLocalizationTests/*'
```

مطابق تصمیم جدید کاربر، بررسی‌های بصری و کلیک‌های دسکتاپ/موبایل به خود کاربر سپرده شده‌اند؛ Luna یا computer-use برای ادامه اجرا نمی‌شوند. gateهای قدیمی در اجرای عادی و اجرای بخش ۲ غیرفعال‌اند. ابزارهای اجرا، قالب‌بندی و seed همچنان PowerShell هستند.
