// vr-probe.cs - loads the home page against a MOCK WebXR device, presses its VR button, lets the world stream in and saves what a headset would be given
// (both eyes, from the page's #xr-mirror canvas) plus the console log. It then pushes the left stick forward (walk) and the right stick sideways
// (turn) with the mock's window.__xr hooks, saves a picture of each, leaves the session and saves the desktop view that takes over.
//
//   dotnet run _tools/vr-probe/vr-probe.cs -- [url] [name] [wait-seconds]
//
// Defaults: http://127.0.0.1:5057/ (the home page, whose VR button starts the session)  vr  30.   Output next to the script in output/. The mock is mock-xr.js (from AubsCraft's viewer
// tests): it checks what the page draws and presents, not comfort, real eye resolution or Quest performance.

#:package Microsoft.Playwright@1.55.*
#:property JsonSerializerIsReflectionEnabledByDefault=true

using Microsoft.Playwright;

AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);

string url = args.ElementAtOrDefault(0) ?? "http://127.0.0.1:5057/";
string name = args.ElementAtOrDefault(1) ?? "vr";
int waitSec = int.TryParse(args.ElementAtOrDefault(2), out var w) ? w : 30;

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
    // "pause": look at the game's pause menu (it offers Enter VR when the mock headset is there) instead of entering
    // "pausevr": open the pause menu, press Enter VR there, then swing at the ground: does play work after switching in from the pause menu?
    if ((args.ElementAtOrDefault(3) ?? "") == "pausevr")
    {
        await Task.Delay(waitSec * 1000);
        await page.Locator("#gameCanvas").PressAsync("Escape");
        await Task.Delay(1500);
        await page.Mouse.ClickAsync(640, 418);
        await Task.Delay(6000);
        await page.EvaluateAsync("window.__xr.aimAt(0.3, 0.2, -2.5)");
        await Task.Delay(500);
        await page.EvaluateAsync("window.__xr.trigger = true");
        await Task.Delay(200);
        await page.EvaluateAsync("window.__xr.trigger = false");
        await Task.Delay(600);
        string vr = Path.Combine(outDir, $"{name}-{stamp}-pausevr.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = vr, Clip = new Clip { X = 0, Y = 0, Width = 1024, Height = 512 } });
        Console.WriteLine($"[probe] screenshot pausevr: {vr}");
        return;
    }
    if ((args.ElementAtOrDefault(3) ?? "") == "pause")
    {
        await Task.Delay(waitSec * 1000);
        await page.Locator("#gameCanvas").PressAsync("Escape");
        await Task.Delay(1500);
        string menu = Path.Combine(outDir, $"{name}-{stamp}-pause.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = menu });
        Console.WriteLine($"[probe] screenshot pause: {menu}");
        return;
    }
    // The home page's VR button starts the session, then the game page loads the world behind it
    var enter = page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("IN VR") });
    await enter.WaitForAsync(new() { Timeout = 90_000 });
    await enter.ClickAsync();
    await Task.Delay(waitSec * 1000);
    async Task Shot(string label)
    {
        string path = Path.Combine(outDir, $"{name}-{stamp}-{label}.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, Clip = new Clip { X = 0, Y = 0, Width = 1024, Height = 512 } });
        Console.WriteLine($"[probe] screenshot {label}: {path}");
    }
    await Shot("1-start");
    // "play": the gameplay layer: wrist numbers, the X menu, pointing at it, a swing with the right trigger
    // "soak": walk for a few minutes swinging now and then, to see what a long session logs (errors, frame time, loading)
    if ((args.ElementAtOrDefault(3) ?? "") == "soak")
    {
        int minutes = int.TryParse(args.ElementAtOrDefault(4), out var mm) ? mm : 3;
        await page.EvaluateAsync("window.__xr.left = [0, -1]");
        for (int i = 0; i < minutes * 60 / 4; i++)
        {
            await page.EvaluateAsync("window.__xr.right = [" + (i % 7 < 2 ? "0.8" : "0") + ", 0]; window.__xr.trigger = true");
            await Task.Delay(300);
            await page.EvaluateAsync("window.__xr.trigger = false");
            await Task.Delay(3700);
            if (i % 15 == 14) await Shot($"soak-{i / 15}");
        }
        await page.EvaluateAsync("window.__xr.left = [0, 0]; window.__xr.right = [0, 0]");
        await Shot("soak-end");
        return;
    }
    // "swing": wave the right hand fast (no trigger) next to an animal that stands still ahead (url: ...&spawn=wolf&still=1&spawndist=1): does a real swing strike it?
    if ((args.ElementAtOrDefault(3) ?? "") == "swing")
    {
        await page.EvaluateAsync("window.__xr.rayRight.p = [0, 0.6, -0.3]; window.__swing = setInterval(() => { window.__xr.rayRight.p[1] = 0.5 + 0.3 * (Math.floor(performance.now() / 16) % 2); }, 16)");
        await Task.Delay(1000);
        await Shot("2-swinging");
        await Task.Delay(4000);
        await Shot("3-later");
        await page.EvaluateAsync("clearInterval(window.__swing)");
        return;
    }
    // "dig": hold the right trigger at the ground ahead for a few seconds: do the holes show?
    if ((args.ElementAtOrDefault(3) ?? "") == "dig")
    {
        await page.EvaluateAsync("window.__xr.aimAt(0.0, 0.0, -1.6)");
        await Task.Delay(400);
        await page.EvaluateAsync("window.__xr.trigger = true");
        await Task.Delay(4500);
        await page.EvaluateAsync("window.__xr.trigger = false");
        await Task.Delay(1500);
        await Shot("2-dug");
        return;
    }
    // "walk": push the left stick forward for a few seconds, a picture every 1.5 s (with ?poi=1 the player starts at a building: does the door let it in, the wall stop it?)
    if ((args.ElementAtOrDefault(3) ?? "") == "walk")
    {
        await page.EvaluateAsync("window.__xr.left = [0, -1]");
        for (int i = 0; i < 4; i++) { await Task.Delay(1500); await Shot($"walk-{i}"); }
        await page.EvaluateAsync("window.__xr.left = [0, 0]");
        return;
    }
    // "settings": open the menu and its Settings page, switch the vignette on, walk and turn to see it
    if ((args.ElementAtOrDefault(3) ?? "") == "settings")
    {
        await page.EvaluateAsync("window.__xr.x = true");
        await Task.Delay(300);
        await page.EvaluateAsync("window.__xr.x = false");
        await Task.Delay(500);
        // Settings button: panel px (212, 499) of an 800 x 620 panel at 0.0011 m a px, 0.75 m ahead, centre at head height - 0.12
        await page.EvaluateAsync("window.__xr.aimAt((212 - 400) * 0.0011, 1.58 + (310 - 499) * 0.0011, -0.75)");
        await Task.Delay(600);
        await page.EvaluateAsync("window.__xr.trigger = true");
        await Task.Delay(300);
        await page.EvaluateAsync("window.__xr.trigger = false");
        await Task.Delay(600);
        await Shot("2-settings");
        // Comfort vignette row: panel px (254, 282) of the 800 x 470 settings panel
        await page.EvaluateAsync("window.__xr.aimAt((254 - 400) * 0.0011, 1.58 + (235 - 282) * 0.0011, -0.75)");
        await Task.Delay(600);
        await page.EvaluateAsync("window.__xr.trigger = true");
        await Task.Delay(300);
        await page.EvaluateAsync("window.__xr.trigger = false");
        await Task.Delay(500);
        await Shot("3-vignette-on");
        await page.EvaluateAsync("window.__xr.x = true");
        await Task.Delay(300);
        await page.EvaluateAsync("window.__xr.x = false; window.__xr.left = [0, -1]");
        await Task.Delay(1500);
        await Shot("4-walking");
        await page.EvaluateAsync("window.__xr.left = [0, 0]");
        return;
    }
    // "help": open the menu and its Controls page
    if ((args.ElementAtOrDefault(3) ?? "") == "help")
    {
        await page.EvaluateAsync("window.__xr.x = true");
        await Task.Delay(300);
        await page.EvaluateAsync("window.__xr.x = false");
        await Task.Delay(500);
        await page.EvaluateAsync("window.__xr.aimAt(-0.06, 1.391, -0.75)");
        await Task.Delay(600);
        await page.EvaluateAsync("window.__xr.trigger = true");
        await Task.Delay(300);
        await page.EvaluateAsync("window.__xr.trigger = false");
        await Task.Delay(600);
        await Shot("2-help");
        return;
    }
    // "die": hurt the player to death with F9, look at the headset's death menu, point at Respawn and press it
    if ((args.ElementAtOrDefault(3) ?? "") == "die")
    {
        for (int i = 0; i < 12; i++) { await page.Keyboard.PressAsync("F9"); await Task.Delay(120); }
        await Task.Delay(1200);
        await Shot("2-dead");
        await page.EvaluateAsync("window.__xr.aimAt(-0.196, 1.365, -0.75)");
        await Task.Delay(700);
        await Shot("3-at-respawn");
        await page.EvaluateAsync("window.__xr.trigger = true");
        await Task.Delay(300);
        await page.EvaluateAsync("window.__xr.trigger = false");
        await Task.Delay(2500);
        await Shot("4-respawned");
        return;
    }
    if ((args.ElementAtOrDefault(3) ?? "") == "play")
    {
        async Task Press(string flag, int ms = 350) { await page.EvaluateAsync($"window.__xr.{flag} = true"); await Task.Delay(ms); await page.EvaluateAsync($"window.__xr.{flag} = false"); await Task.Delay(250); }
        await Press("x");
        await Task.Delay(500);
        await Shot("2-menu");
        // the menu is 0.75 m ahead of the head, a little low; its hotbar buttons are on the left
        await page.EvaluateAsync("window.__xr.aimAt(-0.188, 1.449, -0.75)");
        await Task.Delay(700);
        await Shot("3-pointing");
        await Press("trigger");
        await Task.Delay(500);
        await Shot("3b-backpack");
        await Press("x");
        // menu shut: aim at the ground ahead and swing
        await page.EvaluateAsync("window.__xr.aimAt(0.3, 0.2, -2.5)");
        await Task.Delay(500);
        await page.EvaluateAsync("window.__xr.trigger = true");
        await Task.Delay(180);
        await Shot("4-swing");
        await page.EvaluateAsync("window.__xr.trigger = false");
        await Task.Delay(1500);
        await Shot("5-after");
        return;
    }
    // Walk: the left stick forward (pushed forward reads -1 on y)
    await page.EvaluateAsync("window.__xr.left = [0, -1]");
    await Task.Delay(5000);
    await page.EvaluateAsync("window.__xr.left = [0, 0]");
    await Shot("2-walked");
    // Turn: the right stick sideways
    await page.EvaluateAsync("window.__xr.right = [1, 0]");
    await Task.Delay(2500);
    await page.EvaluateAsync("window.__xr.right = [0, 0]");
    await Shot("3-turned");
    // Leave the session as the headset does and look at the desktop view
    await page.EvaluateAsync("document.getElementById('xr-mirror').style.display = 'none'; window.__xr.leave()");
    await Task.Delay(6000);
    string desktop = Path.Combine(outDir, $"{name}-{stamp}-4-desktop.png");
    await page.ScreenshotAsync(new PageScreenshotOptions { Path = desktop });
    Console.WriteLine($"[probe] screenshot 4-desktop: {desktop}");
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
