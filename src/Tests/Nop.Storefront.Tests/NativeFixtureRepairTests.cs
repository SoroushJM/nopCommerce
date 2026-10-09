using System.Net;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[LocalAdminFixture]
[NotInParallel("storefront-http")]
public sealed class NativeFixtureRepairTests
{
    [Test]
    public async Task ExplicitFixtureRepairIsIdempotentAndKeepsNativeHomeContents()
    {
        await using var client = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(client);
        var before = await client.InitializeAsync("home/index");
        for (var run = 0; run < 2; run++)
        {
            await client.InitializeAsync("Admin/PersianStorefrontMaintenance/Configure");
            using var response = await client.PostFormAsync("Admin/PersianStorefrontMaintenance/Configure",
                new() { ["__RequestVerificationToken"] = client.Token });
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
        var after = await client.InitializeAsync("home/index");
        foreach (var marker in new[] { "data-productid=\"[0-9]+\"", "data-categoryid=\"[0-9]+\"", "<h2 class=\"text-sm font-bold mb-4\">[^<]+</h2>" })
            await Assert.That(Regex.Matches(before, marker).Select(match => match.Value).SequenceEqual(Regex.Matches(after, marker).Select(match => match.Value))).IsTrue();
        await Assert.That(after.Contains("Welcome to our store", StringComparison.Ordinal)).IsFalse();
    }
}