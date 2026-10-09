using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Storefront.UI.Components;

public partial class NativeFormSelect
{
    [Parameter] public NativeFormSelectInput Initial { get; set; } = new();
    private string _value = "";
    private ElementReference _input;
    private IJSObjectReference? _module;
    private IEnumerable<SelectOption<string>> Options => Initial.Options.Select(option => new SelectOption<string>(option.Value, option.Text));

    protected override void OnInitialized()
    {
        _value = Initial.Options.FirstOrDefault(option => option.Selected)?.Value ?? Initial.Options.FirstOrDefault()?.Value ?? "";
    }

    private async Task SetValue(string? value)
    {
        _value = value ?? "";
        _module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Storefront.UI/storefront.js");
        await _module.InvokeVoidAsync("submitNativeSelect", _input, _value, Initial.SubmitName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
            await _module.DisposeAsync();
    }
}