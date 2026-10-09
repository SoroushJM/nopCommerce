namespace Storefront.UI;

public sealed record NativeFormSelectInput
{
    public string Name { get; init; } = "";
    public string Label { get; init; } = "";
    public string SubmitName { get; init; } = "";
    public List<CatalogChoice> Options { get; init; } = [];
}