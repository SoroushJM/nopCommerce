# تکمیل صفحهٔ محصول بومی

وضعیت ۲۰۲۶-۱۰-۰۹: این نقشهٔ اجرایی read-only توسط Sol با مدل `gpt-6.1-sol/xhigh` از کد واقعی تهیه شده است. پیاده‌سازی و بازبینی مستقل این بخش هنوز انجام نشده‌اند. معماری قبلیِ محصول ساده، انتخاب رنگ، قیمت بومی و افزودن به سبد مبنای کار باقی می‌ماند.

## نمایش و قرارداد

قرارداد `NativeProduct` با مدل‌های کوچک تصویر و قیمت پلکانی گسترش می‌یابد. تصاویر شامل شناسه، URL تصویر/thumbnail/full-size و alt/title بومی هستند؛ `PictureModels` خودش تصویر نخست را دارد و نباید default دوباره prepend شود. flags zoom و محدودبودن تصاویر به ترکیب ویژگی‌ها حفظ می‌شوند. قیمت‌ها، قیمت پایه، قیمت تخفیفی اولیه و جدول پلکانی همگی رشتهٔ فرمت‌شدهٔ سرور می‌مانند.

نگاشت از `ProductDetailsModel` انجام می‌شود: `ProductPrice.PriceWithDiscount` قیمت اصلی اولیه در صورت وجود، `Price` و `OldPrice` مرجع‌های جدا، `BasePricePAngV` قیمت واحد پایه، `TierPrices` جدول بومی، `StockAvailability` پیام واقعی موجودی، flags نمایش SKU/MPN/GTIN و فروشنده، همهٔ تولیدکنندگان، تحویل/ارسال رایگان، minimum quantity و پیش‌سفارش. URL دسته/تولیدکننده/فروشنده با `INopUrlHelper` ساخته می‌شود. متن مالیات/ارسال طبق `_ProductPrice.cshtml` و `IWorkContext.GetTaxDisplayTypeAsync()` آماده می‌شود. قیمت پنهان و call-for-price قابلیت نمایشی عادی هستند و فقط به خاطر فقدان مبلغ به fallback نخواهند رفت.

پاسخ بومی تغییر ویژگی شامل stock، SKU/MPN/GTIN، base-price، free-shipping، تصویر اصلی/full-size، pictureIds و message در کامپوننت اعمال می‌شود. response تازه زیر guard نسخهٔ انتخاب معتبر است؛ مقدار خالی metadata باید مقدار قبلی را پاک کند. موجودبودن دکمه از دادهٔ بولی معتبر می‌آید و از متن ترجمه‌شده استخراج نمی‌شود.

endpoint بومی تغییر ویژگی عمداً unit price را برای quantity=1 می‌گیرد. جدول پلکانی نمایش داده می‌شود، ولی تغییر quantity ادعای قیمت پلکانی پویا ندارد؛ قیمت خرید و جمع واقعی در سبد بومی محاسبه می‌شوند. quantity عددی مستقیم همراه دکمه‌های فعلی و allowed quantities بومی پشتیبانی می‌شود؛ سقف دلخواه یا clamp پنهانی اضافه نمی‌شود.

## محتوا و تعامل

Hero دو ستونی رنگی و انتخاب‌گرهای BB حفظ می‌شوند. thumbnail strip و zoom با BB Dialog اضافه می‌شوند. pictureIds فقط وقتی تنظیم بومی فعال است thumbnailها را فیلتر می‌کند؛ لیست خالی همه را نشان می‌دهد و تصویر بازگشتی بیرون از gallery نیز نمایش می‌یابد. جدول قیمت پلکانی با قاعدهٔ بومی، یعنی حذف تنها ردیف با quantity<=1، نمایش داده می‌شود.

محتوای غنی در `Views/NativeProduct.cshtml` روی سرور باقی می‌ماند: full description، specifications، tags، review overview و فرم reviews، related/also-purchased، و partialهای video/3D/sample-download/back-in-stock. Viewهای اصلی و widgetها دوباره استفاده می‌شوند؛ HTML/اسکریپت پلاگین داخل DTO قرار نمی‌گیرد. فرم نظر بیرون فرم خرید است. اگر wishlist یا estimate shipping نگه داشته شوند، یک `#product-details-form` واقعی با فیلدهای هماهنگ ویژگی/quantity/edited-item و eventهای بومی ایجاد می‌شود؛ partial ارسال فرم همین شناسه را serialize می‌کند.

ویرایش سبد از GET و POST بومی استفاده می‌کند: restoration انتخاب و quantity، ownership، merge با ردیف هم‌انتخاب و خطای موجودی بر عهدهٔ nopCommerce است. label به‌روزرسانی سبد و label/date پیش‌سفارش از منابع بومی گرفته می‌شوند. breadcrumb flag رعایت و JSON-LD آن به head اضافه می‌شود؛ SEO اصلی موجود حفظ می‌شود.

## fallback و پذیرش

فیلتر fallback فقط نتیجهٔ واقعی `ProductController.ProductDetails`، Theme Madadrang و `ProductTemplate.Simple` را بررسی می‌کند. نوع SimpleProduct شرط است. rental، gift-card، customer-entered-price، quantity اختصاصی مقدار ویژگی و کنترل‌های خرید پیاده‌نشدهٔ چندمقداری/متن/تاریخ/فایل به فرم کامل بومی fallback می‌کنند. ویرایش wishlist تا داشتن action صحیح wishlist بومی می‌ماند. Gallery، تخفیف، tier-price، حداقل تعداد، delivery، full description و reviews دلیل fallback نیستند.

TUnit باید render اولیهٔ تخفیف/جدول/gallery/metadata/SEO، پاسخ دقیق ویژگی و clearing، حداقل/allowed/max/stock و مقدار پلکانی سبد، ویرایش/merge/ownership و fallbackهای واقعی را بررسی کند. سپس بازبین مستقل Sol و Luna روی دسکتاپ/موبایل thumbnail/zoom، tier table، quantity، stock، edit و کنترل‌های ارسال/نظر را آزمایش می‌کنند. محدودیت ساخت agent تازه در ابزار فعلی گزارش شده است؛ بازبینی تازهٔ این پیاده‌سازی هنوز اثبات نشده و شرط پذیرش باقی است.
