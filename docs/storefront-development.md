# اجرای فروشگاه مدادرنگ

نسخهٔ نخست در پلاگین `Misc.PersianStorefront` و کتابخانهٔ رابط `src/Storefront/Storefront.UI` پیاده شده است. نام مدادرنگ موقت است. صفحهٔ اصلی، فهرست، محصول، سبد و دیالوگ ورود فارسی‌اند؛ مسیرهای /cart و /search نیز به همین رابط هدایت می‌شوند؛ ورود موبایلی در دیالوگ و /stationery/login است. /login اصلی برای حفظ مسیر ورود ادمین نگه داشته شده است؛ viewها، CSS و ورود ادمین تغییر نکرده‌اند. هیچ فایل هستهٔ nopCommerce تغییر نکرده است.

## اجرا روی این دستگاه

راه‌اندازی مجدد در ۲۰۲۶-۱۰-۰۸: SDK پایهٔ `global.json` با نسخهٔ نصب‌شدهٔ `10.0.301` هماهنگ شد. PostgreSQL 17 محلی روی پورت 5432 استفاده می‌شود. نصب روی پایگاه مستقل `nop_stationery_dev` با extension `citext` انجام شد. برای دسترسی به مشخصات اتصال و حساب مدیر، فایل `%LOCALAPPDATA%\MadadrangDev\local-settings.json` را باز کنید؛ کلیدهای `AdminEmail` و `AdminPassword` مشخصات ورود مدیر هستند. این فایل خارج از مخزن است.

آدرس فروشگاه `http://localhost:5090/`، پنل مدیر `http://localhost:5090/admin`، ورود مدیر `/login` و ورود مشتری `/stationery/login` است. نصب Development پلاگین، داده‌های نمونهٔ فارسی را ایجاد می‌کند. برای نصب دستی پلاگین روی پایگاه نصب‌شده، `scripts/seed-storefront.ps1` ایمیل مدیر و رمز از نوع SecureString می‌گیرد؛ سپس برنامه باید در Development دوباره اجرا شود.

از ریشهٔ مخزن:

```powershell
./scripts/run-storefront.ps1
```

فروشگاه: `http://localhost:5090`، Swagger: `/stationery/swagger`، Scalar: `/stationery/scalar`، سند OpenAPI: `/stationery/openapi/v1.json`. مستندات API و OTP آزمایشی فقط در Development فعال‌اند. build پلاگین، پروژهٔ nopCommerce و UI را هم می‌سازد. Node/npm و .NET 10 لازم‌اند؛ npm از lockfile نصب و Tailwind را با تغییر منابع بازتولید می‌کند. خروجی CSS نیز در مخزن ثبت شده است.

PostgreSQL موجود روی localhost:5432 استفاده می‌شود. برای جداسازی اطلاعات، پایگاه `nop_stationery_dev` با extension `citext` ساخته شده؛ پایگاه `FileManagement` دست‌نخورده است. تنظیم اتصال nopCommerce در فایل محلی و ignored `src/Presentation/Nop.Web/App_Data/appsettings.json` است؛ کلید `DefaultConnection` نمونهٔ کاربر عیناً قرارداد تنظیمات nopCommerce نیست. مشخصات اتصال و ورود مدیر فقط در `%LOCALAPPDATA%\MadadrangDev\local-settings.json` نگهداری شده‌اند و در Git نیستند.

برای checkout تازه ابتدا `git submodule update --init`، سپس build پلاگین، نصب nopCommerce با PostgreSQL و نصب پلاگین از Local plugins در ادمین انجام شود. نصب پلاگین در Development هشت کالای نمونه، رنگ‌ها، تصاویر و واحد IRT را می‌سازد؛ در Production دادهٔ نمونه نمی‌سازد. تغییر تنظیم ارز و نیاز به نصب داده‌های نمونه را پیش از نصب روی فروشگاه موجود بررسی کنید.

## فرمت‌بندی کد

مرجع قواعد، `.editorconfig` ریشهٔ مخزن است؛ این فایل با نسخهٔ upstream یکسان است. [مستندات رسمی nopCommerce](https://docs.nopcommerce.com/en/developer/tutorials/coding-standards.html) همین قواعد و پشتیبانی Visual Studio از EditorConfig را معرفی می‌کند؛ formatter اختصاصی دیگری در مخزن یا CI تعریف نشده است.

برای فایل‌های C# سه پروژهٔ فروشگاه، از ابزار رسمی مایکروسافت همراه SDK استفاده کنید:

```powershell
./scripts/format-storefront.ps1
./scripts/format-storefront.ps1 -Verify
```

فرمان دوم بررسی می‌کند که فرمت و اصلاح‌های خودکار سبک C# تغییر دیگری لازم نداشته باشند. پروژه‌های مرجع و submodule فرمت نمی‌شوند. خطای نام‌گذاری `IDE1006` از اجرای دسته‌ای مستثناست، چون ابزار برای آن Fix All ندارد؛ قواعد نام‌گذاری همچنان در `.editorconfig` و ویرایشگر فعال‌اند. این فرمان بررسی همهٔ قراردادهای معماری یا نام‌گذاری نیست.

برای `.razor` و `.cshtml` از **Format Document** افزونهٔ رسمی `ms-dotnettools.csharp` استفاده کنید؛ `.vscode/settings.json` همین formatter را پیش‌فرض کرده و ذخیرهٔ فایل نیز آن را اجرا می‌کند. `dotnet format` جایگزین formatter قالب Razor نیست. اجرای دسته‌ای این تغییر با API رسمی VS Code، یعنی `vscode.executeFormatDocumentProvider`، انجام شد؛ اجراکننده فقط TextEditهای formatter مایکروسافت را اعمال کرد و قواعد یا formatter سفارشی نداشت. CSS و JavaScript با formatterهای داخلی Microsoft VS Code فرمت شدند؛ CSS تولیدشدهٔ Tailwind باید با build بازتولید شود.

قواعد اصلی: چهار فاصله در C#، دو فاصله در JS/CSS و فایل‌های پروژه، `using`های System در ابتدا، آکولادهای چندخطی در خط جدید، ترجیح `var` و namespace فایل‌محور. متدها بدنهٔ بلوکی دارند و propertyهای ساده می‌توانند expression body داشته باشند. `csharp_preserve_single_line_blocks = true` باعث می‌شود formatter بعضی بلوک‌های تک‌خطی موجود را حفظ کند. نام نوع‌ها و اعضای عمومی PascalCase، فیلدهای خصوصی `_camelCase` و نام متدهای async دارای پسوند `Async` است؛ نام‌گذاری از فرمت فاصله‌ها مستقل است.

محدودیت‌های مشاهده‌شده در بررسی ۲۰۲۶-۱۰-۰۸:

- C# با SDK رسمی `10.0.401` و قالب‌های Razor با افزونهٔ Microsoft C# نسخهٔ `2.160.4` فرمت شدند. اجرای بعدی formatter روی قالب‌های تغییرکرده، CSS و JavaScript تغییر دیگری ایجاد نکرد.
- `StorefrontRoot.razor` مستثنا ماند: نسخه‌های رسمی `2.160.4` و `2.140.9` در عبارت شرطی چندخطیِ شمارش نتایج، با هر اجرا تورفتگی بیشتری اضافه کردند. خروجی این دو آزمایش برگردانده شد؛ فایل دستی فرمت نشده و فرمت موفق آن ادعا نمی‌شود. نسخهٔ قبلی فقط در مسیر موقت نصب شد و افزونه‌های اصلی کاربر تغییر نکردند.
- در `Storefront.Preview/Program.cs`، بررسی SDK `10.0.401` برای انتهای فایل هم‌زمان `WHITESPACE` (افزودن خط جدید) و `FINALNEWLINE` (حذف همان خط جدید) گزارش می‌دهد. بنابراین `-Verify` در وضعیت فعلی برای این فایل exit غیرصفر دارد، هرچند بررسی پلاگین و `Storefront.UI` موفق است. این تعارض با تغییر قواعد upstream یا فرمت دستی پنهان نشده است.
- هر دو پروژهٔ پلاگین و پیش‌نمایش بعد از فرمت build شدند. نام‌گذاری و فایل‌های XML پروژه جزو اصلاح‌های انجام‌شده نیستند.

## معماری و اتصال

MVC فقط میزبان HTML اولیهٔ کامپوننت Blazor است. پلاگین سرویس‌ها، Blazor Hub، مسیرها و assets را ثبت می‌کند. middleware فایل‌های UI پیش از routing اجرا می‌شود؛ در غیر این صورت مسیر catch-all nopCommerce فایل framework را می‌بلعد. اسکریپت رسمی `blazor.server.js` از بستهٔ Microsoft.AspNetCore.App.Internal.Assets نسخهٔ 10.0.12 همراه پلاگین کپی می‌شود؛ هنگام ارتقای runtime این نسخه هم بررسی شود.

رویدادهای Blazor از JavaScript و درخواست HTTP هم‌مبدأ با cookie مشتری و antiforgery token به API پلاگین می‌رسند. قیمت و موجودی، XML ویژگی‌ها و سبد با خدمات اصلی nopCommerce محاسبه می‌شوند؛ وابستگی به HttpContext قدیمی circuit وجود ندارد. ورود از `SignInCustomerAsync` استفاده می‌کند که سبد مهمان را منتقل می‌کند. پس از ورود، صفحه دوباره بارگذاری می‌شود تا token مربوط به هویت جدید تولید شود.

حساب موبایلی برای سازگاری با cookie authentication فعلی یک شناسهٔ ایمیل داخلی در دامنهٔ رزروشدهٔ `accounts.invalid` و سابقهٔ رمز تصادفی غیرقابل‌استفاده دریافت می‌کند. کاربر ایمیل یا رمز وارد نمی‌کند. رمز مدیر و تنظیم عمومی UsernamesEnabled تغییر نمی‌کنند. این سازگاری برای نمونهٔ Development است؛ اتصال پنل واقعی، سیاست بازیابی حساب و پیام‌های ایمیل پیش از فروش واقعی باید طراحی شوند.

## Tailwind و BB

BB 4.1.0 از NuGet اجرا می‌شود؛ submodule فقط مرجع سورس pinned است. دکمه، Sheet، Dialog، DirectionProvider و PortalHost از BB هستند. کارت، انتخاب ویژگی، هدر و شبکهٔ محصولات اجزای اختصاصی فروشگاه‌اند. ویژگی رنگ به شکل swatch و ویژگی‌های دیگر به شکل گزینهٔ متنی نمایش داده می‌شوند. BB CSS آماده با پیشوند `bb:` جدا از CSS Tailwind 4.1.18 بارگذاری می‌شود؛ سورس BB در Tailwind اسکن نمی‌شود. رنگ‌ها و فونت در متغیرهای ابتدای `Styles/input.css` قابل تغییرند. Vazirmatn محلی با OFL استفاده شده است.

## حدود نسخهٔ نخست

این نسخه مسیر مشاهده، انتخاب، سبد و ورود آزمایشی را پوشش می‌دهد. پرداخت، ارسال و ثبت سفارش نهایی هنوز متصل نیستند. داده و تصاویر محصولات نمونه‌اند؛ تصاویر الزاماً عکس دقیق کالای نام‌برده نیستند. کاتالوگ فعلاً حداکثر 200 محصول را می‌خواند و فیلترها در UI اجرا می‌شوند؛ پیش از کاتالوگ بزرگ باید صفحه‌بندی و فیلتر سمت سرور اضافه شود. مدل‌های پشتیبانی‌شده ویژگی‌های گزینه‌ای ساده‌اند؛ فایل، متن آزاد، اجاره، gift card و grouped products در این فاز نیستند. مبالغ نمونه در IRT ذخیره شده‌اند؛ برای دادهٔ ریالی باید تبدیل واحد صریح اضافه شود. جمع فعلی سبد جمع اقلام است، مالیات/ارسال/تخفیف کل سفارش در checkout بعدی محاسبه می‌شوند.

Mock OTP تصادفی است، به مهمان و موبایل وابسته است، دو دقیقه عمر، پنج تلاش و محدودیت درخواست یک‌دقیقه‌ای دارد؛ hash کد در حافظه نگهداری می‌شود و پس از موفقیت مصرف می‌شود. Restart کدها را پاک می‌کند. کد صرفاً در Development به کاربر نمایش داده می‌شود و پیامک واقعی نمی‌رود. این سرویس محیط واقعی نیست؛ پنل فعلی کاربر در مرحلهٔ بعد از طریق قرارداد قابل‌تعویض متصل شود.

## منابع طراحی و دارایی‌ها

- ساختار سبد از [shopping-cart1 در shadcnblocks](https://www.shadcnblocks.com/block/shopping-cart1) و صفحهٔ [مرجع درخواستی کاربر](https://www.shadcnblocks.com/blocks/shopping-cart) اقتباس شده است: اقلام، انتخاب تعداد و کارت خلاصهٔ جدا. پیاده‌سازی React مستقیماً اجرا نشده؛ تعامل‌ها با Blazor/BB ساخته شده‌اند.
- الگوی فرم کوتاه ورود از [shadcn login blocks](https://ui.shadcn.com/blocks/login) الهام گرفته است. هدر و صفحهٔ اصلی ترکیب همین الگوهای متداول فروشگاهی با هویت رنگی مصوب‌اند، نه نسخهٔ عین‌به‌عین یک بلوک پولی.
- [مجوز shadcnblocks](https://www.shadcnblocks.com/license)؛ این پروژه فروشگاه نهایی است، کتابخانهٔ توزیع مجدد بلوک‌ها نیست.
- تصاویر نمونه از Unsplash؛ شناسه‌ها: `1513542789411-b6a5d4f31634`، `1531346878377-a5be20888e57`، `1585336261022-680e295ce3fe`، `1517842645767-c639042777db`، `1513364776144-60967b0f800f`، `1455390582262-044cdead277a`، `1506784983877-45594efa4cbe`. [مجوز](https://unsplash.com/license). تصاویر محلی‌اند و در runtime به سرویس بیرونی وابسته نیستند.
- آیکون‌ها بر مبنای [Lucide با مجوز ISC](https://github.com/lucide-icons/lucide/blob/main/LICENSE)، فونت [Vazirmatn](https://github.com/rastikerdar/vazirmatn) با فایل OFL همراه، BB تحت Apache-2.0 و ASP.NET Core تحت MIT.

گزارش اجرای تست و تصاویر در `docs/storefront-qa.md` ثبت می‌شوند.
