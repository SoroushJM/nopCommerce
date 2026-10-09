using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Nop.Data;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Npgsql;

namespace Nop.Plugin.Misc.PersianStorefront.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public sealed class PersianStorefrontMaintenanceController(IWebHostEnvironment environment,
    DevelopmentPresentationRepair repair) : BasePluginController
{
    private bool CanRepair()
    {
        var remote = HttpContext.Connection.RemoteIpAddress;
        var host = Request.Host.Host;
        if (!environment.IsDevelopment() || remote == null || !IPAddress.IsLoopback(remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote) ||
            !(host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
              IPAddress.TryParse(host, out var requestAddress) && IPAddress.IsLoopback(requestAddress)))
            return false;

        try
        {
            var data = DataSettingsManager.LoadSettings();
            if (data?.DataProvider != DataProviderType.PostgreSQL)
                return false;
            var connection = new NpgsqlConnectionStringBuilder(data.ConnectionString);
            return connection.Database == "nop_stationery_dev" &&
                (connection.Host?.Equals("localhost", StringComparison.OrdinalIgnoreCase) == true ||
                 IPAddress.TryParse(connection.Host, out var address) && IPAddress.IsLoopback(address));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public IActionResult Configure()
    {
        return View("~/Plugins/Misc.PersianStorefront/Views/Configure.cshtml", CanRepair());
    }

    [HttpPost]
    [ActionName(nameof(Configure))]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Repair()
    {
        if (!CanRepair())
            return Forbid();
        ViewData["Report"] = await repair.RunAsync();
        return View("~/Plugins/Misc.PersianStorefront/Views/Configure.cshtml", true);
    }
}