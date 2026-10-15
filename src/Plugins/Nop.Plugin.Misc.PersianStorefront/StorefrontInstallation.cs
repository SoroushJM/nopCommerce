using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Web.Controllers;
using Nop.Web.Models.Install;
using Npgsql;

namespace Nop.Plugin.Misc.PersianStorefront;

// Deliberately not IConfig: bootstrap credentials must not be saved to appsettings.
public sealed class StorefrontInstallationDefaults(IConfiguration configuration)
{
    public string AdminEmail => configuration["InstallationConfig:AdminEmail"] ?? "admin@yourStore.com";
    public bool DatabaseConfigured => !string.IsNullOrWhiteSpace(configuration["InstallationConfig:ServerName"]);
    public DataProviderType DataProvider => Enum.TryParse<DataProviderType>(
        configuration["InstallationConfig:DataProvider"] ?? "SqlServer", true, out var provider) && provider != DataProviderType.Unknown
            ? provider : throw new InvalidOperationException("InstallationConfig:DataProvider is not supported.");

    public string BuildConnectionString()
    {
        var connection = DataProviderManager.GetDataProvider(DataProvider).BuildConnectionString(new InstallModel
        {
            ServerName = configuration["InstallationConfig:ServerName"],
            DatabaseName = configuration["InstallationConfig:DatabaseName"],
            Username = configuration["InstallationConfig:Username"],
            Password = configuration["InstallationConfig:Password"]
        });
        return DataProvider == DataProviderType.PostgreSQL
            ? new NpgsqlConnectionStringBuilder(connection) { MaxPoolSize = 10, MinPoolSize = 0 }.ConnectionString
            : connection;
    }
}

public sealed class StorefrontInstallationFilter(StorefrontInstallationDefaults defaults) : IAsyncActionFilter
{
    private static readonly string[] DatabaseFields =
    [
        nameof(InstallModel.DataProvider), nameof(InstallModel.ServerName), nameof(InstallModel.DatabaseName),
        nameof(InstallModel.Username), nameof(InstallModel.Password), nameof(InstallModel.ConnectionString),
        nameof(InstallModel.ConnectionStringRaw), nameof(InstallModel.CreateDatabaseIfNotExists),
        nameof(InstallModel.IntegratedSecurity), nameof(InstallModel.UseCustomCollation),
        nameof(InstallModel.Collation), nameof(InstallModel.CharacterSet)
    ];

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor action
            || action.ControllerTypeInfo.AsType() != typeof(InstallController)
            || action.ActionName != nameof(InstallController.Index)
            || DataSettingsManager.IsDatabaseInstalled())
        {
            await next();
            return;
        }

        var isPost = HttpMethods.IsPost(context.HttpContext.Request.Method);
        var model = context.ActionArguments.Values.OfType<InstallModel>().FirstOrDefault();
        if (isPost && model is not null)
        {
            if (defaults.DatabaseConfigured)
            {
                model.DataProvider = defaults.DataProvider;
                model.ConnectionStringRaw = true;
                model.ConnectionString = defaults.BuildConnectionString();
                model.CreateDatabaseIfNotExists = false;
                model.UseCustomCollation = false;
                model.Collation = null;
                model.CharacterSet = null;
                foreach (var key in context.ModelState.Keys.Where(key => DatabaseFields.Any(field =>
                    key.Equals(field, StringComparison.OrdinalIgnoreCase) || key.EndsWith("." + field, StringComparison.OrdinalIgnoreCase))).ToArray())
                    context.ModelState.Remove(key);
            }

            var validator = context.HttpContext.RequestServices.GetRequiredService<IValidator<InstallModel>>();
            var validation = await validator.ValidateAsync(model, context.HttpContext.RequestAborted);
            foreach (var error in validation.Errors)
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        var executed = await next();
        if (defaults.DatabaseConfigured && model is not null)
        {
            // The native controller already consumed it; never render bootstrap credentials.
            model.ConnectionString = null;
            model.Password = null;
        }
        if (executed.Result is ViewResult { Model: InstallModel rendered } view)
        {
            if (!isPost)
            {
                rendered.AdminEmail = defaults.AdminEmail;
                rendered.DataProvider = defaults.DataProvider;
            }
            if (defaults.DatabaseConfigured)
            {
                rendered.DataProvider = defaults.DataProvider;
                view.ViewData["Storefront.DatabaseConfigured"] = true;
                view.ViewName = "~/Plugins/Misc.PersianStorefront/Views/Install/Index.cshtml";
            }
        }
    }
}

public sealed class StorefrontInstallationStartup : INopStartup
{
    public int Order => 1002;
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<StorefrontInstallationDefaults>();
        services.AddScoped<StorefrontInstallationFilter>();
        services.Configure<MvcOptions>(options => options.Filters.AddService<StorefrontInstallationFilter>());
    }
    public void Configure(IApplicationBuilder application) { }
}
