# Bug Verification: Performance overlay text overflows its background box

- **Slug**: perf-formatting
- **Tested**: 2026-09-02
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: partial

## Summary

The fix builds clean, passes the full local gate (`scripts/verify.sh`), and the new
`WorstCaseOverlayLineFitsInsideTheBackgroundPanel` test confirms the panel's usable content width
(356px) now exceeds the longest possible overlay line (measured ~328px) with real theme-font
metrics. No regressions surfaced in the existing suites. However, the original assessment's
reproduction was a *visual* one (toggle the overlay via the `perf` console command and observe the
text against the background box), and — as `fix.md` itself documents — there is still no
cmdline-accessible way to toggle the overlay before a screenshot capture in this headless
environment, so that visual reproduction could not actually be re-exercised. Marking **partial**
per the "don't over-claim visual verification you didn't perform" guardrail, not because anything
failed.

## Checks Performed

| Check | Command / Action | Result | Notes |
|-------|------------------|--------|-------|
| Reproduction (post-fix, visual) | Toggle overlay via `perf` console command + screenshot | not-run | No cmdline mechanism exists to toggle the console-only (FR-038) overlay before `scripts/screenshot.sh`/`ScreenshotHarness` captures the main scene; building one would require new source changes, which this command must not make (fix.md already flags this as a Follow-up). |
| Reproduction (post-fix, geometric) | New `WorstCaseOverlayLineFitsInsideTheBackgroundPanel` test, measuring real theme-font width of the worst-case line against the panel's content width | pass | 328px measured line ≤ 356px content width (panel widened `-260`→`-380`); directly encodes the reported symptom without needing a display. |
| New / updated tests | `xvfb-run -a godot --headless --rendering-method gl_compatibility res://scenes/Main.tscn -- --run-tests --quit-on-finish` | pass | `Test results: Passed: 9 \| Failed: 0 \| Skipped: 0` — includes the new overlay-width test and unrelated existing overlay/console/capture-timing/smoke tests. |
| Regression suite (Core) | `dotnet test tests/Core.Tests/NewGame1.Core.Tests.csproj` | pass | `Passed: 61, Failed: 0, Skipped: 0`. |
| Full local gate | `bash scripts/verify.sh` | pass | Build, style, Core tests, Godot tests, main-scene screenshot, and golden-image compare all `PASS`. Screenshot/golden stages exercise the default (overlay-off) main scene, so they confirm no regression to normal rendering but don't visually cover the overlay itself. |
| Lint / type-check | `dotnet format NewGame1.sln --verify-no-changes --no-restore` | pass | Clean, no output. |
| Build | `dotnet build NewGame1.sln` | pass | 0 warnings, 0 errors. |

## Output Excerpts

```
Info (GoTest): > ^^ >> OverlayToggleTest::WorstCaseOverlayLineFitsInsideTheBackgroundPanel [Test] > Test started! :3
Info (GoTest): > OK >> OverlayToggleTest::WorstCaseOverlayLineFitsInsideTheBackgroundPanel [Test] > Test passed! :)
...
Info (GoTest): > OK >> Test results: Passed: 9 | Failed: 0 | Skipped: 0
```

```
Passed!  - Failed:     0, Passed:    61, Skipped:     0, Total:    61, Duration: 20 ms - NewGame1.Core.Tests.dll (net10.0)
```

```
PASS: Build
PASS: Code style
PASS: Core tests
PASS: Godot tests
PASS: Screenshot
PASS: Golden compare
screenshot: /workspace/artifacts/main.png
```

## Residual Risks

- No pixel/visual confirmation of the widened overlay exists in this environment (same limitation
  `fix.md` already recorded — the `perf` console command has no cmdline-accessible equivalent, and
  `--run-tests`-context screenshot capture hits a null-texture error). The geometric test is a
  faithful proxy (it uses the label's real rendered theme font, not a guessed character width) but
  is not a substitute for actually seeing the box.
- The 328px measured worst-case line width was observed in this run's headless/software-GL
  environment; font metrics could in principle shift slightly under a different renderer, though
  the fix's 28px safety margin (356 − 328) makes this low risk.
- `PerfMonitor.cs` and `OverlayToggleTest.cs` remain uncommitted in the working tree (matches the
  git status at session start) — this verification ran against the working tree as-is, not a
  committed snapshot.

## Recommendation

Hold on closing the issue as fully verified — the automated checks (build, full regression suite,
and the new geometric fit test) all pass cleanly and give strong confidence the panel is now wide
enough, but the original bug report's own evidence was a screenshot, and this environment still
cannot reproduce or re-check that visually without adding new cmdline plumbing (which is out of
scope for this command). If a true visual sign-off is required before closing, either add the
`--perf-overlay`-style cmdline flag `fix.md` proposes as a Follow-up so `scripts/screenshot.sh` can
capture the overlay-on state, or have the developer eyeball it in the host Godot editor.
Otherwise, treat the geometric test as sufficient and close.
