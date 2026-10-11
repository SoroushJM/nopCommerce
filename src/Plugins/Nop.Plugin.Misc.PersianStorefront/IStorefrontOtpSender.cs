using Storefront.UI;

namespace Nop.Plugin.Misc.PersianStorefront;

public interface IStorefrontOtpSender
{
    Task<OtpResult> SendAsync(string phone, string code);
}