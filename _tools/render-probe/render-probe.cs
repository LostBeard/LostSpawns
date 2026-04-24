// render-probe.cs - Standalone C# single-file script that drives Chromium (via
// Microsoft.Playwright) to navigate to a dev-server page, capture console output
// + a full-page PNG screenshot, and save them to disk. Built so an agent (Data)
// can visually inspect rendered engine output without the Captain screenshot-
// round-trip.
//
// Usage:
//   dotnet run _tools/render-probe/render-probe.cs -- <url> [output-name-prefix] [wait-seconds]
//
// Examples:
//   dotnet run _tools/render-probe/render-probe.cs
//   dotnet run _tools/render-probe/render-probe.cs -- http://localhost:5019/game
//   dotnet run _tools/render-probe/render-probe.cs -- http://localhost:5019/vr vr-probe 8
//
// Output:
//   _tools/render-probe/output/render-{prefix}-{stamp}.png
//   _tools/render-probe/output/console-{prefix}-{stamp}.log
//
// Requires .NET 10 for single-file dotnet-run. Uses the system Chrome via
// Playwright Channel = "chrome" so no separate browser download needed.

#:package Microsoft.Playwright@1.55.*
#:property JsonSerializerIsReflectionEnabledByDefault=true

using Microsoft.Playwright;

// .NET 10 file-based programs disable reflection-based JSON by default.
// Playwright uses System.Text.Json reflection internally, so re-enable at
// runtime as a belt-and-suspenders alongside the MSBuild property above.
AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);

// --- Arg parsing ---
string url      = args.ElementAtOrDefault(0) ?? "http://localhost:5019/game";
string prefix   = args.ElementAtOrDefault(1) ?? "game";
int waitSec     = int.TryParse(args.ElementAtOrDefault(2), out var w) ? w : 8;

// Optional scripted key presses during the wait window.
// Format: "Ns:Key,Ns:Key,..." e.g. "25:Escape,40:Escape" - at t+25s press Escape, then again at t+40s.
// Each press captures an extra screenshot "keyevent-{prefix}-{stamp}-{i}.png" right after.
string keyScript = args.ElementAtOrDefault(3) ?? "";

// AppContext.BaseDirectory works in both single-file apps and normal builds;
// Assembly.Location returns "" in the single-file / file-based scenario.
string scriptDir = AppContext.BaseDirectory;
// Output sits next to the script regardless of where dotnet run was invoked.
string outputDir = Path.Combine(scriptDir.Contains("_tools") ? scriptDir : "D:/users/tj/Projects/Lost/Lost/_tools/render-probe", "output");
Directory.CreateDirectory(outputDir);

string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
string pngPath = Path.Combine(outputDir, $"render-{prefix}-{stamp}.png");
string logPath = Path.Combine(outputDir, $"console-{prefix}-{stamp}.log");

Console.WriteLine($"[probe] url      = {url}");
Console.WriteLine($"[probe] outputs  = {outputDir}");
Console.WriteLine($"[probe] wait     = {waitSec} seconds");
Console.WriteLine($"[probe] stamp    = {stamp}");

using var playwright = await Playwright.CreateAsync();

// Use system Chrome (Channel="chrome") so we don't require Playwright's own
// Chromium download. WebGPU flags match what Chrome ships with by default on
// Win 11 desktop but we include the unsafe flag defensively for Canary/dev builds.
IBrowser browser;
try
{
    browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
    {
        Channel = "chrome",
        Headless = false,
        Args = new[]
        {
            "--enable-unsafe-webgpu",
            "--enable-features=Vulkan",
            "--disable-blink-features=AutomationControlled",
        },
    });
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[probe] FAILED to launch Chrome via Channel='chrome': {ex.Message}");
    Console.Error.WriteLine($"[probe] Fix: make sure Google Chrome is installed, OR edit this script to use");
    Console.Error.WriteLine($"[probe] Playwright's bundled Chromium (drop the Channel line; may require `pwsh bin/Debug/net10.0/playwright.ps1 install chromium` first).");
    return 1;
}

var context = await browser.NewContextAsync(new BrowserNewContextOptions
{
    ViewportSize = new ViewportSize { Width = 1600, Height = 900 },
    IgnoreHTTPSErrors = true,
});
var page = await context.NewPageAsync();

var consoleLines = new List<string>();
var startClock = System.Diagnostics.Stopwatch.StartNew();
page.Console += (_, msg) =>
{
    double t = startClock.Elapsed.TotalSeconds;
    string line = $"[t+{t,6:F2}s] [{msg.Type}] {msg.Text}";
    consoleLines.Add(line);
    // Echo important messages live.
    if (msg.Type is "error" or "warning")
        Console.WriteLine($"[probe] CONSOLE {line}");
};
page.PageError += (_, err) =>
{
    double t = startClock.Elapsed.TotalSeconds;
    string line = $"[t+{t,6:F2}s] [pageerror] {err}";
    consoleLines.Add(line);
    Console.WriteLine($"[probe] {line}");
};

Console.WriteLine($"[probe] navigating...");
try
{
    await page.GotoAsync(url, new PageGotoOptions
    {
        WaitUntil = WaitUntilState.NetworkIdle,
        Timeout = 30000,
    });
}
catch (TimeoutException)
{
    Console.WriteLine($"[probe] networkidle timeout; continuing anyway (long-running frame loops prevent networkidle on WebGPU demos).");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[probe] navigation failed: {ex.Message}");
    await browser.CloseAsync();
    return 2;
}

// Parse scripted key events into sorted list of (elapsedSec, key).
var keyEvents = new List<(int atSec, string key)>();
if (!string.IsNullOrWhiteSpace(keyScript))
{
    foreach (var part in keyScript.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var kv = part.Split(':', 2, StringSplitOptions.TrimEntries);
        if (kv.Length == 2 && int.TryParse(kv[0], out var sec))
            keyEvents.Add((sec, kv[1]));
    }
    keyEvents.Sort((a, b) => a.atSec.CompareTo(b.atSec));
    foreach (var (at, k) in keyEvents)
        Console.WriteLine($"[probe] scripted key: t+{at}s press {k}");
}

Console.WriteLine($"[probe] waiting {waitSec}s for render to stabilize (taking progress shots every 10s)...");
int progressShot = 0;
int keyShot = 0;
int elapsed = 0;
int nextKeyIdx = 0;
while (elapsed < waitSec)
{
    // Smallest of: next progress shot boundary, next scripted key time, or end.
    int nextTick = Math.Min(waitSec, ((elapsed / 10) + 1) * 10);
    int nextEvent = nextKeyIdx < keyEvents.Count ? keyEvents[nextKeyIdx].atSec : int.MaxValue;
    int target = Math.Min(nextTick, Math.Min(nextEvent, waitSec));
    int sleep = Math.Max(0, target - elapsed);
    if (sleep > 0) await Task.Delay(sleep * 1000);
    elapsed = target;

    // Fire any scripted keys that match this moment.
    while (nextKeyIdx < keyEvents.Count && keyEvents[nextKeyIdx].atSec <= elapsed)
    {
        var (_, key) = keyEvents[nextKeyIdx];
        try
        {
            await page.Keyboard.PressAsync(key);
            await Task.Delay(500); // let the UI react
            keyShot++;
            string keyPath = Path.Combine(outputDir, $"keyevent-{prefix}-{stamp}-{keyShot:D2}.png");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = keyPath, FullPage = false });
            Console.WriteLine($"[probe]   +{elapsed}s: pressed {key} -> {Path.GetFileName(keyPath)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[probe]   +{elapsed}s: key press {key} failed ({ex.Message})");
        }
        nextKeyIdx++;
    }

    // Progress shot on every 10s boundary (and at waitSec end).
    if (elapsed % 10 == 0 || elapsed == waitSec)
    {
        progressShot++;
        string progPath = Path.Combine(outputDir, $"progress-{prefix}-{stamp}-{progressShot:D2}.png");
        try
        {
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = progPath, FullPage = false });
            Console.WriteLine($"[probe]   +{elapsed}s: {Path.GetFileName(progPath)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[probe]   +{elapsed}s: progress shot failed ({ex.Message})");
        }
    }
}

Console.WriteLine($"[probe] capturing DOM snapshot (canvases, body state)...");
try
{
    // Evaluates a small JS introspection and returns a JSON-serializable object.
    var dom = await page.EvaluateAsync<System.Text.Json.JsonElement>(@"() => {
        const canvases = Array.from(document.querySelectorAll('canvas')).map((c, i) => ({
            index: i,
            id: c.id || null,
            className: c.className || null,
            clientW: c.clientWidth,
            clientH: c.clientHeight,
            canvasW: c.width,
            canvasH: c.height,
            offsetW: c.offsetWidth,
            offsetH: c.offsetHeight,
            hidden: c.hidden,
            style: { display: getComputedStyle(c).display, visibility: getComputedStyle(c).visibility, opacity: getComputedStyle(c).opacity, zIndex: getComputedStyle(c).zIndex }
        }));
        const bodyBg = getComputedStyle(document.body).backgroundColor;
        const appDiv = document.getElementById('app');
        const errUi = document.getElementById('blazor-error-ui');
        return {
            title: document.title,
            readyState: document.readyState,
            bodyBg: bodyBg,
            appDivInnerHtmlLen: appDiv ? appDiv.innerHTML.length : -1,
            errUiDisplay: errUi ? getComputedStyle(errUi).display : '(none)',
            canvasCount: canvases.length,
            canvases: canvases,
            loadingSpinnerStillPresent: !!document.querySelector('.loading-progress'),
        };
    }");
    Console.WriteLine($"[probe] DOM: {dom}");
    await File.WriteAllTextAsync(Path.Combine(outputDir, $"dom-{prefix}-{stamp}.json"), dom.ToString());
}
catch (Exception ex)
{
    Console.WriteLine($"[probe] DOM snapshot failed (non-fatal): {ex.Message}");
}

Console.WriteLine($"[probe] capturing screenshot...");
await page.ScreenshotAsync(new PageScreenshotOptions { Path = pngPath, FullPage = true });

await File.WriteAllLinesAsync(logPath, consoleLines);

Console.WriteLine($"\n[probe] DONE");
Console.WriteLine($"[probe]   screenshot: {pngPath}");
Console.WriteLine($"[probe]   console   : {logPath}  ({consoleLines.Count} lines)");

// Print a short summary of error-level console lines for quick triage.
var errors = consoleLines.Where(l => l.StartsWith("[error") || l.StartsWith("[pageerror")).ToList();
if (errors.Any())
{
    Console.WriteLine($"\n[probe] {errors.Count} error/pageerror console line(s):");
    foreach (var e in errors.Take(10))
        Console.WriteLine($"        {e}");
}

await browser.CloseAsync();
return 0;
