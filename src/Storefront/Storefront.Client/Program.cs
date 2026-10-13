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
    localizer.Set("MultiSelect.Remove", "حذف {0}");
    localizer.Set("MultiSelect.Clear", "پاک کردن انتخاب‌ها");
    localizer.Set("MultiSelect.More", "{0} مورد دیگر");
});
await builder.Build().RunAsync();
