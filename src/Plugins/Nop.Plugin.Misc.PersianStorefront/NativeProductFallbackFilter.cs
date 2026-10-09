using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Services.Themes;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed class NativeProductFallbackFilter(IThemeContext themes) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ViewResult { ViewName: "ProductTemplate.Simple", Model: ProductDetailsModel model } view
            && !NativeProductPresentation.Supports(model)
            && await themes.GetWorkingThemeNameAsync() == "Madadrang")
            view.ViewName = "~/Views/Product/ProductTemplate.Simple.cshtml";

        await next();
    }
}