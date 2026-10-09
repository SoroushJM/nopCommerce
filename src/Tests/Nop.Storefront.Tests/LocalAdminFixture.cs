using System.Net;
using System.Text.Json;

namespace Nop.Storefront.Tests;

// Administrative mutations are opt-in and require the server's own guarded fixture page.
public sealed class LocalAdminFixtureAttribute() : SkipAttribute("Set STOREFRONT_TEST_ADMIN_FIXTURE=1 for isolated local administrative tests.")
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        return Task.FromResult(Environment.GetEnvironmentVariable("STOREFRONT_TEST_ADMIN_FIXTURE") != "1");
    }
}

internal static class LocalAdminFixture
{
    public static async Task SignInAsync(StorefrontClient client)
    {
        var path = Environment.GetEnvironmentVariable("STOREFRONT_TEST_PRIVATE_SETTINGS") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MadadrangDev", "local-settings.json");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        if (settings.RootElement.GetProperty("Database").GetString() != "nop_stationery_dev")
            throw new InvalidOperationException("Administrator tests require the isolated development fixture.");

        await client.InitializeAsync("login?returnurl=%2FAdmin");
        using var login = await client.PostFormAsync("login?returnurl=%2FAdmin", new()
        {
            ["Email"] = settings.RootElement.GetProperty("AdminEmail").GetString()!,
            ["Password"] = settings.RootElement.GetProperty("AdminPassword").GetString()!,
            ["__RequestVerificationToken"] = client.Token
        });
        if (login.StatusCode != HttpStatusCode.Redirect)
            throw new InvalidOperationException("Native administrator authentication failed.");
        var configure = await client.InitializeAsync("Admin/PersianStorefrontMaintenance/Configure");
        if (!configure.Contains("id=\"madadrang-fixture-repair\"", StringComparison.Ordinal))
            throw new InvalidOperationException("The server did not confirm an isolated local development database.");
    }
}