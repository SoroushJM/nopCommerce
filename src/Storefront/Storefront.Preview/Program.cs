using BlazorBlueprint.Components;
using Storefront.Preview.Components;
using Storefront.UI.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents().AddInteractiveWebAssemblyComponents();
builder.Services.AddBlazorBlueprintComponents(localizer =>
{
    localizer.Set("Sheet.Close", "بستن");
    localizer.Set("Dialog.Close", "بستن");
});
var app = builder.Build();
if (!app.Environment.IsDevelopment())
    throw new InvalidOperationException("The renderer experiment must only run in Development.");
app.UseStaticFiles();
app.MapStaticAssets();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode().AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(RendererProbe).Assembly);
app.Run();