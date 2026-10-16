using System.Text.Json;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed record MishaShopSeedColor(string Name, string Hex);

public sealed record MishaShopSeedProduct(int Id, string Name, string Slug, string Category, string CategoryKey,
    decimal PriceInToman, decimal OldPriceInToman, string Description, string Image,
    IReadOnlyList<MishaShopSeedColor> Colors, IReadOnlyList<string> Sizes)
{
    public string Sku => $"MSHA-DEV-{Id:000}";
}

// Independent copies of the local clothing illustrations; no network or legacy SQL execution.
public static class MishaShopSampleCatalog
{
    private const string ResourcePrefix = "Nop.Plugin.Misc.PersianStorefront.DevelopmentSeed.MishaShop.";
    private static readonly Lazy<IReadOnlyList<MishaShopSeedProduct>> Catalog = new(() =>
    {
        using var stream = typeof(MishaShopSampleCatalog).Assembly.GetManifestResourceStream(ResourcePrefix + "catalog.json")
            ?? throw new InvalidOperationException("MishaShop development catalog resource is missing.");
        return JsonSerializer.Deserialize<List<MishaShopSeedProduct>>(stream)
            ?? throw new InvalidOperationException("MishaShop development catalog resource is empty.");
    });

    public static IReadOnlyList<MishaShopSeedProduct> Products => Catalog.Value;

    public static async Task<Dictionary<string, byte[]>> ReadImagesAsync()
    {
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var image in Products.Select(product => product.Image).Distinct(StringComparer.Ordinal))
        {
            await using var stream = typeof(MishaShopSampleCatalog).Assembly.GetManifestResourceStream(ResourcePrefix + "Images." + image)
                ?? throw new InvalidOperationException($"MishaShop development image resource is missing: {image}");
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            images.Add(image, buffer.ToArray());
        }
        return images;
    }
}
