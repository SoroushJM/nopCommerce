using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
using Nop.Services.Themes;
using Nop.Web.Controllers;
using Nop.Web.Models.Catalog;
using Nop.Plugin.Misc.PersianStorefront.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed class NativeProductFallbackFilter(IThemeContext themes, IUrlHelperFactory urls) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Reuse the native ProductDetails model and its permissions, without rendering a second page.
        if (context.ActionDescriptor is ControllerActionDescriptor purchaseAction
            && purchaseAction.ControllerTypeInfo.AsType() == typeof(ProductController)
            && purchaseAction.MethodInfo.Name == nameof(ProductController.ProductDetails)
            && context.HttpContext.Request.Headers["X-Storefront-Purchase"] == "1"
            && await themes.GetWorkingThemeNameAsync() == "Madadrang"
            && context.Result is ViewResult { Model: ProductDetailsModel purchaseModel })
        {
            if (NativeProductPresentation.Supports(purchaseModel))
            {
                var component = ActivatorUtilities.CreateInstance<MadadrangProductViewComponent>(context.HttpContext.RequestServices);
                context.Result = new JsonResult(await component.PrepareAsync(purchaseModel, urls.GetUrlHelper(context)));
            }
            else
                context.Result = new JsonResult(new { message = "این محصول گزینه‌های خرید ویژه دارد و هنوز در پنجرهٔ خرید سریع پشتیبانی نمی‌شود." }) { StatusCode = 422 };
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            await next();
            return;
        }
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
