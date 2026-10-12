namespace Storefront.UI.Shared;

public sealed record NativeFormSelectInput
{
    public bool Rtl { get; init; } = true;
    public string Name { get; init; } = "";
    public string Label { get; init; } = "";
    public string SubmitName { get; init; } = "";
    public List<CatalogChoice> Options { get; init; } = [];
}
