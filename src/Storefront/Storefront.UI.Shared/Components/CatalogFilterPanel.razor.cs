using System.Globalization;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Storefront.UI.Shared.Components;

public partial class CatalogFilterPanel
{
    [Parameter] public CatalogControls Initial { get; set; } = new();
    [Parameter] public string InstanceId { get; set; } = "sf-catalog";
    [Parameter] public bool Compact { get; set; }
    [Parameter]
    public EventCallback<CatalogFilterChange> Change
    {
        get; set;
    }
    private string _from = "", _to = "", _error = "";
    private bool _hasPriceRange;
    private double _priceMin, _priceMax, _priceStep = 1;
    private (double Start, double End) _priceRange;

    protected override void OnInitialized()
    {
        _from = Initial.PriceFrom;
        _to = Initial.PriceTo;
        if (Compact && TryPrice(Initial.AvailableFrom, out var min) && TryPrice(Initial.AvailableTo, out var max) && min < max)
        {
            _hasPriceRange = true;
            _priceMin = (double)min;
            _priceMax = (double)max;
            var start = TryPrice(_from, out var from) ? Math.Clamp(from, min, max) : min;
            var end = TryPrice(_to, out var to) ? Math.Clamp(to, start, max) : max;
            _priceRange = ((double)start, (double)end);
            _priceStep = min % 1 == 0 && max % 1 == 0 && start % 1 == 0 && end % 1 == 0 ? 1 : 0.01;
        }
    }

    private void UpdatePriceFrom(string value)
    {
        _from = value;
        SyncPriceRange();
    }

    private void UpdatePriceTo(string value)
    {
        _to = value;
        SyncPriceRange();
    }

    private void SyncPriceRange()
    {
        if (!_hasPriceRange || !TrySelectedRange(out var from, out var to))
            return;
        _priceRange = ((double)from, (double)to);
        if (from % 1 != 0 || to % 1 != 0)
            _priceStep = 0.01;
        _error = "";
    }

    private void UpdatePriceRange((double Start, double End) value)
    {
        _priceRange = value;
        _from = FormatPrice(decimal.Round((decimal)value.Start, 2));
        _to = FormatPrice(decimal.Round((decimal)value.End, 2));
        _error = "";
    }

    private bool TrySelectedRange(out decimal from, out decimal to)
    {
        from = (decimal)_priceMin;
        to = (decimal)_priceMax;
        return (_from.Trim().Length == 0 || TryPrice(_from, out from))
            && (_to.Trim().Length == 0 || TryPrice(_to, out to))
            && from >= (decimal)_priceMin && to <= (decimal)_priceMax && from <= to;
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
            _error = Initial.Labels.Get("Common.Error");
            return Task.CompletedTask;
        }
        if (_hasPriceRange && !TrySelectedRange(out _, out _))
        {
            _error = "قیمت‌ها باید در بازهٔ نمایش‌داده‌شده باشند و قیمت شروع از قیمت پایان بیشتر نباشد.";
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
        if (!TryPrice(input, out var number))
            return false;
        value = FormatPrice(number);
        return true;
    }

    private bool TryPrice(string input, out decimal value)
    {
        input = input.Trim();
        if (Compact)
        {
            var digits = input.ToCharArray();
            for (var i = 0; i < digits.Length; i++)
            {
                if (digits[i] is >= '۰' and <= '۹')
                    digits[i] = (char)('0' + digits[i] - '۰');
                else if (digits[i] is >= '٠' and <= '٩')
                    digits[i] = (char)('0' + digits[i] - '٠');
                else if (digits[i] == '٫')
                    digits[i] = '.';
            }
            input = new string(digits);
        }
        if (Initial.DecimalSeparator != ".")
            input = input.Replace(Initial.DecimalSeparator, ".");
        return decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value >= 0;
    }

    private string FormatPrice(decimal value) => value.ToString(CultureInfo.InvariantCulture).Replace(".", Initial.DecimalSeparator);
}
