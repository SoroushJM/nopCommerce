using System.Net;

namespace Nop.Storefront.Tests;

internal static class StorefrontTestTarget
{
    internal static Uri Resolve(string? configuredUrl, string? isolated)
    {
        if (isolated != "1" || !Uri.TryCreate(configuredUrl, UriKind.Absolute, out var url) ||
            !url.IsLoopback || url.Scheme != "http" || url.Port is 80 or 5090 ||
            url.UserInfo.Length > 0 || url.Query.Length > 0 || url.Fragment.Length > 0)
            throw new InvalidOperationException("Set STOREFRONT_TEST_ISOLATED=1 and STOREFRONT_TEST_URL to an explicit HTTP loopback URL for a disposable database copy (for example port 5202 or 5230). Port 5090 is forbidden.");

        // Retain a configured path base when resolving relative native routes.
        return new Uri(url.AbsoluteUri.TrimEnd('/') + "/");
    }

    internal static void EnsureSameTarget(Uri target, Uri? request)
    {
        if (request == null || !request.IsAbsoluteUri ||
            !string.Equals(target.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(target.Host, request.Host, StringComparison.OrdinalIgnoreCase) ||
            target.Port != request.Port || request.UserInfo.Length > 0)
            throw new InvalidOperationException("Storefront tests cannot send requests outside the configured isolated host.");
    }
}

internal sealed class StorefrontTargetHandler(Uri target) : DelegatingHandler(
    new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false })
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        StorefrontTestTarget.EnsureSameTarget(target, request.RequestUri);
        return base.SendAsync(request, cancellationToken);
    }
}
