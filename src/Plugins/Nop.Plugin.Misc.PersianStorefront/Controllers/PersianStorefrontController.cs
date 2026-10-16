using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Services.Customers;
using Nop.Web.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Storefront.UI.Shared;
using Nop.Services.Themes;
using Nop.Core.Http;

namespace Nop.Plugin.Misc.PersianStorefront.Controllers;

public sealed class PersianStorefrontController(IThemeContext themes) : BasePublicController
{
    public async Task<IActionResult> Login()
    {
        if (!StorefrontThemeService.IsSupported(await themes.GetWorkingThemeNameAsync()))
            return RedirectToRoute(NopRouteNames.General.LOGIN);
        return View("~/Plugins/Misc.PersianStorefront/Views/Login.cshtml");
    }
}

[ApiController]
[Route("stationery/api")]
[ApiExplorerSettings(GroupName = "stationery")]
[AutoValidateAntiforgeryToken]
[CheckAccessPublicStore]
[CheckAccessClosedStore]
public sealed class PersianStorefrontApiController(IWorkContext work, IStoreContext store, ICustomerService customers, ICustomerRegistrationService registration, StorefrontOtpService otp, StorefrontPhoneAccounts accounts) : ControllerBase
{
    [HttpPost("otp/send")]
    public async Task<ActionResult<OtpResult>> Send([FromBody] PhoneRequest request)
    {
        return await otp.SendAsync((await work.GetCurrentCustomerAsync()).CustomerGuid, request.Phone);
    }

    [HttpPost("otp/verify")]
    public async Task<ActionResult<OtpResult>> Verify([FromBody] VerifyRequest request)
    {
        var guest = await work.GetCurrentCustomerAsync();
        var phone = await otp.VerifyAsync(guest.CustomerGuid, request.Phone, request.Code);
        if (phone == null)
            return new OtpResult(false, "کد صحیح نیست یا منقضی شده است.");
        var customer = await accounts.ResolveAsync(phone, (await store.GetCurrentStoreAsync()).Id);
        if (customer is null)
            return new OtpResult(false, "ورود به این شماره ممکن نیست؛ برای بررسی حساب با پشتیبانی تماس بگیرید.");

        if (await customers.GetCurrentPasswordAsync(customer.Id) == null)
            await customers.InsertCustomerPasswordAsync(new CustomerPassword { CustomerId = customer.Id, PasswordFormat = PasswordFormat.Hashed, Password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)), PasswordSalt = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), CreatedOnUtc = DateTime.UtcNow });
        await registration.SignInCustomerAsync(customer, "/stationery/cart", true);
        return new OtpResult(true, "با موفقیت وارد شدی؛ سبد خریدت محفوظ است.");
    }
}
