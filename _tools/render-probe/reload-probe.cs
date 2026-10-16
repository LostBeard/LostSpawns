// reload-probe.cs - loads a page, waits, reloads it in the same browser context (same localStorage) and prints the console lines matching a pattern from both loads:
// does the save restore what the first visit saved?
//
//   dotnet run _tools/render-probe/reload-probe.cs -- [url] [wait-seconds] [regex]
#:package Microsoft.Playwright@1.55.*
#:property JsonSerializerIsReflectionEnabledByDefault=true

using Microsoft.Playwright;

AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);
string url = args.ElementAtOrDefault(0) ?? "http://127.0.0.1:5057/game";
int wait = int.TryParse(args.ElementAtOrDefault(1), out var w) ? w : 25;
var pattern = new System.Text.RegularExpressions.Regex(args.ElementAtOrDefault(2) ?? @"\[(Poi|Save|Game)\]");

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "chrome", Headless = false, Args = new[] { "--enable-unsafe-webgpu", "--enable-features=Vulkan" } });
var context = await browser.NewContextAsync(new BrowserNewContextOptions { ViewportSize = new ViewportSize { Width = 1280, Height = 800 } });
var page = await context.NewPageAsync();
string phase = "first";
page.Console += (_, m) => { if (pattern.IsMatch(m.Text)) Console.WriteLine($"[{phase}] {m.Text}"); };
await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 120_000 });
await Task.Delay(wait * 1000);
phase = "second";
await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 120_000 });
await Task.Delay(wait * 1000);
await browser.CloseAsync();
