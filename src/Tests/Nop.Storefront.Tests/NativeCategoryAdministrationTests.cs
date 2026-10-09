using System.Net;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[LocalAdminFixture]
[NotInParallel("storefront-http")]
public sealed class NativeCategoryAdministrationTests
{
    [Test]
    public async Task HomepageReflectsAddedRenamedAndUnpublishedCategoriesWithoutBuild()
    {
        await using var admin = new StorefrontClient();
        await using var guest = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(admin);
        var original = await guest.InitializeAsync("home/index");
        var count = Regex.Matches(original, "data-categoryid=\"[0-9]+\"").Count;
        var name = "آزمون دسته " + Guid.NewGuid().ToString("N");
        var slug = "tunit-category-" + Guid.NewGuid().ToString("N");
        var id = 0;
        try
        {
            await admin.InitializeAsync("Admin/Category/Create");
            using var created = await admin.PostFormAsync("Admin/Category/Create", Fields(admin, name, slug));
            await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            var match = Regex.Match(created.Headers.Location?.OriginalString ?? "", "/Edit/([0-9]+)", RegexOptions.IgnoreCase);
            await Assert.That(match.Success).IsTrue();
            id = int.Parse(match.Groups[1].Value);
            var home = WebUtility.HtmlDecode(await guest.InitializeAsync("home/index"));
            await Assert.That(Regex.Matches(home, "data-categoryid=\"[0-9]+\"").Count).IsEqualTo(count + 1);
            await Assert.That(home.Contains(name, StringComparison.Ordinal)).IsTrue();
            await Assert.That(home.Contains($"data-categoryid=\"{id}\"", StringComparison.Ordinal)).IsTrue();
            using var category = await guest.GetAsync(slug);
            await Assert.That(category.StatusCode).IsEqualTo(HttpStatusCode.OK);

            await admin.InitializeAsync($"Admin/Category/Edit/{id}");
            var renamed = name + " تغییر نام";
            using var edited = await admin.PostFormAsync($"Admin/Category/Edit/{id}", Fields(admin, renamed, slug, id));
            await Assert.That(edited.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            home = WebUtility.HtmlDecode(await guest.InitializeAsync("home/index"));
            await Assert.That(home.Contains(renamed, StringComparison.Ordinal)).IsTrue();

            await admin.InitializeAsync($"Admin/Category/Edit/{id}");
            using var unpublished = await admin.PostFormAsync($"Admin/Category/Edit/{id}", Fields(admin, renamed, slug, id, published: false));
            await Assert.That(unpublished.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            home = await guest.InitializeAsync("home/index");
            await Assert.That(home.Contains($"data-categoryid=\"{id}\"", StringComparison.Ordinal)).IsFalse();
            await Assert.That(Regex.Matches(home, "data-categoryid=\"[0-9]+\"").Count).IsEqualTo(count);
        }
        finally
        {
            if (id > 0)
            {
                await admin.InitializeAsync($"Admin/Category/Edit/{id}");
                using var deleted = await admin.PostFormAsync($"Admin/Category/Delete/{id}", new()
                {
                    ["__RequestVerificationToken"] = admin.Token
                });
                await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            }
        }
    }

    private static Dictionary<string, string> Fields(StorefrontClient client, string name, string slug, int id = 0, bool published = true)
    {
        return new()
        {
            ["Id"] = id.ToString(),
            ["Name"] = name,
            ["SeName"] = slug,
            ["CategoryTemplateId"] = "1",
            ["PageSize"] = "12",
            ["PageSizeOptions"] = "12,24,36",
            ["AllowCustomersToSelectPageSize"] = "true",
            ["ShowOnHomepage"] = "true",
            ["Published"] = published.ToString(),
            ["DisplayOrder"] = "100",
            ["save-continue"] = "true",
            ["__RequestVerificationToken"] = client.Token
        };
    }
}