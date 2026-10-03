namespace Storefront.UI;

public record ProductOption(int Id, string Name, string Color, decimal Adjustment = 0, bool Available = true);
public record ProductAttribute(int Id, string Name, List<ProductOption> Options);
public record StoreProduct(int Id, string Name, string Category, string Brand, decimal Price, decimal OldPrice,
    string Image, string Description, bool Available, List<ProductAttribute> Attributes, string Label = "");
public record CartLine(int Id, int ProductId, string Name, string Image, string Options, int Quantity, decimal UnitPrice);
public record CartSnapshot(List<CartLine> Lines, bool SignedIn = false, string Phone = "")
{
    public decimal Total => Lines.Sum(x => x.Quantity * x.UnitPrice);
    public int Count => Lines.Sum(x => x.Quantity);
}
public record Bootstrap(List<StoreProduct> Products, CartSnapshot Cart, bool Demo, string ApiBase = "/stationery/api");
public record SelectionRequest(int ProductId, Dictionary<int,int> Values, int Quantity = 1);
public record Quote(decimal Price, bool Available, string Message = "");
public record QuantityRequest(int LineId, int Quantity);
public record PhoneRequest(string Phone);
public record VerifyRequest(string Phone, string Code);
public record OtpResult(bool Success, string Message, string? TestCode = null);

// Shared sample catalogue for the isolated preview and explicit development seeding only.
public static class SampleCatalog
{
    public static List<StoreProduct> Products => [
        Make(1,"دفتر نقطه‌ای جلد سخت","دفتر و کاغذ","پاپکو",185000,220000,"notebook.jpg","یک جای تازه برای ایده‌های بزرگ. دفتر ۸۰ برگ با کاغذ نرم، جلد مقاوم و صحافی تخت؛ همراه هر روز درس و نوشتن.","پرفروش"),
        Make(2,"مداد رنگی ۲۴ رنگ","نوشت‌افزار","فابر کاستل",495000,560000,"pencils.jpg","۲۴ رنگ برای خیال‌های بی‌مرز. مدادهای نرم با رنگ‌دانهٔ غنی، مناسب طراحی و رنگ‌آمیزی.","پیشنهاد ما"),
        Make(3,"خودکار ژلی ۰.۵ میلی‌متر","نوشت‌افزار","زبرا",89000,0,"pens.jpg","نوشتن روان و تمیز برای یادداشت‌های روزانه، با نوک ظریف و طراحی راحت.",""),
        Make(4,"دفتر برنامه‌ریزی روزانه","دفتر و کاغذ","پاپکو",245000,285000,"planner.jpg","روزهای شلوغ را به قدم‌های کوچک تبدیل کن. فضای برنامه‌ریزی، یادداشت و پیگیری کارها.","تازه رسیده"),
        Make(5,"ست هایلایتر پاستلی","نوشت‌افزار","استابیلو",320000,360000,"markers.jpg","رنگ‌های ملایم برای برجسته کردن نکته‌های مهم. ست ۶ رنگ مناسب درس و برنامه‌ریزی.",""),
        Make(6,"جامدادی پارچه‌ای","لوازم مدرسه","پاپکو",275000,0,"pouch.jpg","همهٔ همراه‌های کوچک نوشتن، یک‌جا. جامدادی سبک با زیپ مقاوم و فضای کافی.",""),
        Make(7,"کاغذ یادداشت رنگی","دفتر و کاغذ","پاپکو",65000,0,"notes.jpg","ایده‌ها و یادآوری‌های کوچک را رنگی بنویس. بستهٔ کاغذ یادداشت برای میز کار و درس.",""),
        Make(8,"ست ابزار نقاشی","هنر و خلاقیت","فابر کاستل",680000,0,"art.jpg","یک شروع رنگی برای تجربه‌های تازه؛ مجموعهٔ ابزار طراحی و نقاشی.","ناموجود",false)
    ];
    private static StoreProduct Make(int id,string name,string category,string brand,decimal price,decimal old,string image,string description,string label,bool available=true) =>
        new(id,name,category,brand,price,old,"_content/Storefront.UI/images/"+image,description,available,
        [new(1,"رنگ",[new(1,"آبی","#4c75dc"),new(2,"سبز","#7cae91",10000),new(3,"صورتی","#eaa3b6",15000),new(4,"نارنجی","#ee965d",0,false)])],label);
}
