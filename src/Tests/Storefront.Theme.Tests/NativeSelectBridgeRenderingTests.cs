using System.Net;
using System.Text.Json;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NUnit.Framework;
using Storefront.UI.Shared.Components;

namespace Storefront.Theme.Tests;

[TestFixture]
public sealed class NativeSelectBridgeRenderingTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task NativeLabelsAndSelectedTextRenderBeforeOpeningThePopup(bool multiple)
    {
        await using var provider = CreateServices();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var state = State(multiple, "native-a", "Native selected label");
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<NativeSelectBridge>(Parameters(state));
            return WebUtility.HtmlDecode(result.ToHtmlString());
        });

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("aria-label=\"Native field label\""));
            Assert.That(html, Does.Contain("aria-describedby=\"native-validation\""));
            Assert.That(html, Does.Contain("Native selected label"));
            Assert.That(html, Does.Contain("data-bb-direction=\"rtl\""));
            Assert.That(html, Does.Not.Contain("_state."));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReplacingOptionsUpdatesSelectionAndLabelAtTheSameNativeIndex(bool multiple)
    {
        await using var provider = CreateServices();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        BridgeHarness? harness = null;
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<BridgeHarness>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(BridgeHarness.Initial)] = State(multiple, "native-a", "Original province"),
                [nameof(BridgeHarness.Capture)] = (Action<BridgeHarness>)(component => harness = component)
            }));
            Assert.That(WebUtility.HtmlDecode(result.ToHtmlString()), Does.Contain("Original province"));
            await harness!.Update(State(multiple, "native-b", "Replacement province"));
            var html = WebUtility.HtmlDecode(result.ToHtmlString());
            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Contain("Replacement province"));
                Assert.That(html, Does.Not.Contain("Original province"));
            });
        });
    }

    [Test]
    public async Task MultipleBridgesUseOneSharedPortalHost()
    {
        await using var provider = CreateServices();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var portals = provider.GetRequiredService<IPortalService>();
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var first = await renderer.RenderComponentAsync<NativeSelectBridge>(Parameters(State(false, "a", "First")));
            var second = await renderer.RenderComponentAsync<NativeSelectBridge>(Parameters(State(true, "b", "Second")));
            Assert.That(portals.HasHost, Is.False, "A select bridge must not register its own global portal host.");
            const string token = "unique-native-overlay-regression-token";
            portals.RegisterPortal("native-select-regression", builder => builder.AddContent(0, token), PortalCategory.Overlay);
            // HtmlRenderer does not run OnAfterRender. Register the fragment before
            // rendering the host so this checks the actual initial portal output.
            var host = await renderer.RenderComponentAsync<NativeSelectHost>();
            Assert.That(portals.HasHost, Is.True);
            var html = host.ToHtmlString();
            Assert.Multiple(() =>
            {
                Assert.That(html.Split(token).Length - 1, Is.EqualTo(1));
                Assert.That(first.ToHtmlString(), Does.Not.Contain(token));
                Assert.That(second.ToHtmlString(), Does.Not.Contain(token));
            });
            portals.UnregisterPortal("native-select-regression");
        });
    }

    private static NativeSelectBridge.NativeSelectState State(bool multiple, string value, string text) => new()
    {
        Id = "native-field", Label = "Native field label", DescribedBy = "native-validation", Rtl = true,
        Multiple = multiple, Value = value, Values = [value],
        Options = [new() { Index = 0, Value = value, Text = text }]
    };

    private static ParameterView Parameters(NativeSelectBridge.NativeSelectState state) => ParameterView.FromDictionary(new Dictionary<string, object?>
    {
        [nameof(NativeSelectBridge.BridgeKey)] = "native-select-regression",
        [nameof(NativeSelectBridge.StateJson)] = JsonSerializer.Serialize(state)
    });

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorBlueprintComponents();
        services.AddSingleton<IJSRuntime, NoJavaScript>();
        services.AddSingleton<NavigationManager, TestNavigation>();
        return services.BuildServiceProvider();
    }

    public sealed class BridgeHarness : ComponentBase
    {
        [Parameter] public NativeSelectBridge.NativeSelectState Initial { get; set; } = new();
        [Parameter] public Action<BridgeHarness>? Capture { get; set; }
        private NativeSelectBridge.NativeSelectState _state = new();
        protected override void OnInitialized() { _state = Initial; Capture?.Invoke(this); }
        public Task Update(NativeSelectBridge.NativeSelectState state) { _state = state; return InvokeAsync(StateHasChanged); }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<NativeSelectBridge>(0);
            builder.AddAttribute(1, nameof(NativeSelectBridge.BridgeKey), "native-select-regression");
            builder.AddAttribute(2, nameof(NativeSelectBridge.StateJson), JsonSerializer.Serialize(_state));
            builder.CloseComponent();
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://example.test/shop/", "https://example.test/shop/");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("Static rendering must not invoke JavaScript.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
