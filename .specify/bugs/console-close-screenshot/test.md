# Bug Verification: `screenshot` console command captures the console panel itself

- **Slug**: console-close-screenshot
- **Tested**: 2026-09-02
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: verified

## Summary

The fix is applied and committed (`a40c2df`). The full `scripts/verify.sh` gate passes end to end,
and the Godot-tier test that exercises the exact bug path (`GodotScreenshotService.Capture()` with
the console open) confirms the console panel is no longer in the captured pixels and that the
console's prior open state is restored afterward. No regressions found in the Core test suite, the
CLI screenshot harness, code style, or the golden-image compare.

## Checks Performed

| Check | Command / Action | Result | Notes |
|-------|------------------|--------|-------|
| Reproduction (post-fix) | `ScreenshotConsoleTest.CaptureHidesTheOpenConsoleAndRestoresItAfterward` via `scripts/verify.sh` → Godot tests stage | pass | Not a literal keystroke-driven repro (no simulated typing of `perf`/`screenshot`); calls `GodotScreenshotService.Capture()` directly with the console opened via `DevConsole.Open()`. Confirmed by reading `ScreenshotCommand.Execute()` (`src/Core/Screenshots/ScreenshotCommand.cs:30`) that this is exactly what the in-console `screenshot` command invokes — no intervening logic — so the test is a faithful reproduction of the reported bug path, not just an approximation. |
| New / updated tests | `xvfb-run -a godot --rendering-method forward_plus --rendering-driver vulkan --audio-driver Dummy res://scenes/Main.tscn -- --run-tests --quit-on-finish` (via `scripts/verify.sh`) | pass | Both new tests in `tests/Game.Tests/ScreenshotConsoleTest.cs` ran and passed as part of the Godot test stage. |
| Regression suite (Core) | `dotnet test tests/Core.Tests/NewGame1.Core.Tests.csproj` (via `scripts/verify.sh`) | pass | Existing `ScreenshotCommand`/`IScreenshotService` fake-based tests unaffected, as expected since the fix lives entirely in `GodotScreenshotService` (Game layer), not `Core.Screenshots`. |
| CLI screenshot harness (unaffected path) | `scripts/screenshot.sh main` (via `scripts/verify.sh`'s Screenshot stage) | pass | Confirms the added console-hide logic is a no-op on the `--screenshot` CLI path (console never open there), matching the assessment's risk note. |
| Golden compare | `scripts/compare-golden.sh` against `tests/golden/main.png` (via `scripts/verify.sh`) | pass | Exercises the CLI harness path only, not the in-console command — see Residual Risks. |
| Lint / style | `dotnet format NewGame1.sln --verify-no-changes --no-restore` (via `scripts/verify.sh`) | pass | No formatting/analyzer violations. |
| Build | `dotnet build NewGame1.sln` | pass | 0 errors, 21 pre-existing warnings unrelated to the changed files (`DevConsole.cs`/`GodotScreenshotService.cs` diffs introduce no new warnings). |

## Output Excerpts

```
PASS: Build
PASS: Code style
PASS: Core tests
PASS: Godot tests
PASS: Screenshot
PASS: Golden compare
screenshot: /workspace/artifacts/main.png
```

`GodotScreenshotService.Capture()` (post-fix, `src/Game/Infrastructure/GodotScreenshotService.cs:26-40`) closes the console, calls `RenderingServer.ForceDraw()`, captures, and reopens the console in a `finally` block — matching the assessment's preferred remediation exactly.

## Residual Risks

- The reproduction did not literally simulate keyboard input (`console_toggle` key, typing `perf`
  then `screenshot`, pressing Enter) — headless CI has no interactive input path for that. The
  Godot test instead drives the console open state and screenshot capture programmatically, which
  is code-path-equivalent (verified by reading `ScreenshotCommand.Execute`) but is a step removed
  from an end-to-end manual repro on the host with a real display.
- `scripts/verify.sh`'s "Screenshot" and "Golden compare" stages only exercise the `--screenshot`
  CLI harness (console never open), not the in-console `screenshot` command — so the golden-image
  comparison itself does not cover this bug's path. Coverage of the actual bug relies solely on the
  `ScreenshotConsoleTest` pixel-brightness assertions, not a golden-image diff.
- I did not revert the fix to confirm the new test actually fails without it (would require
  modifying source code, which this command's guardrails prohibit); confidence that the test is not
  vacuous instead comes from reading `GodotScreenshotService.Capture()`'s logic directly and
  confirming it matches the assessment's root-cause description.
- Not verified on the host (GPU) Godot editor per the assessment's stated risk about
  `RenderingServer.ForceDraw()` under software GL — only the container's `xvfb-run` +
  `forward_plus`/vulkan path was exercised here.

## Recommendation

Close the bug — verified via the automated Godot-tier test exercising the actual code path the
`screenshot` console command uses, plus a clean full `scripts/verify.sh` run with no regressions.
Optionally, a follow-up could add a true keystroke-driven or golden-image-based check of the
in-console command specifically, since the current golden-compare stage doesn't cover that path.
