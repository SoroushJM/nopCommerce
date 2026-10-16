using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Nop.Services.Events;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Models.Common;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Events;
using Nop.Services.Messages;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Sms.SmsIr;

public sealed class SmsIrWarningViewComponent(SmsIrSettings settings, IWebHostEnvironment environment,
    IPermissionService permissions) : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object? additionalData)
    {
        if (!await permissions.AuthorizeAsync(StandardPermission.Configuration.MANAGE_SMS_SETTINGS))
            return Content("");
        var warning = settings.ConfigurationWarning(environment.IsDevelopment());
        return warning is null ? Content("") : View("~/Plugins/Sms.SmsIr/Views/Warning.cshtml", warning);
    }
}

public sealed class SmsIrMenuConsumer(ISmsPluginManager plugins, INopUrlHelper urls) : IConsumer<AdminMenuCreatedEvent>
{
    public async Task HandleEventAsync(AdminMenuCreatedEvent eventMessage)
    {
        if (await plugins.LoadPluginBySystemNameAsync(SmsIrPlugin.SystemName) is null) return;
        eventMessage.RootMenuItem.InsertAfter("Sms providers", new()
        {
            SystemName = "SmsIr.Reports", Title = "گزارش پیامک‌های sms.ir", IconClass = "far fa-comment-dots",
            Url = urls.RouteUrl("Plugin.SmsIr.Reports"), PermissionNames = [StandardPermission.Configuration.MANAGE_SMS_SETTINGS]
        });
    }
}

public sealed class SmsIrWarningConsumer(SmsIrSettings settings, IWebHostEnvironment environment) : IConsumer<SystemWarningCreatedEvent>
{
    public Task HandleEventAsync(SystemWarningCreatedEvent eventMessage)
    {
        if (settings.ConfigurationWarning(environment.IsDevelopment()) is { } warning)
            eventMessage.SystemWarnings.Add(new() { Level = SystemWarningLevel.Warning, Text = warning });
        return Task.CompletedTask;
    }
}
