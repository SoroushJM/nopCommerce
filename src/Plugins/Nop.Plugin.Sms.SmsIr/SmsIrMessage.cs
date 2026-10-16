using Nop.Core;
using LinqToDB.Mapping;

namespace Nop.Plugin.Sms.SmsIr;

// Audit metadata only. Never persist OTP values, API keys or free-text bodies.
public sealed class SmsIrMessage : BaseEntity
{
    public DateTime CreatedOnUtc { get; set; }
    public DateTime LastRequestOnUtc { get; set; }
    public int RequestCount { get; set; } = 1;
    public int CustomerId { get; set; }
    public int StoreId { get; set; }
    public string Phone { get; set; } = "";
    public string Kind { get; set; } = "OTP";
    public int TemplateId { get; set; }
    public bool Sandbox { get; set; }
    public string Outcome { get; set; } = "Pending";
    public string Reason { get; set; } = "";
    public int? ProviderStatus { get; set; }
    public bool ProviderBlacklisted { get; set; }
    public long? MessageId { get; set; }
    public int? DeliveryStatus { get; set; }
    public DateTime? DeliveryCheckedOnUtc { get; set; }
    public DateTime? BlockedUntilUtc { get; set; }

    [NotColumn] public string OutcomeLabel => Outcome switch { "Accepted" => Sandbox ? "پذیرش آزمایشی" : "پذیرفته‌شده توسط سرویس", "Failed" => "ناموفق", "Blocked" => "محدودشده در فروشگاه", _ => "نتیجه نامشخص / در حال ارسال" };
    [NotColumn] public string ReasonLabel => Reason switch { "GuestCooldown" => "فاصلهٔ یک دقیقه برای مهمان درخواست‌کننده", "PhoneCooldown" => "فاصلهٔ یک دقیقه برای این شماره", "PhoneRateLimit" => "سقف درخواست این شماره", "IpRateLimit" => "سقف درخواست IP؛ این شماره لزوماً برای IP دیگر مسدود نیست", _ => Reason };
    [NotColumn] public string DeliveryLabel => Sandbox ? "آزمایشی؛ تحویل واقعی ندارد" : ProviderBlacklisted ? "رد ارسال به دلیل لیست سیاه سرویس" : DeliveryStatus switch
    {
        1 => "رسیده به گوشی", 2 => "نرسیده به گوشی", 3 => "پردازش در مخابرات", 4 => "نرسیده به مخابرات",
        5 => "رسیده به مخابرات", 6 => "خطا", 7 => "لیست سیاه اپراتور", _ => "هنوز استعلام نشده / در انتظار"
    };
}

public interface ISmsIrJournal
{
    Task<SmsIrMessage> BeginAsync(string phone, string kind, int templateId);
    Task CompleteAsync(SmsIrMessage message, SmsIrSendResult result);
}
