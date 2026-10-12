namespace Nop.Storefront.Tests;

// Pure configuration checks: these tests never create a client or access a server/database.
public sealed class StorefrontTestTargetTests
{
    [Test]
    [Arguments("http://localhost:5202", "http://localhost:5202/")]
    [Arguments("http://127.0.0.1:5230/shop", "http://127.0.0.1:5230/shop/")]
    [Arguments("http://[::1]:5202/", "http://[::1]:5202/")]
    public async Task ExplicitIsolatedTargetsPreserveNativePathBase(string configured, string expected)
    {
        await Assert.That(StorefrontTestTarget.Resolve(configured, "1").AbsoluteUri).IsEqualTo(expected);
    }

    [Test]
    [Arguments(null, "1")]
    [Arguments("", "1")]
    [Arguments("not a URL", "1")]
    [Arguments("http://localhost:5202/", null)]
    [Arguments("http://localhost:5202/", "0")]
    [Arguments("http://localhost:5090/", "1")]
    [Arguments("http://127.0.0.1:5090/", "1")]
    [Arguments("http://[::1]:5090/", "1")]
    [Arguments("http://localhost/", "1")]
    [Arguments("https://localhost:5202/", "1")]
    [Arguments("http://example.com:5202/", "1")]
    [Arguments("http://user@localhost:5202/", "1")]
    [Arguments("http://localhost:5202/?query=1", "1")]
    [Arguments("http://localhost:5202/#fragment", "1")]
    public async Task MissingUnsafeOrUnconfirmedTargetsFailBeforeAnyNetworkRequest(string? url, string? isolated)
    {
        await Assert.That(() => StorefrontTestTarget.Resolve(url, isolated)).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments("http://localhost:5090/cart")]
    [Arguments("http://localhost:5230/cart")]
    [Arguments("https://localhost:5202/cart")]
    [Arguments("http://example.com:5202/cart")]
    [Arguments("http://user@localhost:5202/cart")]
    public async Task AbsoluteLinksCannotEscapeTheConfiguredHost(string requestUrl)
    {
        var target = StorefrontTestTarget.Resolve("http://localhost:5202/", "1");
        await Assert.That(() => StorefrontTestTarget.EnsureSameTarget(target, new Uri(requestUrl))).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task NativeRoutesCanUseTheConfiguredHost()
    {
        var target = StorefrontTestTarget.Resolve("http://localhost:5202/shop/", "1");
        StorefrontTestTarget.EnsureSameTarget(target, new Uri(target, "cart"));
        await Assert.That(new Uri(target, "cart").AbsolutePath).IsEqualTo("/shop/cart");
    }
}
