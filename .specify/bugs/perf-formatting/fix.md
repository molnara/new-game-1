# Bug Fix: Performance overlay text overflows its background box

- **Slug**: perf-formatting
- **Fixed**: 2026-09-02
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

Widened the performance overlay's background `ColorRect` in `PerfMonitor.BuildOverlayUi` so the
longest overlay line ("Frame time: {avg} ms avg / {worst} ms worst") fits fully inside it, and added
a headless test that measures the actual rendered width of a worst-case line against the panel's
content width so a future overlay-content or font change can't silently reintroduce the overflow.

## Changes

| File | Change | Notes |
|------|--------|-------|
| `src/Game/Autoloads/PerfMonitor.cs` | modified | `_overlayPanel.OffsetLeft` widened from `-260` to `-380` (panel: 252px → 372px, usable text width: 236px → 356px). Added an internal `MeasureWorstCaseOverlayFit()` seam (mirrors the existing `IsOverlayVisible` pattern) that measures a worst-case line against the panel's live content width. |
| `tests/Game.Tests/OverlayToggleTest.cs` | added test | `WorstCaseOverlayLineFitsInsideTheBackgroundPanel` asserts the worst-case line's rendered width is ≤ the panel's content width. |

## Diff Highlights

```csharp
// PerfMonitor.BuildOverlayUi
_overlayPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
// Wide enough for the longest overlay line ("Frame time: {avg} ms avg / {worst} ms
// worst", including the "unavailable" case for memory/draw calls, FR-041a) with a
// safety margin for larger numbers (issue #2: text was clipped at the old 252px width).
_overlayPanel.OffsetLeft = -380;
```

```csharp
// New test-only seam, mirrors IsOverlayVisible
internal (float LineWidthPx, float ContentWidthPx) MeasureWorstCaseOverlayFit()
{
    const string worstCaseLine = "Frame time: 99.99 ms avg / 99.99 ms worst";
    var font = _overlayLabel.GetThemeFont("font");
    var fontSize = _overlayLabel.GetThemeFontSize("font_size");
    var lineWidth = font.GetStringSize(worstCaseLine, fontSize: fontSize).X;
    var horizontalPadding = _overlayLabel.OffsetLeft - _overlayLabel.OffsetRight;
    var contentWidth = _overlayPanel.Size.X - horizontalPadding;
    return (lineWidth, contentWidth);
}
```

## Tests Added or Updated

- `tests/Game.Tests/OverlayToggleTest.cs::WorstCaseOverlayLineFitsInsideTheBackgroundPanel` — pins
  down that the widest line the overlay can produce (two-digit avg/worst frame times) fits inside
  the panel's content width, measured with the label's real theme font at runtime rather than an
  eyeballed screenshot. This test caught the fix under-shooting on the first attempt (`-350` gave
  326px of content width against a measured 328px line) before the final `-380` value was chosen,
  confirming it actually exercises the real font metrics rather than passing vacuously.

## Local Verification

- Commands run:
  - `dotnet build NewGame1.sln` → Build succeeded, 0 errors (pre-existing warnings only, none
    introduced by this change).
  - `dotnet format NewGame1.sln --verify-no-changes --no-restore` → clean, no output.
  - `xvfb-run -a godot --headless --rendering-method gl_compatibility res://scenes/Main.tscn --
    --run-tests --quit-on-finish` → `Test results: Passed: 9 | Failed: 0 | Skipped: 0` (includes the
    new overlay-width test).
  - `dotnet test tests/Core.Tests/NewGame1.Core.Tests.csproj` → `Passed: 61, Failed: 0, Skipped: 0`.
- Manual checks: attempted a manual screenshot capture of the overlay (via a throwaway test that
  toggled `SetOverlayVisible(true)` and called `GodotScreenshotService.Capture`) to visually confirm
  the wider box against the reporter's screenshot; it failed with a null-texture error specific to
  running a capture from inside the `--run-tests` harness's rendering context (not present when
  `scripts/screenshot.sh` runs the main scene normally, since that's a plain, non-test viewport). The
  throwaway test and its `.uid` sidecar were deleted before committing — nothing from that attempt is
  part of this change. Visual/pixel confirmation is therefore not available in this environment for
  this change; the headless width-measurement test is the verification of record (see Follow-ups).

## Deviations from Assessment

- The assessment's first-listed test option — a golden-image regression via
  `scripts/screenshot.sh` / `scripts/update-golden.sh` — was not added. `scripts/screenshot.sh`
  captures the main scene exactly as it boots, and there is no existing command-line or cmdline-flag
  mechanism to toggle the perf overlay on before that capture (the overlay is only reachable via the
  in-game dev console, FR-038). Building one would mean adding new cmdline plumbing to
  `PerfMonitor`/`ScreenshotHarness` that the assessment's proposed remediation did not call for, so
  it was left out to keep the change minimal. The assessment's second, optional option — a headless
  assertion computing the rendered width of a worst-case line — was implemented instead and is the
  only test for this fix.
- Widened by 120px (`-260` → `-380`) rather than the assessment's suggested 80–100px. The new
  headless test measured the actual rendered worst-case line at 328px; an 80–100px widening
  (`-340`/`-360`) would have landed the usable content width at 316–336px, too tight a margin
  against font-rendering variance. `-380` (356px usable) was chosen after the test caught a first
  attempt at `-350` (326px usable) as still 2px short.

## Follow-ups

- Consider adding a `--perf-overlay` (or similar) cmdline flag to `PerfMonitor`/`ScreenshotHarness`
  purely for test/CI purposes, so a golden-image regression for the overlay-on state becomes
  possible without relying on the interactive console. Would also enable real visual confirmation of
  this fix, which this environment could not produce.
