using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Data;
using Nop.Plugin.Misc.PersianStorefront;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Sms.SmsIr.Controllers;

public sealed class SmsIrPhoneSummary
{
    public string Phone { get; set; } = "";
    public int Requests { get; set; }
    public int Accepted { get; set; }
    public int SandboxAccepted { get; set; }
    public int Failed { get; set; }
    public int Blocked { get; set; }
    public int Unknown { get; set; }
    public DateTime LastRequestUtc { get; set; }
}

public sealed class SmsIrReportModel
{
    public string? Phone { get; set; }
    public string Status { get; set; } = "All";
    public IPagedList<SmsIrMessage> Messages { get; set; } = null!;
    public IPagedList<SmsIrPhoneSummary> Numbers { get; set; } = null!;
}

[Area(AreaNames.ADMIN)]
[AuthorizeAdmin]
[AutoValidateAntiforgeryToken]
public sealed class SmsIrReportsController(IRepository<SmsIrMessage> messages, SmsIrClient client,
    SmsIrSettings settings, IEncryptionService encryption, INotificationService notifications) : BasePluginController
{
    [HttpGet]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SMS_SETTINGS)]
    public async Task<IActionResult> Index(string? phone, string status = "All", int page = 1, int numbersPage = 1)
    {
        var query = messages.Table;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var canonical = IranianMobile.Normalize(phone);
            if (canonical is null) { ModelState.AddModelError(nameof(phone), "شمارهٔ موبایل معتبر وارد کنید."); query = query.Where(m => false); }
            else query = query.Where(m => m.Phone == canonical);
        }
        var now = DateTime.UtcNow;
        query = status switch
        {
            "Accepted" => query.Where(m => m.Outcome == "Accepted" && !m.Sandbox),
            "Sandbox" => query.Where(m => m.Outcome == "Accepted" && m.Sandbox),
            "Failed" => query.Where(m => m.Outcome == "Failed"),
            "Unknown" => query.Where(m => m.Outcome == "Unknown" || m.Outcome == "Pending"),
            "Blocked" => query.Where(m => m.Outcome == "Blocked"),
            "ActiveBlock" => query.Where(m => m.Outcome == "Blocked" && m.BlockedUntilUtc > now),
            "ProviderBlacklist" => query.Where(m => (m.DeliveryStatus == 7 || m.ProviderBlacklisted) && !m.Sandbox),
            _ => query
        };
        var grouped = query.GroupBy(m => m.Phone).Select(g => new SmsIrPhoneSummary
        {
            Phone = g.Key, Requests = g.Sum(m => m.RequestCount), Accepted = g.Count(m => m.Outcome == "Accepted" && !m.Sandbox),
            SandboxAccepted = g.Count(m => m.Outcome == "Accepted" && m.Sandbox),
            Failed = g.Count(m => m.Outcome == "Failed"), Blocked = g.Sum(m => m.Outcome == "Blocked" ? m.RequestCount : 0), Unknown = g.Count(m => m.Outcome == "Pending" || m.Outcome == "Unknown"), LastRequestUtc = g.Max(m => m.LastRequestOnUtc)
        });
        return View("~/Plugins/Sms.SmsIr/Views/Reports.cshtml", new SmsIrReportModel
        {
            Phone = phone, Status = status,
            Messages = await query.OrderByDescending(m => m.LastRequestOnUtc).ThenByDescending(m => m.Id).ToPagedListAsync(Math.Clamp(page - 1, 0, 100000), 50),
            Numbers = await grouped.OrderByDescending(m => m.LastRequestUtc).ThenBy(m => m.Phone).ToPagedListAsync(Math.Clamp(numbersPage - 1, 0, 100000), 25)
        });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SMS_SETTINGS)]
    public async Task<IActionResult> RefreshDelivery(int id)
    {
        var message = await messages.GetByIdAsync(id);
        if (message is null) return NotFound();
        if (message.Sandbox || message.Outcome != "Accepted" || message.MessageId is null || settings.Sandbox)
            notifications.WarningNotification("استعلام تحویل فقط برای پیام عملیاتی دارای شناسه و کلید عملیاتی مجاز است.");
        else if (message.DeliveryCheckedOnUtc > DateTime.UtcNow.AddSeconds(-30))
            notifications.WarningNotification("برای استعلام دوباره ۳۰ ثانیه صبر کنید.");
        else
        {
            message.DeliveryCheckedOnUtc = DateTime.UtcNow;
            await messages.UpdateAsync(message, false);
            SmsIrDeliveryResult result;
            try { result = await client.GetDeliveryAsync(encryption.DecryptText(settings.EncryptedApiKey), message.MessageId.Value); }
            catch { result = new(false); }
            if (result.Success)
            {
                if (result.Status.HasValue)
                {
                    message.DeliveryStatus = result.Status;
                    await messages.UpdateAsync(message, false);
                    notifications.SuccessNotification("وضعیت تحویل به‌روز شد.");
                }
                else notifications.WarningNotification("نتیجهٔ تازه‌ای برای تحویل موجود نیست؛ آخرین وضعیت شناخته‌شده حفظ شد.");
            }
            else notifications.ErrorNotification("استعلام انجام نشد؛ کلید، دسترسی گزارش و شناسهٔ پیام را بررسی کنید. وضعیت قبلی حفظ شد.");
        }
        return RedirectToAction(nameof(Index), new { phone = message.Phone });
    }
}
