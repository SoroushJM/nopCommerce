using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Text.Encodings.Web;
using Nop.Web.Components;
using Nop.Web.Framework.Controllers;
using Nop.Services.Themes;
using Nop.Web.Controllers;
using Nop.Web.Models.ShoppingCart;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed class NativeCartResponseFilter(IThemeContext themes, NativeCartPresentation presentation,
    IAntiforgery antiforgery, IUrlHelperFactory urls, IViewComponentHelper components,
    ITempDataDictionaryFactory tempData) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if ((request.Headers["X-Storefront-Cart"] == "1" || request.Headers["X-Madadrang-Cart"] == "1")
            && context.ActionDescriptor is ControllerActionDescriptor action
            && action.ControllerTypeInfo.AsType() == typeof(ShoppingCartController)
            && ((HttpMethods.IsGet(request.Method) && action.MethodInfo.Name == nameof(ShoppingCartController.Cart))
                || (HttpMethods.IsPost(request.Method) && action.MethodInfo.Name == nameof(ShoppingCartController.UpdateCart)))
            && context.Result is ViewResult { Model: ShoppingCartModel model } view
            && (!view.StatusCode.HasValue || view.StatusCode == StatusCodes.Status200OK)
            && StorefrontThemeService.IsSupported(await themes.GetWorkingThemeNameAsync()))
        {
            var token = antiforgery.GetAndStoreTokens(context.HttpContext);
            var cart = await presentation.FromAsync(model, urls.GetUrlHelper(context), token);
            if (cart.MiniEnabled)
            {
                using var writer = new StringWriter();
                ((IViewContextAware)components).Contextualize(new ViewContext(context, NullView.Instance,
                    view.ViewData, tempData.GetTempData(context.HttpContext), writer, new HtmlHelperOptions()));
                var flyout = await components.InvokeAsync(typeof(FlyoutShoppingCartViewComponent), null);
                flyout.WriteTo(writer, HtmlEncoder.Default);
                cart.NativeSections["updateflyoutcartsectionhtml"] = writer.ToString();
            }
            context.Result = new JsonResult(cart);
            context.HttpContext.Response.Headers.CacheControl = "no-store";
        }

        await next();
    }
}
