using System.Net;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[LocalAdminFixture]
[NotInParallel("storefront-http")]
public sealed class NativeMenuAdministrationTests
{
    [Test]
    public async Task FooterReflectsNestedAndUnpublishedAdminMenuItems()
    {
        await using var admin = new StorefrontClient();
        await using var guest = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(admin);
        var original = await guest.InitializeAsync("home/index");
        var tile = Regex.Match(original, "<a href=\"([^\"]+)\"[^>]+data-categoryid=\"([0-9]+)\"");
        await Assert.That(tile.Success).IsTrue();
        var categoryId = tile.Groups[2].Value;
        var categoryUrl = WebUtility.HtmlDecode(tile.Groups[1].Value);
        var name = "آزمون منو " + Guid.NewGuid().ToString("N");
        var parentTitle = "شاخهٔ آزمایشی " + Guid.NewGuid().ToString("N");
        var menuId = 0;
        var itemIds = new List<int>();
        try
        {
            await admin.InitializeAsync("Admin/Menu/Create");
            menuId = await Create(admin, "Admin/Menu/Create", new()
            {
                ["Name"] = name,
                ["MenuTypeId"] = "10",
                ["DisplayOrder"] = "1000",
                ["Published"] = "true"
            }, "Edit");
            await admin.InitializeAsync($"Admin/Menu/MenuItemCreate?menuId={menuId}");
            var parent = await Create(admin, "Admin/Menu/MenuItemCreate", new()
            {
                ["MenuId"] = menuId.ToString(),
                ["Title"] = parentTitle,
                ["MenuItemTypeId"] = "7",
                ["Published"] = "true"
            }, "MenuItemEdit");
            itemIds.Add(parent);
            await admin.InitializeAsync($"Admin/Menu/MenuItemCreate?menuId={menuId}");
            var childFields = new Dictionary<string, string>
            {
                ["MenuId"] = menuId.ToString(),
                ["ParentId"] = parent.ToString(),
                ["MenuItemTypeId"] = "1",
                ["CategoryId"] = categoryId,
                ["Published"] = "true"
            };
            var child = await Create(admin, "Admin/Menu/MenuItemCreate", childFields, "MenuItemEdit");
            itemIds.Add(child);
            var home = WebUtility.HtmlDecode(await guest.InitializeAsync("home/index"));
            var branch = home[home.IndexOf(name, home.IndexOf("<footer", StringComparison.Ordinal), StringComparison.Ordinal)..];
            await Assert.That(branch.Contains(parentTitle, StringComparison.Ordinal)).IsTrue();
            await Assert.That(branch.Contains($"href=\"{categoryUrl}\"", StringComparison.Ordinal)).IsTrue();

            await admin.InitializeAsync($"Admin/Menu/MenuItemEdit/{child}");
            childFields["Id"] = child.ToString();
            childFields["Published"] = "false";
            childFields["__RequestVerificationToken"] = admin.Token;
            childFields["save-continue"] = "true";
            using var unpublished = await admin.PostFormAsync($"Admin/Menu/MenuItemEdit/{child}", childFields);
            await Assert.That(unpublished.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            home = WebUtility.HtmlDecode(await guest.InitializeAsync("home/index"));
            branch = home[home.IndexOf(name, home.IndexOf("<footer", StringComparison.Ordinal), StringComparison.Ordinal)..];
            await Assert.That(branch.Contains(parentTitle, StringComparison.Ordinal)).IsTrue();
            await Assert.That(branch.Contains($"href=\"{categoryUrl}\"", StringComparison.Ordinal)).IsFalse();
        }
        finally
        {
            foreach (var item in itemIds.AsEnumerable().Reverse())
            {
                await admin.InitializeAsync($"Admin/Menu/MenuItemEdit/{item}");
                using var removed = await admin.PostFormAsync($"Admin/Menu/MenuItemDelete/{item}", new()
                {
                    ["__RequestVerificationToken"] = admin.Token
                });
                await Assert.That(removed.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            }
            if (menuId > 0)
            {
                await admin.InitializeAsync($"Admin/Menu/Edit/{menuId}");
                using var removed = await admin.PostFormAsync($"Admin/Menu/Delete/{menuId}", new()
                {
                    ["__RequestVerificationToken"] = admin.Token
                });
                await Assert.That(removed.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            }
        }
    }

    private static async Task<int> Create(StorefrontClient admin, string path, Dictionary<string, string> fields, string editAction)
    {
        fields["__RequestVerificationToken"] = admin.Token;
        fields["save-continue"] = "true";
        using var response = await admin.PostFormAsync(path, fields);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        var match = Regex.Match(response.Headers.Location?.OriginalString ?? "", $"/{editAction}/([0-9]+)", RegexOptions.IgnoreCase);
        await Assert.That(match.Success).IsTrue();
        return int.Parse(match.Groups[1].Value);
    }
}