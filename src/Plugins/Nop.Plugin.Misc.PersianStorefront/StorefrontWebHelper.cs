using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Nop.Services.Helpers;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed class StorefrontWebHelper(IHostApplicationLifetime lifetime, IHttpContextAccessor accessor,
    StorefrontHostConfig ports) : WebHelper(lifetime, accessor)
{
    public override string GetStoreHost(bool useSsl)
    {
        var host = base.GetStoreHost(useSsl);
        if (string.IsNullOrEmpty(host) || useSsl == IsCurrentConnectionSecured())
            return host;
        var sourcePort = useSsl ? ports.HttpPort : ports.HttpsPort;
        var targetPort = useSsl ? ports.HttpsPort : ports.HttpPort;
        // Other ports may belong to a reverse proxy; retain their original authority.
        var authority = _httpContextAccessor.HttpContext!.Request.Host;
        if (!sourcePort.HasValue || targetPort is not (> 0 and <= 65535) || authority.Port != sourcePort)
            return host;
        authority = new HostString(authority.Host, targetPort.Value);
        return $"{(useSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp)}://{authority}/";
    }
}