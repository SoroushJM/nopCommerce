# طرح ادغام Blazor Blueprint و Tailwind

وضعیت: یادداشت فنی پیش از پیاده‌سازی و آزمایش؛ راه‌اندازی هنوز انجام نشده است.

## مسیر اولیه

- قالب اختصاصی nopCommerce، viewهای میزبان MVC را فراهم کند.
- کامپوننت‌های Blazor در پروژه/پلاگین اختصاصی کامپایل شوند؛ فایل‌های razor صرفاً با قرار گرفتن در پوشهٔ قالب MVC کامپایل نمی‌شوند.
- سرویس‌ها و endpointهای لازم Blazor از نقاط توسعهٔ nopCommerce ثبت شوند؛ امکان کامل این مسیر با نمونهٔ اجرایی بررسی شود.
- BB از بستهٔ NuGet با نسخهٔ مشخص استفاده شود، مگر آزمایش نیاز دیگری را روشن کند. submodule نقش مرجع محلی سورس را دارد.

## CSS و build

- BB v4 فایل آمادهٔ `_content/BlazorBlueprint.Components/blazorblueprint.css` دارد؛ utilityهای آن پیشوند `bb:` دارند.
- کامپوننت‌های اختصاصی فروشگاه build جداگانهٔ Tailwind داشته باشند؛ صرف وجود CSS آمادهٔ BB تضمین نمی‌کند کلاس‌های دلخواه ما موجود باشند.
- متغیرهای معنایی رنگ، فونت و شعاع گوشه‌ها میان BB و اجزای فروشگاه مشترک باشند؛ theme variables پیش از CSS کتابخانه بارگذاری شوند.
- source scanning فقط فایل‌های رابط اختصاصی razor و cshtml و منابع لازم را پوشش دهد. طبق THEMING.md مخزن BB، سورس یا بستهٔ BB نباید با @source به build خودمان اضافه شود؛ CSS آمادهٔ کتابخانه جدا بارگذاری شود.
- کلاس‌های متغیر رنگ/وضعیت با نگاشت کلاس‌های کامل تعریف شوند تا Tailwind آن‌ها را هنگام build تشخیص دهد.
- CSS فروشگاه و BB فقط در layout فروشگاه بارگذاری شود؛ pipeline و ظاهر ادمین تغییر نکند.
- reset/preflight، ترتیب لایه‌های CSS و portalهای BB در نمونهٔ واقعی بررسی شوند؛ پیشوند utility به‌تنهایی تضمین جداسازی همهٔ CSS نیست.
- در base layer مربوط به Tailwind خودمان، قانون border-color: var(--border) تعریف شود؛ preflight می‌تواند آن را به currentColor بازنشانی کند. اگر حالت تاریک اضافه شد، dark variant مبتنی بر کلاس با BB هماهنگ شود.
- خروجی CSS در publish حضور داشته باشد و CI/build همان نسخه‌های قفل‌شدهٔ ابزار را استفاده کند.

## معیارهای آزمایش نخست

- صفحهٔ محصول فارسی و RTL همراه با کامپوننت BB و یک جزء اختصاصی Tailwind نمایش داده شود.
- رنگ/مدل انتخاب‌شده قیمت صحیح را نمایش دهد و همان ترکیب به سبد nopCommerce اضافه شود.
- مشتری و سبد در refresh و ناوبری حفظ شوند؛ مسیر Blazor نباید به HttpContext فعال در تمام رویدادهای تعاملی متکی باشد.
- متن و اطلاعات اولیهٔ محصول در HTML پاسخ برای بررسی قابلیت ایندکس شدن موجود باشند.
- کنترل کیبورد، فوکوس، RTL و نمایش موبایل بررسی شوند.
- صفحهٔ ادمین CSS جدید فروشگاه را بارگذاری نکند و ظاهر آن ثابت بماند.

## منابع

- https://blazorblueprintui.com/docs/installation
- https://blazorblueprintui.com/docs/theming
- https://learn.microsoft.com/en-us/aspnet/core/blazor/components/integration?view=aspnetcore-10.0
- https://tailwindcss.com/docs/theme

جزئیات باید با نسخه و commit واقعی BB تطبیق داده شوند؛ این سند تأیید اجرای موفق ادغام نیست.

سورس محلی بررسی‌شده: external/blazor-blueprint/THEMING.md در commit 667809bdcd85b698423c7465e72ce613ce1803a2.
