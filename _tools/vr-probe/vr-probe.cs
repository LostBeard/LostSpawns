// vr-probe.cs - loads /vr against a MOCK WebXR device, presses Enter VR, lets the world stream in and saves what a headset would be given
// (both eyes, from the page's #xr-mirror canvas) plus the console log.
//
//   dotnet run _tools/vr-probe/vr-probe.cs -- [url] [name] [wait-seconds]
//
// Defaults: http://127.0.0.1:5057/vr  vr  25.   Output next to the script in output/. The mock is mock-xr.js (from AubsCraft's viewer tests):
// it checks what the page draws and presents, not comfort, real eye resolution or Quest performance.

#:package Microsoft.Playwright@1.55.*
#:property JsonSerializerIsReflectionEnabledByDefault=true

using Microsoft.Playwright;

AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);

string url = args.ElementAtOrDefault(0) ?? "http://127.0.0.1:5057/vr";
string name = args.ElementAtOrDefault(1) ?? "vr";
int waitSec = int.TryParse(args.ElementAtOrDefault(2), out var w) ? w : 25;

string scriptDir = Path.GetDirectoryName(Environment.GetCommandLineArgs().Length > 0 ? Path.GetFullPath("_tools/vr-probe/vr-probe.cs") : ".")!;
string outDir = Path.Combine(scriptDir, "output");
Directory.CreateDirectory(outDir);
string mock = await File.ReadAllTextAsync(Path.Combine(scriptDir, "mock-xr.js"));
string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Channel = "chrome",
    Headless = false,
    Args = new[] { "--enable-unsafe-webgpu", "--enable-features=Vulkan" },
});
var page = await browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 1280, Height = 800 } });
var log = new List<string>();
page.Console += (_, m) => log.Add($"[{m.Type}] {m.Text}");
page.PageError += (_, e) => log.Add($"[pageerror] {e}");
await page.AddInitScriptAsync(mock);
try
{
    await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 120_000 });
    var enter = page.GetByRole(AriaRole.Button, new() { Name = "Enter VR" });
    await enter.WaitForAsync(new() { Timeout = 90_000 });
    await enter.ClickAsync();
    await Task.Delay(waitSec * 1000);
    string shot = Path.Combine(outDir, $"{name}-{stamp}.png");
    await page.ScreenshotAsync(new PageScreenshotOptions { Path = shot, Clip = new Clip { X = 0, Y = 0, Width = 1024, Height = 512 } });
    Console.WriteLine($"[probe] screenshot: {shot}");
    // The page shows its own XR state and any error as text
    log.Add("[page] " + (await page.InnerTextAsync("body")).ReplaceLineEndings(" | "));
    var frames = await page.EvaluateAsync<int>("window.__xr ? window.__xr.frames : -1");
    Console.WriteLine($"[probe] frames: {frames}");
}
finally
{
    await File.WriteAllLinesAsync(Path.Combine(outDir, $"{name}-{stamp}.log"), log);
    Console.WriteLine($"[probe] log lines: {log.Count}");
    foreach (var l in log.Where(l => l.StartsWith("[error]") || l.StartsWith("[pageerror]")).Take(10)) Console.WriteLine("[probe] " + l);
    await browser.CloseAsync();
}
