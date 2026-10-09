using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Nop.Core.Infrastructure;
using Nop.Web.Framework.Infrastructure.Extensions;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed class StorefrontClientStartup : INopStartup
{
    public int Order => 98;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseNopResponseCompression();
        ClientAssets.Configure(app, app.ApplicationServices.GetRequiredService<IWebHostEnvironment>());
    }
}

public static class ClientAssets
{
    public static void Configure(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.ContentRootPath, "Plugins", "Misc.PersianStorefront", "Content", "Client", "wwwroot");
        if (!Directory.Exists(path))
            return;

        var provider = new PhysicalFileProvider(path);
        environment.WebRootFileProvider = new CompositeFileProvider(provider, environment.WebRootFileProvider);
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".dat"] = "application/octet-stream";
        contentTypes.Mappings[".blat"] = "application/octet-stream";
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = provider,
            ContentTypeProvider = contentTypes,
            OnPrepareResponse = context =>
            {
                var fingerprinted = context.File.Name.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase);
                context.Context.Response.Headers.CacheControl = fingerprinted ? "public, max-age=31536000, immutable" : "no-cache";
            }
        });
    }
}