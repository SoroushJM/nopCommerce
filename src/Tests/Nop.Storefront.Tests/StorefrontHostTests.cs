using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core;
using Nop.Plugin.Misc.PersianStorefront;
using Nop.Services.Helpers;

namespace Nop.Storefront.Tests;

public sealed class StorefrontHostTests
{
    [Test]
    [Arguments("http", "localhost:2020", true, "https://localhost:2021")]
    [Arguments("https", "localhost:2021", false, "http://localhost:2020")]
    [Arguments("http", "localhost:2020", false, "http://localhost:2020")]
    [Arguments("https", "localhost:2021", true, "https://localhost:2021")]
    [Arguments("http", "localhost:9090", true, "https://localhost:9090")]
    [Arguments("http", "[::1]:2020", true, "https://[::1]:2021")]
    [Arguments("http", "example.com", true, "https://example.com")]
    public async Task ConfiguredPortsPreservePathAndQuery(string scheme, string authority, bool useSsl, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = HostString.FromUriComponent(authority);
        context.Request.PathBase = "/shop";
        context.Request.Path = "/stationery/catalog";
        context.Request.QueryString = new QueryString("?q=a%20b");
        var helper = new StorefrontWebHelper(null!, new HttpContextAccessor { HttpContext = context },
            new()
            {
                HttpPort = 2020,
                HttpsPort = 2021
            });
        await Assert.That(helper.GetThisPageUrl(true, useSsl)).IsEqualTo(expected + "/shop/stationery/catalog?q=a%20b");
    }

    [Test]
    [Arguments(null, null)]
    [Arguments(2020, null)]
    [Arguments(2020, 0)]
    [Arguments(2020, 65536)]
    public async Task MissingOrInvalidPortsPreserveNativeHost(int? http, int? https)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 2020);
        var helper = new StorefrontWebHelper(null!, new HttpContextAccessor { HttpContext = context },
            new()
            {
                HttpPort = http,
                HttpsPort = https
            });
        await Assert.That(helper.GetStoreHost(true)).IsEqualTo("https://localhost:2020/");
    }

    [Test]
    [Arguments("StorefrontHostConfig")]
    [Arguments("CommonConfig")]
    public async Task PluginRegistrationReplacesNativeHelperAndAcceptsDeploymentKeys(string section)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["StorefrontHostConfig:HttpPort"] = null,
            ["StorefrontHostConfig:HttpsPort"] = null,
            [section + ":HttpPort"] = "2020",
            [section + ":HttpsPort"] = "2021"
        }).Build();
        var services = new ServiceCollection();
        services.AddScoped<IWebHelper, WebHelper>();
        var startup = new StorefrontHostStartup();
        startup.ConfigureServices(services, configuration);
        var helper = services.Last(descriptor => descriptor.ServiceType == typeof(IWebHelper));
        await Assert.That(helper.ImplementationType).IsEqualTo(typeof(StorefrontWebHelper));
        await Assert.That(helper.Lifetime).IsEqualTo(ServiceLifetime.Scoped);
        await Assert.That(startup.Order).IsGreaterThan(2000);
        using var provider = services.BuildServiceProvider();
        var ports = provider.GetRequiredService<StorefrontHostConfig>();
        await Assert.That(ports.HttpPort).IsEqualTo(2020);
        await Assert.That(ports.HttpsPort).IsEqualTo(2021);
    }

    [Test]
    public async Task NewPartialPairDoesNotBorrowALegacyPort()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["StorefrontHostConfig:HttpPort"] = "3030",
            ["CommonConfig:HttpPort"] = "2020",
            ["CommonConfig:HttpsPort"] = "2021"
        }).Build();
        var services = new ServiceCollection();
        new StorefrontHostStartup().ConfigureServices(services, configuration);
        using var provider = services.BuildServiceProvider();
        var ports = provider.GetRequiredService<StorefrontHostConfig>();
        await Assert.That(ports.HttpPort).IsEqualTo(3030);
        await Assert.That(ports.HttpsPort).IsNull();
    }

    [Test]
    public async Task ImplicitProtocolPortDoesNotMatchConfiguredSourcePort()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("example.com");
        var helper = new StorefrontWebHelper(null!, new HttpContextAccessor { HttpContext = context },
            new() { HttpPort = 443, HttpsPort = 8443 });
        await Assert.That(helper.GetStoreHost(true)).IsEqualTo("https://example.com/");
    }
}
