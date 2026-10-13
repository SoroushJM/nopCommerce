using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Web;
using Storefront.UI.Shared.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.RegisterForJavaScript<NativeSelectBridge>("storefront-native-select");
builder.RootComponents.RegisterForJavaScript<NativeSelectHost>("storefront-native-select-host");
builder.Services.AddBlazorBlueprintComponents(localizer =>
{
    localizer.Set("Sheet.Close", "بستن");
    localizer.Set("Dialog.Close", "بستن");
});
await builder.Build().RunAsync();
