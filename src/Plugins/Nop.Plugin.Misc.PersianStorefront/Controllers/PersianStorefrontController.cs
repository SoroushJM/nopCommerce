using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Services.Customers;
using Nop.Web.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Storefront.UI;

namespace Nop.Plugin.Misc.PersianStorefront.Controllers;

public sealed class PersianStorefrontController : BasePublicController
{
    public IActionResult Login()
    {
        return View("~/Plugins/Misc.PersianStorefront/Views/Login.cshtml");
    }
}

[ApiController]
[Route("stationery/api")]
[ApiExplorerSettings(GroupName = "stationery")]
[AutoValidateAntiforgeryToken]
[CheckAccessPublicStore]
[CheckAccessClosedStore]
public sealed class PersianStorefrontApiController(IWebHostEnvironment environment, IWorkContext work, IStoreContext store, ICustomerService customers, ICustomerRegistrationService registration, StorefrontOtpService otp) : ControllerBase
{
    [HttpPost("otp/send")]
    public async Task<ActionResult<OtpResult>> Send([FromBody] PhoneRequest request)
    {
        if (!environment.IsDevelopment())
            return StatusCode(503, new OtpResult(false, "ورود پیامکی هنوز به سرویس واقعی متصل نشده است."));
        return await otp.SendAsync((await work.GetCurrentCustomerAsync()).CustomerGuid, request.Phone);
    }

    [HttpPost("otp/verify")]
    public async Task<ActionResult<OtpResult>> Verify([FromBody] VerifyRequest request)
    {
        if (!environment.IsDevelopment())
            return StatusCode(503, new OtpResult(false, "محیط آزمایشی غیرفعال است."));
        var guest = await work.GetCurrentCustomerAsync();
        var phone = await otp.VerifyAsync(guest.CustomerGuid, request.Phone, request.Code);
        if (phone == null)
            return new OtpResult(false, "کد صحیح نیست یا منقضی شده است.");
        var customer = await customers.GetCustomerByPhoneAsync(phone);
        if (customer != null && (!customer.Active || customer.Deleted || await customers.IsAdminAsync(customer)))
            return new OtpResult(false, "ورود به این حساب از محیط آزمایشی مجاز نیست.");
        if (customer == null)
        {
            customer = new Customer
            {
                Email = "mobile-" + phone + "@accounts.invalid",
                Phone = phone,
                PhoneSmsVerified = true,
                Username = "mobile:" + phone,
                Active = true,
                CreatedOnUtc = DateTime.UtcNow,
                LastActivityDateUtc = DateTime.UtcNow,
                RegisteredInStoreId = (await store.GetCurrentStoreAsync()).Id
            };
            await customers.InsertCustomerAsync(customer);
            var role = await customers.GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.RegisteredRoleName);
            await customers.AddCustomerRoleMappingAsync(new CustomerCustomerRoleMapping { CustomerId = customer.Id, CustomerRoleId = role.Id });
        }

        if (string.IsNullOrWhiteSpace(customer.Email))
        {
            customer.Email = "mobile-" + customer.Id + "@accounts.invalid";
            await customers.UpdateCustomerAsync(customer);
        }

        if (await customers.GetCurrentPasswordAsync(customer.Id) == null)
            await customers.InsertCustomerPasswordAsync(new CustomerPassword { CustomerId = customer.Id, PasswordFormat = PasswordFormat.Hashed, Password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)), PasswordSalt = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), CreatedOnUtc = DateTime.UtcNow });
        await registration.SignInCustomerAsync(customer, "/stationery/cart", true);
        return new OtpResult(true, "با موفقیت وارد شدی؛ سبد خریدت محفوظ است.");
    }
}