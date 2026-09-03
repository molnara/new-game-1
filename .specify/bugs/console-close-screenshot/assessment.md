# Bug Assessment: `screenshot` console command captures the console panel itself

- **Slug**: console-close-screenshot
- **Created**: 2026-09-02
- **Source**: https://github.com/molnara/new-game-1/issues/4
- **Verdict**: valid
- **Severity**: medium

## URL Handling

- **Verbatim URL**: `https://github.com/molnara/new-game-1/issues/4`
- **Host**: `github.com`
- **Policy branch**: allowlisted (fetched without prompting, via `gh issue view`)

## Report (verbatim)

> Title: Console not closing after screenshot command
>
> The screenshot command should close the console first BEFORE saving a screenshot. This should
> work in both headless and on the host. We can't verify against golden images without this.
>
> For example, if I run command `perf` and then run `screenshot`, the console is still open and
> the screenshot still includes the console overlay.
>
> [Attached image: viewport screenshot showing the dev console panel (output log + input line)
> overlaid on top of the game view]

Issue #4, opened 2026-09-02, labeled `bug`, no comments.

## Symptom

Running the `screenshot` command from inside the dev console captures a PNG that includes the
console's own panel (output log + input line), instead of the clean game view expected for golden
image comparisons. Expected: the console closes itself before the frame is captured, so the PNG
only shows the game (and any other overlays the user intentionally left open, e.g. the perf
overlay from a prior `perf` command).

## Reproduction

1. Launch the game (host or `xvfb-run … godot --rendering-method gl_compatibility`).
2. Open the dev console (backtick / `console_toggle`).
3. Type `perf` and press Enter (toggles the perf overlay — incidental to this bug, just
   establishes the reporter's exact sequence).
4. Type `screenshot` and press Enter.
5. Inspect `artifacts/main.png` (or `artifacts/<name>.png`): the dev console panel (semi-transparent
   black rect with output text and input line) is visible in the captured image, overlapping the
   game view.

This reproduces identically whether triggered via the in-console `screenshot` command; the
`--screenshot <name>` command-line harness (`ScreenshotHarness.cs`) is a separate code path and is
not implicated (the console isn't open in that flow, since it starts before any input exists).

## Suspected Code Paths

- `src/Game/Infrastructure/GodotScreenshotService.cs:26` — `Capture()` reads
  `((SceneTree)Engine.GetMainLoop()).Root.GetTexture()?.GetImage()`, i.e. whatever was last
  rendered to the root viewport, with no attempt to hide overlay UI or force a fresh render
  beforehand.
- `src/Game/Autoloads/DevConsole.cs:9` / `:134` — `DevConsole` is a `CanvasLayer` with
  `Layer = 100` (drawn above everything else) whose `_panel` stays `Visible = true` for the entire
  time the user is typing and submitting commands, including the `screenshot` command itself.
  Nothing in `OnSubmitted` (`DevConsole.cs:178`) special-cases `screenshot` (or any other command)
  to hide the panel first.
- `src/Core/Screenshots/ScreenshotCommand.cs:21` — `Execute()` calls `service.Capture(name)`
  synchronously and has no way to signal "hide overlays / wait a frame" back up to the console,
  because `CommandDescriptor.Handler` (`src/Core/Console/CommandDescriptor.cs:15`) is a plain
  `Func<CommandArgs, CommandResult>` with no async/deferred capability.
- `src/Game/Autoloads/ScreenshotHarness.cs:54-58` — the CLI (`--screenshot`) harness already solves
  an analogous problem (waiting for rendering to catch up) by `await`-ing
  `ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame)` for a configurable number of frames
  before capturing. This is the precedent for "capturing stale-looking frames requires an explicit
  wait/force-draw," but that harness never has the console open in the first place, so it doesn't
  need to hide anything.

## Root Cause Hypothesis

**Confidence: high.** `GodotScreenshotService.Capture()` reads back whatever is currently in the
viewport's render target. The dev console is a top-layer (`Layer=100`) `CanvasLayer` that remains
visible for the entire duration of typing and submitting a command, including `screenshot` itself.
Because nothing hides the console panel (or any other overlay) before the capture, and nothing
forces a fresh render after hiding it, the captured image reflects the console-open state at the
moment `screenshot` was typed. This directly blocks golden-image verification per the reporter's
stated motivation ("We can't verify against golden images without this").

A secondary, real constraint: even if the console were hidden synchronously right before
`Capture()`, Godot's viewport texture reflects the *last rendered* frame — hiding a node and
immediately reading `GetTexture().GetImage()` in the same synchronous call, with no intervening
render, would very likely still show the stale (console-open) frame. `RenderingServer.ForceDraw()`
(used for exactly this "hide UI, force one flushed frame, screenshot" pattern in Godot) or an
async frame wait (as `ScreenshotHarness` already does) is needed to guarantee the hidden state is
actually what gets rasterized. `CommandDescriptor.Handler` being purely synchronous
(`Func<CommandArgs, CommandResult>`, `CommandRegistry.Execute` at `CommandRegistry.cs:57`) means
the fix can't simply `await` a frame inside `ScreenshotCommand.Execute` without also touching that
synchronous contract, unless the force-draw approach is used instead.

## Proposed Remediation

**Preferred**: Keep the fix inside `GodotScreenshotService.Capture()` (Game/Infrastructure layer,
which already talks directly to Godot's `SceneTree`/`Viewport` APIs) rather than teaching
`Core.Screenshots` about the console:

1. Locate the `DevConsole` autoload from the scene tree (it's registered as `/root/DevConsole` per
   `project.godot`'s `[autoload]` section).
2. If its panel is open (`DevConsole.IsOpen`), close it (there's already a `SetOpen`-equivalent
   surface — `IsOpen` is public but `SetOpen` is private; expose a public `Close()`/`SetOpen`
   method for this purpose).
3. Force the hidden state to actually be rendered before reading back the texture —
   `RenderingServer.ForceDraw()` (Godot's documented mechanism for "flush a frame synchronously
   right now," used by other engine screenshot tooling) — rather than relying on an async frame
   wait, since `Capture()` must stay synchronous to satisfy `IScreenshotService`'s existing
   synchronous contract used by both the console command and `ScreenshotHarness`.
4. Capture as today.
5. Restore the console's prior open/closed state afterward (re-open if it had been open), so the
   `screenshot` command doesn't have the side effect of silently closing a console the user was
   still using.

This keeps `Core.Screenshots` (including `ScreenshotCommand` and the `IScreenshotService`
interface) completely unaware of the console, preserving the existing Core/Game split
(`IScreenshotService.cs:5` explicitly notes `ScreenshotCommand` lives in Core so it can be tested
against a fake service — that must not regress).

**Alternatives** (optional):
- Special-case `screenshot` inside `DevConsole.OnSubmitted` (close panel, await a frame via
  `ToSignal`, execute, reopen). Rejected as preferred: `OnSubmitted`/`Registry.Execute` are
  synchronous today, and this would need `CommandDescriptor.Handler`'s signature to grow
  async-awareness for every command, or a one-off async branch that only `screenshot` uses —
  more invasive than containing the fix inside the Game-side screenshot service.
- Generalize to "hide *all* CanvasLayer overlays flagged as excludable" instead of specifically
  targeting `DevConsole`. Worth a follow-up if `PerfMonitor`'s overlay also turns out to need
  excluding, but the reporter's example explicitly keeps `perf`'s overlay in frame ("if I run
  command `perf` and then run `screenshot`" — no complaint about the perf overlay showing), so
  scope this fix to the console only for now.

**Files likely to change**:
- `src/Game/Infrastructure/GodotScreenshotService.cs`
- `src/Game/Autoloads/DevConsole.cs` (expose a way to query + set open state, e.g. a public
  `Close()`/`Open()` pair, without making the panel field itself public)

**Tests to add or update**:
- A host/Godot-tier test (`specs/001-dev-foundations` already establishes a Godot test tier per
  research R1/R10) that: opens the console, submits `screenshot`, and asserts the resulting PNG
  does not contain the console panel's overlay (e.g. via a golden-image compare, or a simpler
  check that no console-colored pixels are present at the panel's known screen region).
- A regression test that the console's open/closed state is restored after the screenshot
  command runs (i.e. `screenshot` from an open console leaves the console open afterward).
- Existing fast-tier `ScreenshotCommand`/`IScreenshotService` fake-based tests should keep passing
  unchanged, since the fix lives in `GodotScreenshotService`, not `Core.Screenshots`.

## Risks & Considerations

- `RenderingServer.ForceDraw()` forces a synchronous render pass mid-frame; needs verification it
  behaves correctly under `xvfb-run` + `gl_compatibility` in this container (per this project's own
  note that headless/software-GL rendering is "not pixel-identical to GPU" — the fix should be
  verified with a real headless run, not just reasoned about).
- Reaching from `GodotScreenshotService` (Infrastructure) into `DevConsole` (Autoloads) is a new
  coupling; both are in the `Game` project so it doesn't cross the Core/Game boundary, but it does
  mean the screenshot service now has an implicit dependency on the console autoload existing at
  `/root/DevConsole` — should degrade gracefully (not throw) if that node is ever absent (e.g. a
  future scene without the autoload, or a unit test constructing `GodotScreenshotService` directly
  via its internal constructor).
- The `--screenshot` CLI harness (`ScreenshotHarness.cs`) is unaffected since it runs before any
  console interaction is possible, but the shared `IScreenshotService.Capture()` codepath means
  the added console-hide logic runs on every capture, console open or not — must be a no-op
  (cheap `IsOpen` check) when the console was never opened, so the CLI harness's behavior and
  timing (`frameDelay`) are not perturbed by an extra `ForceDraw()` call.

## Open Questions

Both resolved by the reporter (2026-09-02):

- **Restore vs. leave closed**: restore the console to its prior open state after the screenshot
  (confirmed) — the reporter typically wants to run further commands afterward, so `screenshot`
  should not have the side effect of closing the console out from under them.
- **Perf overlay**: out of scope, keep as-is (confirmed) — only the console panel should be hidden
  during capture; the perf overlay is left visible by design.

These confirm the "Preferred" remediation above (close → force-draw → capture → restore) as
written, with no changes needed to its shape.
