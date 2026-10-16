using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.PersianStorefront;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Sms.SmsIr;

public sealed class SmsIrStartup : INopStartup
{
    public int Order => 4000;
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<SmsIrClient>(client => client.Timeout = TimeSpan.FromSeconds(15)).RedactLoggedHeaders(["X-API-KEY"]);
        services.RemoveAll<IStorefrontOtpSender>();
        services.AddScoped<IStorefrontOtpSender, SmsIrSender>();
        services.AddScoped<SmsIrLocalBootstrap>();
        services.AddScoped<SmsIrRuntimeState>();
        services.AddScoped<ISmsIrJournal, SmsIrJournal>();
        services.RemoveAll<IStorefrontOtpAudit>();
        services.AddScoped<IStorefrontOtpAudit, SmsIrJournal>();
    }
    public void Configure(IApplicationBuilder application) { }
}

public sealed class SmsIrRoutes : IRouteProvider
{
    public int Priority => 0;
    public void RegisterRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllerRoute(SmsIrPlugin.ConfigurationRoute, "Admin/SmsIr/Configure", new { controller = "SmsIr", action = "Configure", area = AreaNames.ADMIN });
        endpoints.MapControllerRoute("Plugin.SmsIr.Reports", "Admin/SmsIr/Reports", new { controller = "SmsIrReports", action = "Index", area = AreaNames.ADMIN });
        endpoints.MapControllerRoute("Plugin.SmsIr.RefreshDelivery", "Admin/SmsIr/RefreshDelivery", new { controller = "SmsIrReports", action = "RefreshDelivery", area = AreaNames.ADMIN });
    }
}
