using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Services.Themes;
using Nop.Web.Controllers;
using Nop.Web.Models.ShoppingCart;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed class NativeCartResponseFilter(IThemeContext themes, NativeCartPresentation presentation,
    IAntiforgery antiforgery, IUrlHelperFactory urls) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.Headers["X-Madadrang-Cart"] == "1"
            && context.ActionDescriptor is ControllerActionDescriptor action
            && action.ControllerTypeInfo.AsType() == typeof(ShoppingCartController)
            && ((HttpMethods.IsGet(request.Method) && action.MethodInfo.Name == nameof(ShoppingCartController.Cart))
                || (HttpMethods.IsPost(request.Method) && action.MethodInfo.Name == nameof(ShoppingCartController.UpdateCart)))
            && context.Result is ViewResult { Model: ShoppingCartModel model } view
            && (!view.StatusCode.HasValue || view.StatusCode == StatusCodes.Status200OK)
            && await themes.GetWorkingThemeNameAsync() == "Madadrang")
        {
            var token = antiforgery.GetAndStoreTokens(context.HttpContext);
            context.Result = new JsonResult(await presentation.FromAsync(model, urls.GetUrlHelper(context), token));
            context.HttpContext.Response.Headers.CacheControl = "no-store";
        }

        await next();
    }
}