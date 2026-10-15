using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nop.Core.Infrastructure;

namespace Nop.Plugin.Misc.PersianStorefront;

// The packaged view set is fixed; it does not require runtime Razor compilation.
public sealed class StorefrontCompiledViewCompilerProvider(ApplicationPartManager parts) : IViewCompilerProvider
{
    private readonly IViewCompiler _compiler = new CompiledViews(parts);
    public IViewCompiler GetCompiler() => _compiler;

    private sealed class CompiledViews : IViewCompiler
    {
        private readonly Dictionary<string, CompiledViewDescriptor> _views;

        public CompiledViews(ApplicationPartManager parts)
        {
            var feature = new ViewsFeature();
            parts.PopulateFeature(feature);
            _views = feature.ViewDescriptors.GroupBy(view => Normalize(view.RelativePath), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        }

        private static string Normalize(string path) => "/" + path.Replace('\\', '/').TrimStart('~', '/');

        public Task<CompiledViewDescriptor> CompileAsync(string relativePath)
        {
            var path = Normalize(relativePath);
            return Task.FromResult(_views.TryGetValue(path, out var view)
                ? view
                : new CompiledViewDescriptor { RelativePath = path });
        }
    }
}

public sealed class StorefrontCompiledViewsStartup : INopStartup
{
    public int Order => 1001; // Native MVC is registered at 1000.

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool?>("StorefrontRuntime:UsePrecompiledViews")
            ?? configuration.GetValue<bool>("CommonConfig:UsePrecompiledViews");
        if (!enabled)
            return;
        var assembly = System.Reflection.Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Nop.Docker.Views.dll"));
        services.AddControllersWithViews().AddApplicationPart(assembly);
        services.Replace(ServiceDescriptor.Singleton<IViewCompilerProvider, StorefrontCompiledViewCompilerProvider>());
    }

    public void Configure(IApplicationBuilder application) { }
}
