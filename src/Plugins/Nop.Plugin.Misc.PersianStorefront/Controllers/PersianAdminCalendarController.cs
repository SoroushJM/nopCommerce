using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Services.ExportImport;
using Nop.Web.Areas.Admin.Infrastructure;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.PersianStorefront.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public sealed class PersianAdminCalendarController : BasePluginController
{
    [HttpPost]
    public IActionResult SetTransferCalendar(string transferCalendar, string returnUrl = "")
    {
        if (!TransferCalendar.IsValid(transferCalendar))
            return BadRequest();

        Response.Cookies.Append(AdminTransferCalendarPreference.CookieName, transferCalendar, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = Request.PathBase.HasValue ? Request.PathBase.Value + "/" : "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        });

        return Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Home", new { area = AreaNames.ADMIN });
    }
}
