using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;

namespace Nop.Plugin.Misc.PersianStorefront;

// Translate at the HTTP boundary so native services keep their upstream cookie names.
public sealed class StorefrontCookieScope(string prefix)
{
    private const string NativePrefix = ".Nop";
    public bool Enabled => !string.IsNullOrEmpty(prefix) && prefix != NativePrefix;

    public string ResponseName(string name) => name.StartsWith(NativePrefix + ".", StringComparison.Ordinal)
        && !name.StartsWith(prefix + ".", StringComparison.Ordinal)
            ? prefix + name[NativePrefix.Length..]
            : name;

    public IRequestCookieCollection RequestCookies(IRequestCookieCollection cookies)
    {
        var mapped = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var cookie in cookies)
        {
            if (cookie.Key.StartsWith(prefix + ".", StringComparison.Ordinal))
                mapped[NativePrefix + cookie.Key[prefix.Length..]] = cookie.Value;
            else if (!cookie.Key.StartsWith(NativePrefix + ".", StringComparison.Ordinal))
                mapped[cookie.Key] = cookie.Value;
        }
        return new ScopedRequestCookies(mapped);
    }

    private sealed class ScopedRequestCookies(Dictionary<string, string> values) : IRequestCookieCollection
    {
        public int Count => values.Count;
        public ICollection<string> Keys => values.Keys;
        public string? this[string key] => values.GetValueOrDefault(key);
        public bool ContainsKey(string key) => values.ContainsKey(key);
        public bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class ScopedResponseCookies(IResponseCookies cookies, StorefrontCookieScope scope) : IResponseCookies
    {
        public void Append(string key, string value) => cookies.Append(scope.ResponseName(key), value);
        public void Append(string key, string value, CookieOptions options) => cookies.Append(scope.ResponseName(key), value, options);
        public void Append(ReadOnlySpan<KeyValuePair<string, string>> values, CookieOptions options)
        {
            var mapped = new KeyValuePair<string, string>[values.Length];
            for (var index = 0; index < values.Length; index++)
                mapped[index] = new(scope.ResponseName(values[index].Key), values[index].Value);
            cookies.Append(mapped, options);
        }
        public void Delete(string key) => cookies.Delete(scope.ResponseName(key));
        public void Delete(string key, CookieOptions options) => cookies.Delete(scope.ResponseName(key), options);
    }

    private sealed class ScopedResponseCookiesFeature(IResponseCookies cookies) : IResponseCookiesFeature
    {
        public IResponseCookies Cookies { get; } = cookies;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Request.Cookies = RequestCookies(context.Request.Cookies);
        var responseCookies = context.Response.Cookies;
        context.Features.Set<IResponseCookiesFeature>(new ScopedResponseCookiesFeature(new ScopedResponseCookies(responseCookies, this)));
        // Keep the feature until request disposal: session/auth may append from OnStarting.
        await next(context);
    }
}

public sealed class StorefrontCookieStartup : INopStartup
{
    public int Order => 1; // Before native session, localization, authentication and installation middleware.

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddSingleton(new StorefrontCookieScope(configuration["NOP_COOKIE_PREFIX"] ?? ""));

    public void Configure(IApplicationBuilder application)
    {
        var scope = application.ApplicationServices.GetRequiredService<StorefrontCookieScope>();
        if (scope.Enabled)
            application.Use(next => context => scope.InvokeAsync(context, next));
    }
}
