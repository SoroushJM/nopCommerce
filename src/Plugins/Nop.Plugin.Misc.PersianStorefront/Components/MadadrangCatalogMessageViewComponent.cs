using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Misc.PersianStorefront.Components;

public sealed class MadadrangCatalogMessageViewComponent : NopViewComponent
{
    public IViewComponentResult Invoke(string message, bool warning = false)
    {
        ViewData["Warning"] = warning;
        return View("~/Plugins/Misc.PersianStorefront/Views/CatalogMessage.cshtml", message);
    }
}