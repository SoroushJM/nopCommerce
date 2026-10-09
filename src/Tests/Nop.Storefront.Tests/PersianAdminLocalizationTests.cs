using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Nop.Storefront.Tests;

public sealed class PersianAdminLocalizationTests
{
    private static readonly string _directory = Path.Combine(AppContext.BaseDirectory, "Localization");
    private static readonly XElement[] _translations = Read("PersianAdmin/fa-IR.admin.xml");
    private static readonly Dictionary<string, string> _source = Read("defaultResources.nopres.xml")
        .Concat(Read("PersianAdmin/en-US.admin.xml"))
        .GroupBy(item => item.Attribute("Name")!.Value)
        .ToDictionary(group => group.Key, group => group.Last().Element("Value")?.Value ?? "");

    [Test]
    public async Task ResourceNamesAreUniqueAndKnown()
    {
        var names = _translations.Select(item => item.Attribute("Name")!.Value).ToArray();
        await Assert.That(names.Distinct().Count()).IsEqualTo(names.Length);
        await Assert.That(names.Where(name => !_source.ContainsKey(name)).ToArray()).IsEmpty();
    }

    [Test]
    [Arguments(@"\{\d+(?:[^}]*)\}", true)]
    [Arguments(@"<[^>]+>", false)]
    [Arguments(@"%[A-Za-z][A-Za-z0-9_.]*%", true)]
    public async Task TranslationPreservesSourceTokens(string pattern, bool sort)
    {
        foreach (var item in _translations)
        {
            var name = item.Attribute("Name")!.Value;
            if (!_source.TryGetValue(name, out var english))
                continue; // Reported independently by ResourceNamesAreUniqueAndKnown.
            var expected = Regex.Matches(english, pattern).Select(match => match.Value);
            var actual = Regex.Matches(item.Element("Value")?.Value ?? "", pattern).Select(match => match.Value);
            if (sort)
            { expected = expected.Order(StringComparer.Ordinal); actual = actual.Order(StringComparer.Ordinal); }
            await Assert.That(actual.SequenceEqual(expected)).IsTrue().Because($"translation {name} must preserve source tokens");
        }
    }

    [Test]
    public async Task TranslationsContainValuesAndNoTemporaryTokens()
    {
        foreach (var item in _translations)
        {
            var name = item.Attribute("Name")!.Value;
            var value = item.Element("Value")?.Value ?? "";
            await Assert.That(!string.IsNullOrWhiteSpace(value)).IsTrue().Because($"translation {name} must have a value");
            await Assert.That(value.Contains("QQTOKEN", StringComparison.Ordinal) || value.Contains("ZQZ", StringComparison.Ordinal))
                .IsFalse().Because($"translation {name} must not contain an unrestored token");
        }
    }

    [Test]
    public async Task RequiredAdministratorResourcePrefixesAreCovered()
    {
        string[] prefixes = ["Admin.", "Enums.", "ActivityLog.", "Pdf.", "PDFInvoice.", "Security.Permission.", "Common.", "Plugins.FriendlyName.", "Account.Login."];
        var translated = _translations.Select(item => item.Attribute("Name")!.Value).ToHashSet();
        var missing = _source.Keys.Where(name => prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)) && !translated.Contains(name)).ToArray();
        await Assert.That(missing).IsEmpty();
    }

    private static XElement[] Read(string relativePath) => XDocument.Load(Path.Combine(_directory, relativePath)).Root!.Elements().ToArray();
}