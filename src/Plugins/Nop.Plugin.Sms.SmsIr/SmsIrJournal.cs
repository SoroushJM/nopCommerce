using Nop.Core;
using Nop.Data;
using Nop.Plugin.Misc.PersianStorefront;

namespace Nop.Plugin.Sms.SmsIr;

public sealed class SmsIrJournal(IRepository<SmsIrMessage> messages, SmsIrSettings settings,
    IWorkContext work, IStoreContext store) : ISmsIrJournal, IStorefrontOtpAudit
{
    public async Task<SmsIrMessage> BeginAsync(string phone, string kind, int templateId)
    {
        var message = new SmsIrMessage
        {
            CreatedOnUtc = DateTime.UtcNow, LastRequestOnUtc = DateTime.UtcNow, CustomerId = (await work.GetCurrentCustomerAsync()).Id,
            StoreId = (await store.GetCurrentStoreAsync()).Id, Phone = phone, Kind = kind,
            TemplateId = templateId, Sandbox = settings.Sandbox
        };
        await messages.InsertAsync(message, false);
        return message;
    }

    public async Task CompleteAsync(SmsIrMessage message, SmsIrSendResult result)
    {
        message.Outcome = result.Success ? "Accepted" : result.IsAmbiguous ? "Unknown" : "Failed";
        message.Reason = result.Success ? "" : result.Message;
        message.ProviderStatus = result.ProviderStatus;
        message.ProviderBlacklisted = result.IsBlacklisted;
        message.MessageId = result.MessageId;
        await messages.UpdateAsync(message, false);
    }

    public async Task BlockedAsync(string phone, string reason, DateTime untilUtc)
    {
        // Called under the storefront's short state lock. Coalesce repeated
        // rejections in the same window, retaining their count and latest actor.
        var storeId = (await store.GetCurrentStoreAsync()).Id;
        var existing = await messages.GetAllAsync(query => query.Where(m => m.Outcome == "Blocked" &&
            m.Phone == phone && m.Reason == reason && m.BlockedUntilUtc == untilUtc && m.StoreId == storeId));
        if (existing.Count == 1)
        {
            var previous = existing[0];
            previous.RequestCount++;
            previous.LastRequestOnUtc = DateTime.UtcNow;
            previous.CustomerId = (await work.GetCurrentCustomerAsync()).Id;
            await messages.UpdateAsync(previous, false);
            return;
        }
        var message = await BeginAsync(phone, "OTP", settings.TemplateId);
        message.Outcome = "Blocked";
        message.Reason = reason;
        message.BlockedUntilUtc = untilUtc;
        await messages.UpdateAsync(message, false);
    }
}
