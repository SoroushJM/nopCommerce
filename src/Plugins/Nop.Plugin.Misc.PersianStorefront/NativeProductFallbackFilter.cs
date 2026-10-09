using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
using Nop.Services.Themes;
using Nop.Web.Controllers;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed class NativeProductFallbackFilter(IThemeContext themes) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor action
            && action.ControllerTypeInfo.AsType() == typeof(ProductController)
            && action.MethodInfo.Name is nameof(ProductController.ProductDetails) or nameof(ProductController.ProductReviewsAdd)
            && context.Result is ViewResult { ViewName: "ProductTemplate.Simple", Model: ProductDetailsModel model } view
            && !NativeProductPresentation.Supports(model)
            && await themes.GetWorkingThemeNameAsync() == "Madadrang")
            view.ViewName = "~/Views/Product/ProductTemplate.Simple.cshtml";

        await next();
    }
}