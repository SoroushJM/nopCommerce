using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Components;
using Storefront.UI;

namespace Nop.Plugin.Misc.PersianStorefront.Components;

public sealed class MadadrangFormSelectViewComponent : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string name, string label, IEnumerable<SelectListItem> options, string submitName, string? selectedValue = null)
    {
        var model = new NativeFormSelectInput
        {
            Name = name,
            Label = label,
            SubmitName = submitName,
            Options = options.Select(option => new CatalogChoice(option.Value, option.Text,
                selectedValue == null ? option.Selected : option.Value == selectedValue)).ToList()
        };
        return await ViewAsync("~/Plugins/Misc.PersianStorefront/Views/FormSelect.cshtml", model);
    }
}