using Storefront.UI.Shared;

namespace Nop.Plugin.Misc.PersianStorefront;

public interface IStorefrontOtpSender
{
    Task<OtpResult> SendAsync(string phone, string code);
}

public interface IStorefrontOtpAudit
{
    Task BlockedAsync(string phone, string reason, DateTime untilUtc);
}

public sealed class NullStorefrontOtpAudit : IStorefrontOtpAudit
{
    public Task BlockedAsync(string phone, string reason, DateTime untilUtc) => Task.CompletedTask;
}
