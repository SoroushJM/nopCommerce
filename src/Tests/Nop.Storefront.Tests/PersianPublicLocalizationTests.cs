using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Nop.Core.Domain.Localization;
using Nop.Plugin.Misc.PersianStorefront;
using Nop.Services.Localization;

namespace Nop.Storefront.Tests;

// Resource-pack and upgrade contracts; no website or database required.
public sealed class PersianPublicLocalizationTests
{
    [Test]
    [Arguments("متن اختصاصی قبلی", null)]
    [Arguments("متن اختصاصی قبلی", "متن اختصاصی جدید")]
    [Arguments(null, null)]
    [Arguments("  ", null)]
    public async Task RenamedWithdrawalResourcePreservesMerchantTextAndIsIdempotent(string? legacyValue, string? currentValue)
    {
        const string legacyKey = "ReturnRequests.WithdrawalForm.ConfirnationText";
        const string currentKey = "ReturnRequests.WithdrawalForm.ConfirmationText";
        var localization = DispatchProxy.Create<ILocalizationService, ResourceLocalization>();
        var state = (ResourceLocalization)(object)localization;
        if (legacyValue != null)
            state.Resources[legacyKey] = legacyValue;
        if (currentValue != null)
            state.Resources[currentKey] = currentValue;

        await PersianPublicResourcesStartup.PreserveRenamedWithdrawalResourceAsync(localization, 7);
        await PersianPublicResourcesStartup.PreserveRenamedWithdrawalResourceAsync(localization, 7);

        var expected = currentValue ?? (string.IsNullOrWhiteSpace(legacyValue) ? null : legacyValue);
        await Assert.That(state.Resources.GetValueOrDefault(currentKey)).IsEqualTo(expected);
        await Assert.That(state.Resources.GetValueOrDefault(legacyKey)).IsEqualTo(legacyValue);
        await Assert.That(state.Writes).IsEqualTo(currentValue == null && !string.IsNullOrWhiteSpace(legacyValue) ? 1 : 0);
    }

    public class ResourceLocalization : DispatchProxy
    {
        public Dictionary<string, string> Resources { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int Writes { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ILocalizationService.GetLocaleStringResourceByNameAsync))
            {
                if ((int)args![1]! != 7 || (bool)args[2]!)
                    throw new InvalidOperationException("Only the selected Persian language may be queried, without missing-key warnings.");
                var key = (string)args[0]!;
                var resource = Resources.TryGetValue(key, out var value)
                    ? new LocaleStringResource { LanguageId = 7, ResourceName = key, ResourceValue = value }
                    : null;
                return Task.FromResult(resource);
            }
            if (targetMethod?.Name == nameof(ILocalizationService.AddOrUpdateLocaleResourceAsync))
            {
                if ((int?)args![1] != 7)
                    throw new InvalidOperationException("Only the selected Persian language may be updated.");
                foreach (var pair in (IDictionary<string, string>)args[0]!)
                    Resources[pair.Key] = pair.Value;
                Writes++;
                return Task.CompletedTask;
            }
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShippedPublicPackWinsOverAnOldOrMissingPersistentFile(bool oldFileExists)
    {
        var path = Path.Combine(Path.GetTempPath(), $"storefront-locale-{Guid.NewGuid():N}.xml");
        try
        {
            if (oldFileExists)
                await File.WriteAllTextAsync(path, "<Language><LocaleResource Name=\"OldMountedPack\"><Value>old</Value></LocaleResource></Language>");
            using var reader = Nop.Plugin.Misc.PersianStorefront.PersianPublicResourcesStartup.OpenResourcePack(path);
            await Assert.That(reader).IsNotNull();
            var resources = XDocument.Load(reader!).Root!.Elements("LocaleResource").ToArray();
            await Assert.That(resources.Length).IsEqualTo(_translations.Length);
            await Assert.That(resources.Any(item => item.Attribute("Name")!.Value == "OldMountedPack")).IsFalse();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

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
    [Arguments(@"%[A-Za-z][A-Za-z0-9_.]*%", true)]
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

    [Test]
    public async Task EveryNativePublicResourceHasAPersianTranslationInTheSharedPacks()
    {
        var admin = XDocument.Load(Path.Combine(_directory, "PersianAdmin", "fa-IR.admin.xml"))
            .Root!.Elements("LocaleResource");
        var translated = _translations.Concat(admin)
            .Where(item => !string.IsNullOrWhiteSpace(item.Element("Value")?.Value))
            .Select(item => item.Attribute("Name")!.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        // The exact native source is authoritative, including root labels, route literals,
        // validation, accessibility text and technical keys outside the Admin. namespace.
        var nativePublic = _source.Keys.Where(name => !name.StartsWith("Admin.", StringComparison.OrdinalIgnoreCase)).ToArray();
        await Assert.That(nativePublic.Where(name => !translated.Contains(name))).IsEmpty();
        await Assert.That(nativePublic.Length).IsEqualTo(1718);
    }
}
