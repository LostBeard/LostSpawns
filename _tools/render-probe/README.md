# render-probe

Standalone agent tool. Lets Data (or any crew member) visually inspect what a
dev-server page renders, and read its browser console output, without
round-tripping a screenshot through Captain.

The probe drives system Chrome via Playwright for .NET, navigates to a URL,
waits for the page to settle, captures a full-page PNG + the browser console
log, and writes them both to `output/` next to the script.

## Why this exists

Captain (TJ) ran a Quest session 2026-04-23 where he was describing renders by
hand ("green on tops and bottoms of cubes, no sides") and pasting screenshots
into chat. Pivot after that session:

> "we need a usable path for you to unit test rendered engine output so you
> can iterate faster"
> "me testing and telling you what I see and pasting screenshots is slow as
> hell. I am slowing YOU down"
> "make sure to document the debugging process you are creating in the
> solution folder so we remember"

This probe is that path. Data runs it, Reads the PNG with the Read tool
(multimodal Read renders PNGs in-context), and iterates directly on the
rendering pipeline.

## Requirements

- .NET 10 SDK (for single-file `dotnet run` of a `.cs` script)
- Google Chrome installed on the machine (used via `Channel = "chrome"` so
  Playwright's bundled Chromium download is NOT required)
- The target dev server running (default assumes `http://localhost:5019/game`)

## Usage

From the Lost solution root:

```bash
# Default: http://localhost:5019/game, prefix "game", 8s settle wait
dotnet run _tools/render-probe/render-probe.cs

# Custom URL, prefix, settle time
dotnet run _tools/render-probe/render-probe.cs -- http://localhost:5019/vr vr-probe 10
```

Arg order: `<url> [prefix] [wait-seconds]`.

### First run

First invocation restores `Microsoft.Playwright@1.55.*` and resolves all
transitive packages. Expect ~30-60s on the very first run; subsequent runs
start in a few seconds.

If this is the first Playwright install on this machine AND you choose to drop
the `Channel = "chrome"` line (see Fallbacks below), you also need to run the
Playwright browser download script:

```bash
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

With `Channel = "chrome"` (the default) that download is unnecessary — system
Chrome is used directly.

## Output

Files land in `_tools/render-probe/output/` (created on demand, gitignored):

- `render-{prefix}-{stamp}.png` — full-page screenshot, 1600x900 viewport
- `console-{prefix}-{stamp}.log` — every `page.Console` + `page.PageError`
  line, in order, prefixed by `[type]` tag so errors/warnings are easy to grep

`{stamp}` format: `yyyyMMdd_HHmmss` (local time). Keeps runs chronologically
ordered without overwriting.

On exit the probe also prints the first ten `[error]` / `[pageerror]` lines to
the terminal so you can see failures immediately without opening the log file.

## How Data uses this

Typical iteration loop:

1. Dev server is already running (`dotnet watch` etc.) on localhost:5019.
2. Make a change to the engine / shader / scene.
3. Run the probe (`dotnet run _tools/render-probe/render-probe.cs`).
4. Read the produced PNG from `output/` with the Read tool. The multimodal
   Read surfaces it inline — no need to ask Captain "what do you see?".
5. If the console log flagged errors, grep them; otherwise keep iterating.

This replaces the "Captain hits F5, takes a screenshot, pastes into chat"
round trip.

## Exit codes

- `0` — screenshot + log written successfully
- `1` — Chrome launch failed (usually means Chrome isn't installed, or the
  `chrome` channel can't find it). Error message suggests the fix.
- `2` — navigation to the URL failed for a reason other than networkidle
  timeout (e.g. dev server not running, bad URL).

A networkidle timeout is NOT an error — many engine demos run a continuous
requestAnimationFrame loop, so Chrome never reports networkidle. The probe
logs the timeout and continues.

## Fallbacks

**Chrome not installed:** drop the `Channel = "chrome"` line in
`render-probe.cs` and run `pwsh bin/Debug/net10.0/playwright.ps1 install
chromium` once. Playwright's bundled Chromium is ~150MB but works on any
machine.

**Headless mode:** change `Headless = false` to `Headless = true` if you're
running in a context where a visible browser window is unwanted (e.g. CI).
Default is non-headless so Captain can see the probe running and interrupt
if needed.

**WebGPU flags:** the launch args include `--enable-unsafe-webgpu` and
`--enable-features=Vulkan` defensively for dev/Canary builds. Stable Chrome
on Win 11 doesn't need them but they're harmless.

## Notes

- Viewport is 1600x900 to match a typical desktop dev setup. Change in-script
  if you need a different aspect ratio.
- Screenshots are `FullPage = true`, so a scrolling page captures the whole
  document height, not just the viewport.
- The probe does NOT try to tear down a dev server it didn't start — if your
  server hangs, kill it separately.
- `output/` is `.gitignore`d; these are throwaway artifacts.
