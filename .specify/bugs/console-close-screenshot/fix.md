# Bug Fix: `screenshot` console command captures the console panel itself

- **Slug**: console-close-screenshot
- **Fixed**: 2026-09-02
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

`GodotScreenshotService.Capture()` now closes the dev console before reading back the viewport
texture (force-drawing the hidden state first so the read-back isn't stale) and restores the
console's prior open/closed state afterward, exactly as the assessment's preferred remediation
described.

## Changes

| File | Change | Notes |
|------|--------|-------|
| `src/Game/Autoloads/DevConsole.cs` | modified | Added public `Open()`/`Close()` wrappers around the existing private `SetOpen`, giving the screenshot service a way to hide/restore the panel without exposing it directly. |
| `src/Game/Infrastructure/GodotScreenshotService.cs` | modified | `Capture()` looks up `/root/DevConsole`, closes it + calls `RenderingServer.ForceDraw()` if it was open, captures, then reopens it in a `finally` block. No-op (and never throws) if the autoload node isn't present. |
| `tests/Game.Tests/ScreenshotConsoleTest.cs` | added | Godot-tier test: opens the console, captures via a real `GodotScreenshotService` to a scratch directory, and asserts (a) the console is restored to open afterward and (b) the saved PNG's panel-region pixel is not console-dark, proving the hide actually took effect before the frame was read back. A second test confirms an already-closed console stays closed after capture. |
| `tests/Game.Tests/ScreenshotConsoleTest.cs.uid` | added | Sidecar minted via `godot --headless --import`, per project convention for new `.cs` files. |

## Diff Highlights

```csharp
// GodotScreenshotService.Capture()
var console = root.GetNodeOrNull<DevConsole>("DevConsole");
var reopenConsoleAfterCapture = console is not null && console.IsOpen;

if (reopenConsoleAfterCapture)
{
    console!.Close();
    RenderingServer.ForceDraw();
}

try
{
    var image = root.GetTexture()?.GetImage();
    // ... unchanged capture/save logic ...
}
finally
{
    if (reopenConsoleAfterCapture)
    {
        console!.Open();
    }
}
```

```csharp
// DevConsole.cs
public void Open() => SetOpen(true);
public void Close() => SetOpen(false);
```

## Tests Added or Updated

- `tests/Game.Tests/ScreenshotConsoleTest.cs::CaptureHidesTheOpenConsoleAndRestoresItAfterward` —
  pins down that the console panel is not present in the captured PNG when the console was open at
  capture time, and that the console is left open afterward (no closing side effect).
- `tests/Game.Tests/ScreenshotConsoleTest.cs::CaptureLeavesAnAlreadyClosedConsoleClosed` — pins
  down the no-op path when the console was never open.

## Local Verification

- `dotnet build NewGame1.sln` → succeeded, no new warnings.
- `godot --headless --import` → minted `ScreenshotConsoleTest.cs.uid`; `git diff project.godot`
  confirmed empty (renderer pin untouched).
- `dotnet test tests/Core.Tests/NewGame1.Core.Tests.csproj` → 61 passed, 0 failed (existing
  `ScreenshotCommand`/`IScreenshotService` fake-based tests unaffected, as expected since the fix
  lives entirely in `GodotScreenshotService`).
- `xvfb-run -a godot --rendering-method forward_plus --rendering-driver vulkan --audio-driver
  Dummy res://scenes/Main.tscn -- --run-tests --quit-on-finish` → 11 passed, 0 failed, including
  the two new tests.
- `xvfb-run -a godot ... -- --screenshot repro` (CLI harness, console never opened) → still writes
  a valid PNG, confirming the added console-hide logic is a no-op on that path (timing/`frameDelay`
  unperturbed).
- `scripts/verify.sh` (full gate: build, style, Core tests, Godot tests, screenshot, golden
  compare) → all stages PASS.
- Manual: reproduced the original repro steps (open console → `perf` → `screenshot`) via the
  Godot-tier test's equivalent assertions; the captured PNG's console-panel region is no longer
  console-dark.

## Deviations from Assessment

None. The fix follows the assessment's preferred remediation shape (close → force-draw → capture →
restore, contained inside `GodotScreenshotService`, `Core.Screenshots` untouched) with no changes
needed.

## Follow-ups

- The assessment's "generalize to all excludable overlays" alternative was explicitly deferred
  (perf overlay stays visible by design, confirmed by the reporter) — no action needed unless a
  future overlay raises the same complaint.
