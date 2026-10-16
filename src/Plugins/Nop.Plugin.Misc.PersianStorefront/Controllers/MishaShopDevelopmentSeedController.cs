using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.PersianStorefront.Controllers;

public sealed record MishaShopDevelopmentSeedModel : BaseNopModel
{
    public MishaShopSeedAvailability Availability { get; init; } = new(false, "");
    public MishaShopSeedResult? Result { get; init; }
}

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public sealed class MishaShopDevelopmentSeedController(IWebHostEnvironment environment,
    MishaShopDevelopmentCatalogSeeder seeder) : BasePluginController
{
    private bool IsLocalDevelopmentRequest()
    {
        var remote = HttpContext.Connection.RemoteIpAddress;
        var host = Request.Host.Host;
        return environment.IsDevelopment() && remote is not null
            && IPAddress.IsLoopback(remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote)
            && (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Configure()
    {
        if (!IsLocalDevelopmentRequest()) return NotFound();
        return View("~/Plugins/Misc.PersianStorefront/Views/MishaShopDevelopmentSeed.cshtml",
            new MishaShopDevelopmentSeedModel { Availability = await seeder.GetAvailabilityAsync() });
    }

    [HttpPost]
    [ActionName(nameof(Configure))]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Seed()
    {
        if (!IsLocalDevelopmentRequest()) return Forbid();
        var result = await seeder.SeedAsync();
        return View("~/Plugins/Misc.PersianStorefront/Views/MishaShopDevelopmentSeed.cshtml",
            new MishaShopDevelopmentSeedModel { Availability = await seeder.GetAvailabilityAsync(), Result = result });
    }
}
