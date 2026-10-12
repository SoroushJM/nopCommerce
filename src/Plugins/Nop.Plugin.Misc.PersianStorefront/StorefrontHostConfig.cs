using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Services.Helpers;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed class StorefrontHostConfig : IConfig
{
    public int? HttpPort
    {
        get; set;
    }
    public int? HttpsPort
    {
        get; set;
    }
}

public sealed class StorefrontHostStartup : INopStartup
{
    // The framework registers IWebHelper at order 2000.
    public int Order => 3001;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(nameof(StorefrontHostConfig));
        var ports = new StorefrontHostConfig();
        section.Bind(ports);
        // Native configuration discovery may have already created an empty new section.
        if (ports.HttpPort == null && ports.HttpsPort == null)
            configuration.GetSection(nameof(CommonConfig)).Bind(ports);
        services.AddSingleton(ports);
        services.AddScoped<IWebHelper, StorefrontWebHelper>();
    }

    public void Configure(IApplicationBuilder application)
    {
    }
}