using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Configuration;
using Nop.Services.Media;
using Nop.Services.Security;
using Nop.Services.Themes;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.PersianStorefront.Controllers;

public record BoutiqueThemeConfigurationModel : BaseNopModel
{
    [UIHint("Picture")]
    public int HeroPictureId { get; set; }
    [StringLength(160)]
    public string HeroHeading { get; set; } = "";
    [StringLength(600)]
    public string HeroDescription { get; set; } = "";
}

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public sealed class StorefrontThemeSettingsController(IThemeContext themes, IStoreContext stores,
    ISettingService settings, IPictureService pictures) : BasePluginController
{
    [CheckPermission(StandardPermission.Configuration.MANAGE_SETTINGS)]
    public async Task<IActionResult> Configure()
    {
        if (await themes.GetWorkingThemeNameAsync() != "Boutique")
            return RedirectToAction("Configure", "PersianStorefrontMaintenance");
        var storeId = (await stores.GetCurrentStoreAsync()).Id;
        var saved = await settings.LoadSettingAsync<BoutiqueThemeSettings>(storeId);
        return View("~/Plugins/Misc.PersianStorefront/Views/BoutiqueConfigure.cshtml", new BoutiqueThemeConfigurationModel
        {
            HeroPictureId = saved.HeroPictureId, HeroHeading = saved.HeroHeading, HeroDescription = saved.HeroDescription
        });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SETTINGS)]
    public async Task<IActionResult> Configure(BoutiqueThemeConfigurationModel model)
    {
        if (await themes.GetWorkingThemeNameAsync() != "Boutique")
            return Forbid();
        if (model.HeroPictureId < 0 || model.HeroPictureId > 0 && await pictures.GetPictureByIdAsync(model.HeroPictureId) == null)
            ModelState.AddModelError(nameof(model.HeroPictureId), "تصویر انتخاب‌شده موجود نیست.");
        if (!ModelState.IsValid)
            return View("~/Plugins/Misc.PersianStorefront/Views/BoutiqueConfigure.cshtml", model);
        var storeId = (await stores.GetCurrentStoreAsync()).Id;
        await settings.SaveSettingAsync(new BoutiqueThemeSettings
        {
            HeroPictureId = model.HeroPictureId,
            HeroHeading = model.HeroHeading?.Trim() ?? "",
            HeroDescription = model.HeroDescription?.Trim() ?? ""
        }, storeId);
        return RedirectToAction(nameof(Configure));
    }
}
