using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using Nop.Core.Infrastructure;
using Nop.Web.Framework.Mvc.Routing;
using Scalar.AspNetCore;

namespace Nop.Plugin.Misc.PersianStorefront;
public sealed class StorefrontStartup : INopStartup
{
 public int Order=>250;
 public void ConfigureServices(IServiceCollection services,IConfiguration configuration){services.AddServerSideBlazor();services.AddBlazorBlueprintComponents(localizer=>{localizer.Set("Sheet.Close","بستن");localizer.Set("Dialog.Close","بستن");});services.AddScoped<StorefrontService>();services.AddScoped<DevelopmentCatalogSeeder>();services.AddSingleton<MockOtpStore>();services.AddSwaggerGen(o=>{o.SwaggerDoc("v1",new OpenApiInfo{Title="Persian storefront API",Version="v1"});o.DocInclusionPredicate((_,description)=>description.GroupName=="stationery");});}
 public void Configure(IApplicationBuilder app){var env=app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();foreach(var name in new[]{"Storefront.UI","BlazorBlueprint.Components","BlazorBlueprint.Primitives"}){var path=Path.Combine(env.ContentRootPath,"Plugins","Misc.PersianStorefront","Content",name);if(Directory.Exists(path))app.UseStaticFiles(new StaticFileOptions{FileProvider=new PhysicalFileProvider(path),RequestPath="/_content/"+name});}var frameworkPath=Path.Combine(env.ContentRootPath,"Plugins","Misc.PersianStorefront","Content","Framework");if(Directory.Exists(frameworkPath))app.UseStaticFiles(new StaticFileOptions{FileProvider=new PhysicalFileProvider(frameworkPath),RequestPath="/_framework"});if(env.IsDevelopment())app.UseSwaggerUI(o=>{o.RoutePrefix="stationery/swagger";o.SwaggerEndpoint("/stationery/openapi/v1.json","Stationery v1");});}
}
public sealed class StorefrontRoutes:IRouteProvider
{
 public int Priority=>1000;
 public void RegisterRoutes(IEndpointRouteBuilder endpoints){endpoints.MapBlazorHub();endpoints.MapControllerRoute("StationeryCartAlias","cart",new{controller="PersianStorefront",action="Index",page="cart"});endpoints.MapControllerRoute("StationeryLogin","stationery/login",new{controller="PersianStorefront",action="Index",page="login"});endpoints.MapControllerRoute("StationerySearch","search",new{controller="PersianStorefront",action="Index",page="catalog"});endpoints.MapControllerRoute("StationeryHome","",new{controller="PersianStorefront",action="Index",page="home"});endpoints.MapControllerRoute("StationeryCatalog","stationery/catalog",new{controller="PersianStorefront",action="Index",page="catalog"});endpoints.MapControllerRoute("StationeryCart","stationery/cart",new{controller="PersianStorefront",action="Index",page="cart"});endpoints.MapControllerRoute("StationeryProduct","stationery/product/{id:int}",new{controller="PersianStorefront",action="Index",page="product"});var env=endpoints.ServiceProvider.GetRequiredService<IWebHostEnvironment>();if(env.IsDevelopment()){endpoints.MapSwagger("/stationery/openapi/{documentName}.json");endpoints.MapScalarApiReference("/stationery/scalar",o=>o.WithOpenApiRoutePattern("/stationery/openapi/{documentName}.json").WithTitle("Madadrang Storefront API"));}}
}
