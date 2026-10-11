using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Storefront.UI;

namespace Nop.Plugin.Misc.PersianStorefront;

// No SMS is sent; test codes are only available in Development.
public sealed class DevelopmentMockOtpSender(IWebHostEnvironment environment) : IStorefrontOtpSender
{
    public Task<OtpResult> SendAsync(string phone, string code)
    {
        return Task.FromResult(environment.IsDevelopment()
            ? new OtpResult(true, "کد آزمایشی آماده است.", code)
            : new OtpResult(false, "محیط آزمایشی غیرفعال است."));
    }
}