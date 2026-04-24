using LostSpawns;
using LostSpawns.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SpawnDev.BlazorJS;
using SpawnDev.GameUI;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Initialize BlazorJS runtime (required before any JS interop)
builder.Services.AddBlazorJSRuntime(out var JS);

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

// BlazorJSRunAsync replaces RunAsync — handles BlazorJS initialization lifecycle
await builder.Build().BlazorJSRunAsync();
