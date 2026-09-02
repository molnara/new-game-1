# Bug Assessment: Performance overlay text overflows its background box

- **Slug**: perf-formatting
- **Created**: 2026-09-02
- **Source**: https://github.com/molnara/new-game-1/issues/2
- **Verdict**: valid
- **Severity**: low

## URL Handling

- **Verbatim URL**: `https://github.com/molnara/new-game-1/issues/2`
- **Host**: `github.com`
- **Policy branch**: `allowlisted` (github.com is on the pre-approved fetch list — fetched without prompting)

## Report (verbatim or summarized)

**Title**: Fix Performance Monitor formatting
**Author**: molnara · **State**: Open · **Labels**: bug · **Created**: 2026-09-02
**Project**: 001-dev-foundations (Status: To triage)

> The issue includes an image showing a Performance Monitor display with text overflow. The
> problem statement reads: "The width of the box should be increased so all text fits inside."

No comments. No assignees/milestone. The attached screenshot was retrieved and inspected directly
(user-supplied signed image URL, downloaded and viewed): it shows the overlay in its top-right
corner over the placeholder scene. Four of the five lines (FPS, Draw calls, Process memory, Video
memory) fit fully inside the dark background panel. Only the first line —
`Frame time: 6.06 ms avg / 6.06 m` — is cut off at the panel's right edge (and at the edge of the
captured window itself), truncating the `worst` figure and its `ms worst` suffix. This confirms the
code-derived hypothesis below exactly.

## Symptom

The performance overlay (toggled on with the `perf` console command) draws its metric text wider
than the dark background panel behind it, so some text spills outside — and is not fully legible
against — the box that's supposed to contain it. Expected: all five overlay lines fit fully inside
the background panel.

## Reproduction

1. Launch the game headlessly with the console enabled.
2. Run the `perf` console command to show the overlay (`PerfMonitor.SetOverlayVisible(true)`).
3. Wait ~0.25s+ for `RefreshOverlayText()` to populate real numbers (frame time/FPS/draw
   calls/memory).
4. Observe the first line, `Frame time: {avg} ms avg / {worst} ms worst`, clipped at the panel's
   right edge — confirmed in the reporter's screenshot (165 FPS, 4 draw calls, 631.2 MB process /
   26.2 MB video memory shown correctly; "Frame time: 6.06 ms avg / 6.06 m" cut off mid-word).

## Suspected Code Paths

- `src/Game/Autoloads/PerfMonitor.cs:201-221` (`BuildOverlayUi`) — hardcodes the overlay
  background (`_overlayPanel`, a `ColorRect`) to a fixed 252×132px box
  (`OffsetLeft=-260`/`OffsetRight=-8` → 252px wide; `OffsetTop=8`/`OffsetBottom=140` → 132px tall),
  with 8px of horizontal label padding on top (`OffsetLeft=8`/`OffsetRight=-8` on the label),
  leaving ~236px of usable text width.
- `src/Game/Autoloads/PerfMonitor.cs:223-234` (`RefreshOverlayText`) — builds the five display
  lines, the longest of which is `"Frame time: {avg:F2} ms avg / {worst:F2} ms worst"` (up to ~42
  characters with two-digit values), plus lines like `"Process memory: {bytes} MB"` /
  `"Video memory: {bytes} MB"` which can read `"unavailable"` (FR-041a) and are equally long.
- The `Label` created in `BuildOverlayUi` (line 213) has no `AutowrapMode` or `ClipContentsMode`
  configured, so it uses Godot's default of no wrapping and no clipping — text simply overflows the
  control's (and therefore the `ColorRect`'s) bounds rather than wrapping onto more lines or being
  cut off cleanly.

## Root Cause Hypothesis

**Confidence: confirmed** (verified directly against the reporter's screenshot). At the overlay's
font size, the first line — `"Frame time: {avg:F2} ms avg / {worst:F2} ms worst"` — is the longest
of the five and is the only one that overflows; it renders wider than the ~236px of usable width
inside the fixed 252px-wide background panel while the other four (FPS, Draw calls, Process memory,
Video memory) fit comfortably. Because the `Label` has no autowrap or clipping set, the excess text
draws past the `ColorRect`'s right edge instead of wrapping or being truncated — exactly matching
the reporter's screenshot ("Frame time: 6.06 ms avg / 6.06 m" cut off mid-word) and stated fix ("the
width of the box should be increased"). This is a hardcoded-size layout bug, not a data or logic
error: the values themselves are computed and formatted correctly (per
`FormatCount`/`FormatBytes`/`FormatPercentile`), only the container is too narrow for its longest
line.

## Proposed Remediation

**Preferred**: Widen `_overlayPanel` in `BuildOverlayUi` so its usable text width comfortably fits
the longest line the overlay can produce at the default overlay font size — including the
"unavailable" case for memory/draw-call figures (FR-041a) — with a safety margin for larger numbers
(e.g. draw calls or memory in the thousands). Concretely, increase the magnitude of `OffsetLeft`
(currently `-260`) by roughly 80–100px; height can stay as-is since the fix is width-only and the
issue only calls out horizontal overflow. Re-verify visually with `scripts/screenshot.sh` (overlay
toggled on) since there is no GPU in this environment to eyeball it interactively.

**Alternatives** (optional):
- Compute the panel width dynamically in `BuildOverlayUi`/`RefreshOverlayText` from
  `Font.GetStringSize()` on the longest candidate line, so future changes to the displayed metrics
  (new fields, longer labels) can't silently reintroduce the overflow. More robust, but more change
  than the reported issue asks for.
- Set `AutowrapMode` on the label and grow the panel's *height* instead of its width. Rejected as
  primary fix: the reporter explicitly asked for a wider box, and wrapping would shift the line
  count/vertical layout of a debug overlay whose refresh cadence (FR-039) and content (FR-037)
  are otherwise stable.

**Files likely to change**:
- `src/Game/Autoloads/PerfMonitor.cs` (`BuildOverlayUi` offsets)

**Tests to add or update**:
- A golden-image regression: capture a screenshot with the overlay toggled on (via
  `scripts/screenshot.sh` + `scripts/update-golden.sh`, following the same pattern as
  `tests/golden/main.png`) so a future change to the overlay's content or font can't silently
  regress the box width again. `tests/golden/` currently only has a golden for the overlay-off main
  scene.
- Optionally, a headless assertion (in the `Game.Tests` project, alongside
  `OverlayToggleTest.cs`) that computes the rendered width of a representative worst-case line
  (e.g. via `Label.GetThemeFont("font").GetStringSize(...)`) and asserts it is ≤ the panel's
  content width, so the check doesn't depend on eyeballing a screenshot.

## Risks & Considerations

- Purely cosmetic/layout change confined to a dev-only, off-by-default overlay (FR-038); no
  gameplay, API, or logged-statistics behavior changes (the log line in `WriteStatistics` is
  unaffected).
- A wider overlay covers slightly more of the top-right of the screen while visible — acceptable
  for a developer debug tool, but worth a quick visual sanity check at common resolutions.
- No migration, no persisted state, no security implications.

## Open Questions

None. The reporter's screenshot was retrieved and inspected directly, confirming the root cause
and scope above (only the "Frame time" line overflows).
