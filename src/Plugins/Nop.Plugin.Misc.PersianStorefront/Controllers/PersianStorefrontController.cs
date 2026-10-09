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

public sealed record StorefrontPageModel(Bootstrap Initial, string Page, int ProductId);
public sealed class PersianStorefrontController(StorefrontService storefront, IWebHostEnvironment environment) : BasePublicController
{
    public async Task<IActionResult> Index(string page = "home", int id = 0)
    {
        var catalog = await storefront.Catalog();
        if (page == "product" && catalog.All(x => x.Id != id))
            return NotFound();
        return View("~/Plugins/Misc.PersianStorefront/Views/Index.cshtml", new StorefrontPageModel(new(catalog, await storefront.Cart(), environment.IsDevelopment()), page, id));
    }
}
[ApiController]
[Route("stationery/api")]
[ApiExplorerSettings(GroupName = "stationery")]
[AutoValidateAntiforgeryToken]
[CheckAccessPublicStore]
[CheckAccessClosedStore]
public sealed class PersianStorefrontApiController(StorefrontService storefront, IWebHostEnvironment environment, IWorkContext work,
 IStoreContext store, ICustomerService customers, ICustomerRegistrationService registration, MockOtpStore otp) : ControllerBase
{
    [HttpGet("cart")]
    public async Task<ActionResult<CartSnapshot>> Cart()
    {
        return await storefront.Cart();
    }

    [HttpGet("catalog")]
    public async Task<ActionResult<List<StoreProduct>>> Catalog()
    {
        return await storefront.Catalog();
    }

    [HttpPost("quote")]
    public async Task<ActionResult<Quote>> Quote([FromBody] SelectionRequest request)
    {
        try
        {
            return await storefront.Quote(request);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
    [HttpPost("cart/add")]
    public async Task<ActionResult<CartSnapshot>> Add([FromBody] SelectionRequest request)
    {
        try
        {
            return await storefront.Add(request);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
    [HttpPost("cart/quantity")]
    public async Task<ActionResult<CartSnapshot>> Quantity([FromBody] QuantityRequest request)
    {
        try
        {
            return await storefront.Quantity(request);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
    [HttpPost("otp/send")]
    public async Task<ActionResult<OtpResult>> Send([FromBody] PhoneRequest request)
    {
        if (!environment.IsDevelopment())
            return StatusCode(503, new OtpResult(false, "ورود پیامکی هنوز به سرویس واقعی متصل نشده است."));
        return otp.Send((await work.GetCurrentCustomerAsync()).CustomerGuid, request.Phone);
    }
    [HttpPost("otp/verify")]
    public async Task<ActionResult<OtpResult>> Verify([FromBody] VerifyRequest request)
    {
        if (!environment.IsDevelopment())
            return StatusCode(503, new OtpResult(false, "محیط آزمایشی غیرفعال است."));
        var guest = await work.GetCurrentCustomerAsync();
        if (!otp.Verify(guest.CustomerGuid, request.Phone, request.Code))
            return new OtpResult(false, "کد صحیح نیست یا منقضی شده است.");
        var phone = MockOtpStore.Normalize(request.Phone);
        var customer = await customers.GetCustomerByPhoneAsync(phone);
        if (customer != null && (!customer.Active || customer.Deleted || await customers.IsAdminAsync(customer)))
            return new OtpResult(false, "ورود به این حساب از محیط آزمایشی مجاز نیست.");
        if (customer == null)
        {
            customer = new Customer { Email = "mobile-" + phone + "@accounts.invalid", Phone = phone, PhoneSmsVerified = true, Username = "mobile:" + phone, Active = true, CreatedOnUtc = DateTime.UtcNow, LastActivityDateUtc = DateTime.UtcNow, RegisteredInStoreId = (await store.GetCurrentStoreAsync()).Id };
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