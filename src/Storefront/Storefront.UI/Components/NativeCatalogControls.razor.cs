using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using BlazorBlueprint.Primitives.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace Storefront.UI.Components;

public partial class NativeCatalogControls
{
    [Parameter] public CatalogControls Initial { get; set; } = new();
    [Parameter] public string Placement { get; set; } = "filters";
    [Inject] private IFocusManager FocusManager { get; set; } = default!;
    private bool _open, _restoreFocus;
    private ElementReference _filterTrigger;

    private void SetOpen(bool value)
    {
        _restoreFocus = _open && !value;
        _open = value;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_restoreFocus)
            return;
        _restoreFocus = false;
        await FocusManager.FocusFirst(_filterTrigger);
    }

    private static IEnumerable<SelectOption<string>> Options(List<CatalogChoice> choices)
    {
        return choices.Select(choice => new SelectOption<string>(choice.Value, choice.Text));
    }

    private static string Selected(List<CatalogChoice> choices)
    {
        return choices.FirstOrDefault(choice => choice.Selected)?.Value ?? "";
    }

    private void Change(CatalogFilterChange change)
    {
        var query = Initial.Query.ToDictionary(pair => pair.Key, pair => (string?)pair.Value);
        query.Remove("pagenumber");
        if (change.Key == "reset")
        {
            foreach (var key in new[] { "price", "ms", "specs", "mid", "cid", "vid", "sid", "sit", "isc", "advs" })
                query.Remove(key);
        }
        else if (string.IsNullOrWhiteSpace(change.Value) || change.Value == "0" && change.Key is "cid" or "mid")
            query.Remove(change.Key);
        else
            query[change.Key] = change.Value;
        if (Initial.NativeSearch && change.Key is "cid" or "mid")
            query["advs"] = "true";
        Navigation.NavigateTo(QueryHelpers.AddQueryString(Initial.Path, query), forceLoad: true);
    }
}