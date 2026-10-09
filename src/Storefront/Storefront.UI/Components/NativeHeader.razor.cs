using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Storefront.UI;

namespace Storefront.UI.Components;

public partial class NativeHeader
{
    [Parameter]
    public StoreNavigation Initial { get; set; } = new();

    private IJSObjectReference? module;
    private DotNetObjectReference<NativeHeader>? _reference;
    private NativeCart cart = new();
    private bool _signedIn, cartOpen, loginOpen, otpSent, busy;
    private int _count, _listener;
    private string error = "", phone = "", code = "", testCode = "", loginMessage = "";
    protected override void OnInitialized()
    {
        _count = Initial.CartCount;
        _signedIn = Initial.SignedIn;
        cart = new NativeCart
        {
            SignedIn = Initial.SignedIn
        };
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        try
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Storefront.UI/storefront.js");
            _reference = DotNetObjectReference.Create(this);
            _listener = await module.InvokeAsync<int>("listenForCart", _reference);
            if (Initial.CartEnabled)
                await RefreshCart();
        }
        catch (Exception)
        {
            error = "بارگذاری سبد خرید انجام نشد. دوباره تلاش کنید.";
        }

        StateHasChanged();
    }

    private async Task<T> Api<T>(string path, string method = "GET", object? data = null)
    {
        return await module!.InvokeAsync<T>("request", "/stationery/api/" + path, method, data);
    }

    private async Task RefreshCart()
    {
        cart = await module!.InvokeAsync<NativeCart>("getNativeCart", Initial.CartUrl);
        _count = cart.Count;
        _signedIn = cart.SignedIn;
    }

    private async Task Run(Func<Task> action)
    {
        if (module == null || busy)
            return;
        busy = true;
        try
        {
            await action();
            error = "";
        }
        catch (Exception)
        {
            error = "درخواست انجام نشد. دوباره تلاش کنید.";
        }
        finally
        {
            busy = false;
        }
    }

    [JSInvokable]
    public async Task CartChanged(bool open)
    {
        await Run(RefreshCart);
        if (open && cart.MiniEnabled)
            cartOpen = true;
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCart()
    {
        await Run(RefreshCart);
        if (error.Length > 0)
            return;
        if (cart.MiniEnabled)
            cartOpen = true;
        else
            await module!.InvokeVoidAsync("navigate", Initial.CartUrl);
    }

    private async Task UpdateQuantity(QuantityRequest request)
    {
        await Run(async () =>
        {
            cart = await module!.InvokeAsync<NativeCart>("updateNativeCart", cart, request.LineId, request.Quantity);
            _count = cart.Count;
        });
    }

    private void ShowLogin()
    {
        loginOpen = true;
        loginMessage = "";
    }

    private async Task SubmitLogin()
    {
        await Run(async () =>
        {
            var result = otpSent ? await Api<OtpResult>("otp/verify", "POST", new VerifyRequest(phone, code)) : await Api<OtpResult>("otp/send", "POST", new PhoneRequest(phone));
            loginMessage = result.Message;
            testCode = result.TestCode ?? "";
            if (!result.Success)
                return;
            if (otpSent)
            {
                if (await module!.InvokeAsync<bool>("reloadAfterSignIn", !Initial.CartEnabled))
                    return;
                await RefreshCart();

                loginOpen = false;
                otpSent = false;
                code = "";
            }
            else
                otpSent = true;
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (module != null)
        {
            await module.InvokeVoidAsync("stopListeningForCart", _listener);
            await module.DisposeAsync();
        }

        _reference?.Dispose();
    }
}