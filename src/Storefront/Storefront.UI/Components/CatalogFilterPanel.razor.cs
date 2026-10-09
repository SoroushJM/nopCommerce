using System.Globalization;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Storefront.UI.Components;

public partial class CatalogFilterPanel
{
    [Parameter] public CatalogControls Initial { get; set; } = new();
    [Parameter] public string InstanceId { get; set; } = "sf-catalog";
    [Parameter]
    public EventCallback<CatalogFilterChange> Change
    {
        get; set;
    }
    private string _from = "", _to = "", _error = "";

    protected override void OnInitialized()
    {
        _from = Initial.PriceFrom;
        _to = Initial.PriceTo;
    }

    private static IEnumerable<SelectOption<string>> Options(List<CatalogChoice> choices)
    {
        return choices.Select(choice => new SelectOption<string>(choice.Value, choice.Text));
    }

    private static string Selected(List<CatalogChoice> choices)
    {
        return choices.FirstOrDefault(choice => choice.Selected)?.Value ?? "0";
    }

    private Task Toggle(string key, string id, bool enabled)
    {
        var choices = key == "ms" ? Initial.Manufacturers : Initial.Specifications.SelectMany(group => group.Options).ToList();
        var values = choices.Where(option => option.Selected).Select(option => option.Value).ToHashSet();
        if (enabled)
            values.Add(id);
        else
            values.Remove(id);
        return Change.InvokeAsync(new(key, string.Join(',', values)));
    }

    private Task ApplyPrice()
    {
        if (!NormalizePrice(_from, out var from) || !NormalizePrice(_to, out var to))
        {
            _error = "قیمت را با عدد مثبت وارد کن.";
            return Task.CompletedTask;
        }
        _error = "";
        return Change.InvokeAsync(new("price", from.Length == 0 && to.Length == 0 ? null : from + "-" + to));
    }

    private bool NormalizePrice(string input, out string value)
    {
        value = "";
        input = input.Trim();
        if (input.Length == 0)
            return true;
        if (Initial.DecimalSeparator != ".")
            input = input.Replace(Initial.DecimalSeparator, ".");
        if (!decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) || number < 0)
            return false;
        value = number.ToString(CultureInfo.InvariantCulture).Replace(".", Initial.DecimalSeparator);
        return true;
    }
}