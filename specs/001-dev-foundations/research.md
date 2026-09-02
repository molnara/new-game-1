# Phase 0 Research: Developer Foundations

**Date**: 2026-09-02 | **Plan**: [plan.md](./plan.md)

Every unknown in the Technical Context is resolved below. Findings marked **spike-verified** were
proven by running code in this container during planning, not reasoned about.

---

## R1. Headless screenshot capture — CORRECTED ASSUMPTION (spike-verified)

**Decision**: The screenshot harness runs under `xvfb-run` with a real rendering driver and
`--audio-driver Dummy`. It must **not** run under `--headless`.

*Which* driver is settled by R10, which supersedes the OpenGL3 choice recorded here: captures use
`--rendering-method forward_plus --rendering-driver vulkan` to match the host editor's renderer. The
finding below — that `--headless` cannot capture at all — is unaffected by that change.

**Rationale**: `--headless` selects Godot's dummy rendering backend, which has no viewport texture.
The spike proved capture is impossible there — `get_viewport().get_texture()` returns null:

```
ERROR: Parameter "t" is null.
   at: texture_2d_get (./servers/rendering/dummy/storage/texture_storage.h:110)
SCRIPT ERROR: Cannot call method 'save_png' on a null value.
```

The same scene under `xvfb-run -a godot --rendering-driver opengl3 --audio-driver Dummy` succeeded:

```
OpenGL API 4.5 (Core Profile) Mesa 25.2.8 - Compatibility - Using Device: Mesa - llvmpipe
save err: 0 size: (640, 360)
```

The resulting PNG was inspected and verified to contain the rendered scene — mean RGB
(0.201, 0.451, 0.800) matched the source `ColorRect` colour (0.2, 0.45, 0.8) exactly, with 100
distinct colours from text antialiasing.

**Consequence for the plan**: this is why `screenshot.sh` exists as a wrapper rather than the
harness being invoked directly. It is also why `--audio-driver Dummy` is mandatory and not
cosmetic — without it the process stalls on ALSA device probing in a container with no sound card,
which is what made the first spike attempt appear to hang.

**Alternatives considered**: `--headless` with a manually created `SubViewport` (rejected — the
dummy driver has no rasterizer at all, so nothing renders regardless of viewport arrangement);
Vulkan via lavapipe (initially set aside here as the heavier path, then **adopted** in R10 once it
turned out to be the only way to capture under the same renderer the host editor uses — the 173 ms
it costs buys away an entire category of golden-image drift).

---

## R2. Render determinism and the golden-image threshold (spike-verified)

**Decision**: Compare with ImageMagick `compare -metric AE` against a pixel-count threshold,
defaulting to a small non-zero tolerance. Goldens are generated and compared **in the container
only**.

**Rationale**: two consecutive runs of the same scene produced byte-identical PNGs (identical MD5,
`AE` difference of 0). Software rendering through llvmpipe is deterministic on the same machine, so
in-container comparison could in principle demand an exact match.

The threshold exists for a different reason: the developer also runs the Godot editor on the host —
Arch Linux — against this same folder, where rendering goes through the host's real GPU driver
rather than llvmpipe. Hardware and software rasterizers differ in antialiasing, texture filtering
and floating-point detail, so a host capture will not be byte-identical to a container one even
though both are Linux and both may be using Mesa. A zero-tolerance golden would fail the first time
it was checked outside the container.

**Confidence**: unlike the rest of this document, this paragraph is reasoned rather than
spike-verified — the host GPU is not reachable from inside the container. The practical consequence
is that the threshold's starting value is a hedge, and should be calibrated the first time a golden
is actually compared on the host rather than treated as settled.

**Consequence**: goldens are container artifacts. `compare-golden.sh` takes a threshold argument so
the policy is visible and tunable rather than buried, and the quickstart documents that goldens are
regenerated in the container.

**Refined by R10**: the threshold has two distinct causes mixed into it — the renderer the capture
runs under, and hardware-versus-software rasterization. The first is eliminable and should be
eliminated; only the second genuinely needs a tolerance. See R10.

**Alternatives considered**: perceptual hashing (rejected — more machinery than a pixel count for a
flat placeholder scene); `-metric RMSE` with a fuzz factor (viable, but `AE` yields a plain
"how many pixels differ" count that is far easier to reason about when a golden fails).

---

## R3. ImageMagick invocation form (spike-verified)

**Decision**: Call `compare`, `convert`, and `identify` directly. Do not use `magick`.

**Rationale**: the container has ImageMagick 6.9.12, which ships the classic separate binaries. The
IM7 `magick` wrapper is not installed (`magick: command not found`). Scripts written against IM7
syntax would fail on this machine.

**Detail that matters for scripting**: `compare -metric AE a.png b.png null:` writes the pixel count
to **stderr**, not stdout, and exits non-zero when the images differ. `compare-golden.sh` must
capture stderr and must not let `set -e` abort on the expected non-zero exit.

---

## R4. Godot exit codes are not a reliable failure signal (spike-verified)

**Decision**: `verify.sh` determines Godot-stage success by scanning output for error markers, not
by exit code alone.

**Rationale**: running the project with no main scene defined produced a fatal error and still
exited **0**:

```
Error: Can't run project: no main scene defined in the project.
=== EXIT: 0 ===
```

A verification script trusting `$?` would report a green build for a game that cannot start. This
confirms the approach already used in `CLAUDE.md`'s smoke check (grepping for `script error|parse
error`) and generalises it: the screenshot stage additionally asserts that the expected PNG exists
and is non-empty, which is a positive signal rather than an absence-of-error one.

---

## R5. User command-line arguments reach the game (spike-verified)

**Decision**: Activate the screenshot harness with `OS.GetCmdlineUserArgs()`, invoked as
`godot -- --screenshot <name>`.

**Rationale**: the spike confirmed arguments after `--` arrive intact: `user args: ["--myflag"]`.
This keeps engine flags and game flags cleanly separated, so the harness cannot be confused by
Godot's own arguments.

**Consequence**: the harness autoload is always loaded but inert. It inspects user args in `_Ready`
and, absent `--screenshot`, does nothing at all — no frame counting, no capture, zero cost in a
normal play session.

---

## R6. Log file location (spike-verified)

**Decision**: Session logs are written to `user://logs/`, which resolves to
`~/.local/share/godot/app_userdata/new game 1/logs/` on this container.

**Rationale**: confirmed at runtime — `OS.get_user_data_dir()` returned
`/home/dev/.local/share/godot/app_userdata/spike` for a project named "spike", so the path follows
`config/name` in `project.godot` (currently `"new game 1"`).

**Interaction to be aware of**: `project.godot` already sets
`debug/file_logging/enable_file_logging=true`, so Godot writes its own `godot.log` into the same
`logs/` folder. This is complementary, not conflicting — Godot's log captures engine-level output
while the Serilog file captures structured application entries — but log retention (FR-006) must
prune only our own session files and leave Godot's alone.

---

## R7. Satisfying the flush policy with Serilog

**Decision**: Configure `Serilog.Sinks.File` with `buffered: true` and a short
`flushToDiskInterval`, wrapped in a small decorating sink that forces a flush when an event is
`Warning` or above (via the sink package's `IFlushableFileSink`). Call `Log.CloseAndFlush()` on
shutdown.

**Rationale**: this maps directly onto the clarified durability decision — warnings and errors reach
disk as they occur, routine chatter batches and does not cost a write per frame.

**Fallback if `IFlushableFileSink` proves awkward in practice**: configure `buffered: false`
outright. Every entry is then handed to the OS as it is written, which *exceeds* the durability
requirement and only loses data on a machine crash rather than a process kill. The cost is a write
syscall per entry. This fallback satisfies FR-005 strictly, so the task is not at risk either way —
it is a performance refinement, not a correctness one.

**Alternatives considered**: two sinks writing the same file at different buffering settings
(rejected — concurrent writers to one file require `shared: true`, which disables buffering entirely
and defeats the purpose); a separate errors-only file (rejected — FR-001 specifies one file per
session, and splitting would make a session harder to read, not easier).

---

## R8. Keeping the console engine-free where it matters

**Decision**: `CommandRegistry`, `CommandLineParser`, `HelpCommand`, `BoundedLog` and
`ScreenshotName` validation live in `src/Core`. `DevConsole` (the `CanvasLayer`) holds only input
handling and text rendering.

**Rationale**: constitution I plus constitution II's preference for the fast test tier. Everything
with a decision in it — how a line is tokenised, what `help` prints, what happens on a duplicate
registration, which names are rejected, what falls off the end of a bounded history — is testable in
milliseconds with xUnit and needs no engine.

**The one wrinkle, and its resolution**: the `screenshot` command must live in the same registry as
`help`, but capturing needs the engine. Core therefore declares `IScreenshotService` and
`src/Game/Infrastructure/GodotScreenshotService.cs` implements it, exactly the pattern constitution I
prescribes for engine services. The command handler itself stays in Core and is unit-testable
against a fake service.

**Alternatives considered**: registering the `screenshot` command from the Game side as a lambda
(rejected — the argument validation and result formatting are the interesting parts and would then
be untestable in the fast tier).

---

## R9. Console input isolation

**Decision**: `DevConsole` is a `CanvasLayer` autoload with `ProcessMode = Always`, handling the
toggle in `_UnhandledKeyInput` and calling `GetViewport().SetInputAsHandled()` on the toggle event.

**Rationale**: FR-011 requires the backtick not to leak into the input field, and FR-010 requires
gameplay not to receive keystrokes while the console is open. Consuming the toggle event before the
`LineEdit` sees it addresses the first; grabbing focus and marking input handled addresses the
second. `ProcessMode = Always` means the console still works if the game pauses itself — which is
precisely when a developer most wants to inspect state.

**Detail**: the toggle binds to an `InputMap` action rather than a hard-coded key, satisfying FR-009's
remappability requirement and the "backtick is awkward on some layouts" edge case.

---

## R10. Renderer choice: Forward+ vs gl_compatibility (spike-verified)

**Decision**: Pin the rendering method explicitly in `project.godot`, and have the capture harness
run under the **same** renderer the host editor uses — `forward_plus` via Vulkan, which works in
this container through Mesa's lavapipe software Vulkan driver.

**The problem found**: `project.godot` currently has **no** `rendering/renderer/rendering_method`
key. The project therefore runs Godot's default, `forward_plus`, which is what the host editor uses.
Meanwhile `CLAUDE.md` documents capturing with `--rendering-method gl_compatibility`. Host and
container would have been rendering the golden scene with two different renderers, and nothing in
the project stated which one was authoritative.

**Measurements** (640x360 = 230,400 pixels, flat background plus a text label):

| Comparison | Differing pixels | Notes |
|---|---|---|
| `--rendering-driver opengl3` vs `--rendering-method gl_compatibility` | **0** | Identical. Requesting the OpenGL driver implies the compatibility renderer, so the plan's flag and `CLAUDE.md`'s flag were never actually in conflict. |
| `gl_compatibility` vs `forward_plus` | **111** (0.048%) | RMSE 3.79/65535. Entirely glyph-edge antialiasing — a diff image shows the flat background is byte-identical and every changed pixel sits on the text. |
| `forward_plus` run 1 vs run 2 | **0** | Deterministic, same as gl_compatibility. |

**Availability**: `lvp_icd.json` is present and Godot reports `Vulkan 1.4.318 - Forward+ - llvmpipe`,
so Forward+ needs no GPU here.

**Cost**: 596 ms versus 423 ms per capture — roughly 40% slower, 173 ms in absolute terms. Against a
verification run that also builds and tests, this is noise.

**Why match the host renderer rather than take the cheaper one**: 111 pixels is trivially absorbed by
a threshold *for this scene*, which is a flat rectangle and a label. That is not the scene the
project will have for long. `gl_compatibility` genuinely lacks Forward+ features — it is a reduced
renderer, not merely a different one — so once the real scene has lighting, shadows, or any
post-processing, the two stop differing by antialiasing and start differing by whole effects being
absent. A golden captured under the wrong renderer would then be worse than no golden: it would
pass while showing something the developer never sees.

**Consequence for the plan**:

- `project.godot` should gain an explicit `rendering/renderer/rendering_method="forward_plus"` so the
  renderer is stated rather than inherited from a default that could change between Godot versions.
  This is a task for implementation, not something to change during planning — the developer runs
  the editor against this same file.
- `screenshot.sh` passes `--rendering-method forward_plus --rendering-driver vulkan`.
- With the renderer matched, the golden threshold covers only hardware-versus-software rasterization
  (R2), which is the one cause that cannot be eliminated from inside the container.

**Alternatives considered**: capture under `gl_compatibility` and accept the drift (rejected — cheap
now, actively misleading later, for 173 ms); pin the *project* to `gl_compatibility` so host and
container match at the lower capability level (rejected — that downgrades the actual game to suit
the test harness, which is precisely backwards).

---

## Remaining unknowns

None. No `NEEDS CLARIFICATION` markers remain in the Technical Context.
