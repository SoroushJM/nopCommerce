using Nop.Core;
using Nop.Services.Localization;
using Storefront.UI.Shared;

namespace Nop.Plugin.Misc.PersianStorefront;

public static class NativeUiLocalization
{
    public static async Task<NativeUiLabels> PrepareAsync(ILocalizationService localization, IWorkContext work)
    {
        var language = await work.GetWorkingLanguageAsync();
        var values = new Dictionary<string, string>();
        foreach (var resource in NativeUiLabels.Defaults.Keys)
            values[resource] = await localization.GetResourceAsync(resource, language.Id);
        return new NativeUiLabels { Rtl = language.Rtl, Values = values };
    }
}
