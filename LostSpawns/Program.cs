using LostSpawns;
using LostSpawns.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.Cryptography;
using SpawnDev.GameUI;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Initialize SpawnJS runtime (required before any JS interop)
builder.Services.AddSpawnJSRuntime(out var JS);
// Slot lifetime is manual in SpawnJS; watcher names leaks from owned wrappers/callbacks.
SpawnJSRuntime.EnableIDisposableWatcher = false;

// Cross-platform crypto (Ed25519, SHA, etc) - browser uses BrowserWASMCrypto
builder.Services.AddPlatformCrypto();

// Core game services
builder.Services.AddSingleton<IdentityService>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<InputService>();
builder.Services.AddSingleton<VoxelEngineService>();
builder.Services.AddSingleton<WorldService>();
builder.Services.AddSingleton<RenderService>();
// PlayerStats first because InventoryService now constructor-injects it for consumables.
builder.Services.AddSingleton<PlayerStatsService>();
builder.Services.AddSingleton<InventoryService>();
builder.Services.AddSingleton<CraftingService>();
builder.Services.AddSingleton<WorldTimeService>();
builder.Services.AddSingleton<WeatherService>();
builder.Services.AddSingleton<EntityService>();
builder.Services.AddSingleton<CampfireService>();
builder.Services.AddSingleton<GroundItemService>();
builder.Services.AddSingleton<AudioService>();
builder.Services.AddSingleton<GltfMeshService>();
builder.Services.AddSingleton<SaveService>();
builder.Services.AddSingleton<HudService>();

// GPU-rendered game UI overlay (SDF fonts, HUD elements, inventory, chat)
builder.Services.AddGameUI(UITheme.LostSpawns);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

if (JS.IsWindow)
{
    builder.RootComponents.Add<App>("#app");
    builder.RootComponents.Add<HeadOutlet>("head::after");
}

// SpawnJSRunAsync replaces RunAsync - handles SpawnJS initialization lifecycle
await builder.Build().SpawnJSRunAsync();
