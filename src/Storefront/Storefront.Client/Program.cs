using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddBlazorBlueprintComponents(localizer =>
{
    localizer.Set("Sheet.Close", "بستن");
    localizer.Set("Dialog.Close", "بستن");
});
await builder.Build().RunAsync();
