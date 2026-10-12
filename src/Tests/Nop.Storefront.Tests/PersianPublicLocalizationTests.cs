using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Nop.Storefront.Tests;

// Source-pack contracts only; no website or database required.
public sealed class PersianPublicLocalizationTests
{
    private static readonly string _directory = Path.Combine(AppContext.BaseDirectory, "Localization");
    private static readonly XElement[] _translations = XDocument.Load(Path.Combine(_directory, "PersianPublic", "fa-IR.public.xml"))
        .Root!.Elements("LocaleResource").ToArray();
    private static readonly Dictionary<string, string> _source = XDocument.Load(Path.Combine(_directory, "defaultResources.nopres.xml"))
        .Root!.Elements("LocaleResource").ToDictionary(item => item.Attribute("Name")!.Value,
            item => item.Element("Value")!.Value, StringComparer.OrdinalIgnoreCase);

    [Test]
    public async Task PublicResourceNamesAreUniqueKnownAndContainTranslations()
    {
        var names = _translations.Select(item => item.Attribute("Name")!.Value).ToArray();
        await Assert.That(names.Distinct(StringComparer.OrdinalIgnoreCase).Count()).IsEqualTo(names.Length);
        await Assert.That(names.Where(name => !_source.ContainsKey(name) || name.StartsWith("Admin.", StringComparison.OrdinalIgnoreCase))).IsEmpty();
        foreach (var item in _translations)
            await Assert.That(string.IsNullOrWhiteSpace(item.Element("Value")?.Value)).IsFalse();
    }

    [Test]
    [Arguments(@"\{\d+[^}]*\}", true)]
    [Arguments(@"<[^>]+>", false)]
    public async Task TranslationsPreserveNativeFormattingTokensAndLinkMarkup(string pattern, bool sort)
    {
        foreach (var item in _translations)
        {
            var name = item.Attribute("Name")!.Value;
            var expected = Regex.Matches(_source[name], pattern).Select(match => match.Value);
            var actual = Regex.Matches(item.Element("Value")!.Value, pattern).Select(match => match.Value);
            if (sort)
            {
                expected = expected.Order(StringComparer.Ordinal);
                actual = actual.Order(StringComparer.Ordinal);
            }
            await Assert.That(actual.SequenceEqual(expected)).IsTrue().Because($"public translation {name} must preserve native tokens");
        }
    }

    [Test]
    public async Task ReportedSharedHeaderFooterAndCatalogLabelsArePresent()
    {
        string[] required = ["Search.Button", "Account.Login", "Wishlist.HeaderQuantity", "ShoppingCart.HeaderQuantity",
            "Footer.FollowUs", "Newsletter.Title", "Content.CopyrightNotice", "Catalog.OrderBy", "Catalog.PageSize",
            "Catalog.Products.NoResult", "Checkout.Button", "Account.CustomerInfo", "Products.Compare.Title"];
        var names = _translations.Select(item => item.Attribute("Name")!.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        await Assert.That(required.Where(name => !names.Contains(name))).IsEmpty();
    }
}
