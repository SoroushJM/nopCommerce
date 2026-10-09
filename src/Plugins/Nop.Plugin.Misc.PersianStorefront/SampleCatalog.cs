namespace Nop.Plugin.Misc.PersianStorefront;

public record SeedOption(int Id, string Name, string Color, decimal Adjustment = 0, bool Available = true);
public record SeedProduct(int Id, string Name, string Category, string Brand, decimal Price, decimal OldPrice,
    string Image, string Description, bool Available, List<SeedOption> Options);

// Used only by explicit, guarded development seed and repair operations.
public static class SampleCatalog
{
    public static List<SeedProduct> Products => [
        Make(1,"دفتر نقطه‌ای جلد سخت","دفتر و کاغذ","پاپکو",185000,220000,"notebook.jpg","یک جای تازه برای ایده‌های بزرگ. دفتر ۸۰ برگ با کاغذ نرم، جلد مقاوم و صحافی تخت؛ همراه هر روز درس و نوشتن."),
        Make(2,"مداد رنگی ۲۴ رنگ","نوشت‌افزار","فابر کاستل",495000,560000,"pencils.jpg","۲۴ رنگ برای خیال‌های بی‌مرز. مدادهای نرم با رنگ‌دانهٔ غنی، مناسب طراحی و رنگ‌آمیزی."),
        Make(3,"خودکار ژلی ۰.۵ میلی‌متر","نوشت‌افزار","زبرا",89000,0,"pens.jpg","نوشتن روان و تمیز برای یادداشت‌های روزانه، با نوک ظریف و طراحی راحت."),
        Make(4,"دفتر برنامه‌ریزی روزانه","دفتر و کاغذ","پاپکو",245000,285000,"planner.jpg","روزهای شلوغ را به قدم‌های کوچک تبدیل کن. فضای برنامه‌ریزی، یادداشت و پیگیری کارها."),
        Make(5,"ست هایلایتر پاستلی","نوشت‌افزار","استابیلو",320000,360000,"markers.jpg","رنگ‌های ملایم برای برجسته کردن نکته‌های مهم. ست ۶ رنگ مناسب درس و برنامه‌ریزی."),
        Make(6,"جامدادی پارچه‌ای","لوازم مدرسه","پاپکو",275000,0,"pouch.jpg","همهٔ همراه‌های کوچک نوشتن، یک‌جا. جامدادی سبک با زیپ مقاوم و فضای کافی."),
        Make(7,"کاغذ یادداشت رنگی","دفتر و کاغذ","پاپکو",65000,0,"notes.jpg","ایده‌ها و یادآوری‌های کوچک را رنگی بنویس. بستهٔ کاغذ یادداشت برای میز کار و درس."),
        Make(8,"ست ابزار نقاشی","هنر و خلاقیت","فابر کاستل",680000,0,"art.jpg","یک شروع رنگی برای تجربه‌های تازه؛ مجموعهٔ ابزار طراحی و نقاشی.",false)
    ];
    private static SeedProduct Make(int id, string name, string category, string brand, decimal price, decimal old, string image, string description, bool available = true)
    {
        return new(id, name, category, brand, price, old, "_content/Storefront.UI/images/" + image, description, available,
        [new(1, "آبی", "#4c75dc"), new(2, "سبز", "#7cae91", 10000), new(3, "صورتی", "#eaa3b6", 15000), new(4, "نارنجی", "#ee965d", 0, false)]);
    }
}