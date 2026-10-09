namespace Storefront.UI;

public sealed record StoreMenu
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public List<StoreMenuItem> Items { get; init; } = [];
}

public sealed record StoreMenuItem
{
    public int Id { get; init; }
    public int EntityId { get; init; }
    public int EntityType { get; init; }
    public string Title { get; init; } = "";
    public string Url { get; init; } = "";
    public string CssClass { get; init; } = "";
    public List<StoreMenuItem> Children { get; init; } = [];
}

public sealed record StoreNavigation
{
    public StoreMenu Main { get; init; } = new();
    public List<StoreMenu> Footer { get; init; } = [];
    public bool SignedIn { get; init; }
    public bool CartEnabled { get; init; }
    public int CartCount { get; init; }
}