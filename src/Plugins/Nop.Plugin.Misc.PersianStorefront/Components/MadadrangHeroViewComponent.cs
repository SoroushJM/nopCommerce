using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Misc.PersianStorefront.Components;

public sealed class MadadrangHeroViewComponent : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        return await ViewAsync("~/Plugins/Misc.PersianStorefront/Views/HomeHero.cshtml");
    }
}