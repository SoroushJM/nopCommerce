using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Data;
using Nop.Plugin.Misc.PersianStorefront;
using Nop.Web.Controllers;
using Nop.Web.Infrastructure.Installation;
using Nop.Web.Models.Install;
using Nop.Web.Validators.Install;
using Npgsql;

namespace Nop.Storefront.Tests;

// These checks use fake HTTP contexts and never connect to a database or running shop.
[NotInParallel]
public sealed class StorefrontRuntimeTests
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [Test]
    public void CookiesReadOnlyTheSelectedStoreAndKeepUnrelatedNames()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = ".Nop.madadrang.Customer=own; .Nop.mishashop.Customer=other; .Nop.Customer=legacy; .Nop.madadrang.AuthenticationC1=chunk; external=value";
        var cookies = new StorefrontCookieScope(".Nop.madadrang").RequestCookies(http.Request.Cookies);
        Require(cookies[".Nop.Customer"] == "own", "The selected store customer cookie must be mapped to its native name.");
        Require(cookies[".Nop.AuthenticationC1"] == "chunk", "Authentication cookie chunks must keep their suffix.");
        Require(cookies["external"] == "value" && cookies.Count == 3, "Foreign store and legacy native cookies must not leak into this request.");
    }

    [Test]
    public async Task CookiesScopeAppendDeleteAndLateSessionWrites()
    {
        var http = new DefaultHttpContext();
        var scope = new StorefrontCookieScope(".Nop.mishashop");
        await scope.InvokeAsync(http, context =>
        {
            context.Response.Cookies.Append(".Nop.Customer", "customer", new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict });
            context.Response.Cookies.Append(".Nop.AuthenticationC1", "chunk");
            context.Response.Cookies.Append(".Nop.mishashop.Culture", "fa");
            context.Response.Cookies.Append("external", "same");
            context.Response.Cookies.Delete(".Nop.Authentication");
            return Task.CompletedTask;
        });
        // OnStarting callbacks can write after the middleware delegate has returned.
        http.Response.Cookies.Append(".Nop.Session", "late");
        var headers = http.Response.Headers.SetCookie.Select(value => value ?? "").ToArray();
        Require(headers.Any(value => value.StartsWith(".Nop.mishashop.Customer=", StringComparison.Ordinal)
            && value.Contains("httponly", StringComparison.OrdinalIgnoreCase) && value.Contains("secure", StringComparison.OrdinalIgnoreCase)
            && value.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase)), "Cookie options must survive scoping.");
        Require(headers.Any(value => value.StartsWith(".Nop.mishashop.AuthenticationC1=", StringComparison.Ordinal)), "Chunk cookies must be scoped.");
        Require(headers.Any(value => value.StartsWith(".Nop.mishashop.Authentication=;", StringComparison.Ordinal)), "Delete must expire this store's native cookie.");
        Require(headers.Any(value => value.StartsWith(".Nop.mishashop.Session=late", StringComparison.Ordinal)), "Late session writes must still be scoped.");
        Require(headers.Any(value => value.StartsWith("external=same", StringComparison.Ordinal)), "Unrelated names must be preserved.");
        Require(headers.All(value => !value.StartsWith(".Nop.Customer=", StringComparison.Ordinal)
            && !value.StartsWith(".Nop.mishashop.mishashop.", StringComparison.Ordinal)), "Native and already-scoped names must not be duplicated.");
    }

    [Test]
    public void DefaultCookieConfigurationKeepsNativeBehavior()
    {
        Require(!new StorefrontCookieScope("").Enabled && !new StorefrontCookieScope(".Nop").Enabled,
            "No custom prefix must leave the native cookie pipeline unchanged.");
    }

    [Test]
    public async Task CompiledViewsResolveDescriptorsWithoutCompilingMissingFiles()
    {
        var expected = new CompiledViewDescriptor { RelativePath = "/Views/Install/Index.cshtml" };
        var parts = new ApplicationPartManager();
        parts.FeatureProviders.Add(new DescriptorFeature(expected));
        var compiler = new StorefrontCompiledViewCompilerProvider(parts).GetCompiler();
        Require(ReferenceEquals(expected, await compiler.CompileAsync("~/views/install/Index.cshtml")), "Compiled descriptors must resolve normalized paths case-insensitively.");
        var missing = await compiler.CompileAsync("/Views/Missing.cshtml");
        Require(missing.Item is null && missing.RelativePath == "/Views/Missing.cshtml", "A missing descriptor must remain missing, without invoking runtime Razor compilation.");
    }

    [Test]
    public async Task ConfiguredInstallerValidatesAdministratorAndDoesNotRenderBootstrapCredentials()
    {
        var installed = typeof(DataSettingsManager).GetField("_databaseIsInstalled", BindingFlags.NonPublic | BindingFlags.Static)!;
        var previous = installed.GetValue(null);
        installed.SetValue(null, false);
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InstallationConfig:AdminEmail"] = "owner@example.test",
                ["InstallationConfig:DataProvider"] = "PostgreSQL",
                ["InstallationConfig:ServerName"] = "database",
                ["InstallationConfig:DatabaseName"] = "UnitTestStore",
                ["InstallationConfig:Username"] = "unit-user",
                ["InstallationConfig:Password"] = "unit-only-password"
            }).Build();
            var defaults = new StorefrontInstallationDefaults(configuration);
            var nativeValidator = new InstallValidator(new InstallationResources());
            using var provider = new ServiceCollection().AddSingleton<IValidator<InstallModel>>(nativeValidator).BuildServiceProvider();
            var http = new DefaultHttpContext { RequestServices = provider };
            http.Request.Method = "POST";
            var model = new InstallModel { AdminEmail = "", AdminPassword = "", ConfirmPassword = "", DataProvider = DataProviderType.MySql,
                CreateDatabaseIfNotExists = true, UseCustomCollation = true, ConnectionString = "client-supplied", Password = "client-supplied" };
            var state = new ModelStateDictionary();
            state.AddModelError(nameof(InstallModel.DataProvider), "client-supplied provider error");
            var descriptor = new ControllerActionDescriptor { ControllerTypeInfo = typeof(InstallController).GetTypeInfo(), ActionName = "Index" };
            var action = new ActionContext(http, new RouteData(), descriptor, state);
            var context = new ActionExecutingContext(action, new List<IFilterMetadata>(), new Dictionary<string, object?> { ["model"] = model }, new object());
            var view = new ViewResult { ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), state) { Model = model } };
            var called = false;
            await new StorefrontInstallationFilter(defaults).OnActionExecutionAsync(context, () =>
            {
                called = true;
                var connection = new NpgsqlConnectionStringBuilder(model.ConnectionString);
                Require(model.DataProvider == DataProviderType.PostgreSQL && connection.Password == "unit-only-password"
                    && connection.MaxPoolSize == 10 && connection.MinPoolSize == 0, "Configured database credentials/provider and pool limits must replace submitted values.");
                Require(model.ConnectionStringRaw && !model.CreateDatabaseIfNotExists && !model.UseCustomCollation,
                    "An existing configured database must not be created or have collation changed.");
                Require(state.ContainsKey(nameof(InstallModel.AdminEmail)) && state.ContainsKey(nameof(InstallModel.AdminPassword))
                    && !state.ContainsKey(nameof(InstallModel.DataProvider)), "Native administrator validation must remain active after configured database fields are replaced.");
                return Task.FromResult(new ActionExecutedContext(action, context.Filters, context.Controller) { Result = view });
            });
            Require(called && model.ConnectionString is null && model.Password is null, "Bootstrap credentials must be removed from the model before rendering.");
            Require(view.ViewName == "~/Plugins/Misc.PersianStorefront/Views/Install/Index.cshtml"
                && view.ViewData["Storefront.DatabaseConfigured"] is true, "Configured installs must render the dedicated plugin view.");
            Require(defaults.AdminEmail == "owner@example.test", "The legacy deployment administrator default must remain available.");
        }
        finally { installed.SetValue(null, previous); }
    }

    private sealed class DescriptorFeature(CompiledViewDescriptor descriptor) : IApplicationFeatureProvider<ViewsFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ViewsFeature feature) => feature.ViewDescriptors.Add(descriptor);
    }

    private sealed class InstallationResources : IInstallationLocalizationService
    {
        public string GetResource(string resourceName) => resourceName;
        public string GetBrowserCulture() => throw new NotSupportedException();
        public InstallationLanguage GetCurrentLanguage() => throw new NotSupportedException();
        public void SaveCurrentLanguage(string languageCode) => throw new NotSupportedException();
        public IList<InstallationLanguage> GetAvailableLanguages() => throw new NotSupportedException();
        public Dictionary<int, string> GetAvailableProviderTypes(int[]? valuesToExclude = null, bool useLocalization = true) => throw new NotSupportedException();
    }
}
