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

## R11. Where the profiling numbers come from (spike-verified)

**Decision**: frame time is measured from the engine's own per-frame delta; draw calls and video
memory come from Godot's `Performance` monitors; total process memory is read from
`/proc/self/status`. FPS is *derived* from frame time, never read from the engine.

**Measured in this container** (Forward+ / lavapipe, trivial scene):

| Source | Value | Verdict |
|---|---|---|
| `Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME` | 2 | **Use.** Works under Forward+ software Vulkan. |
| `Performance.RENDER_VIDEO_MEM_USED` | 16,546,288 (~16.5 MB) | **Use** — this is FR-047's video memory figure. |
| `Performance.MEMORY_STATIC` / `OS.get_static_memory_usage()` | 44,772,703 (~44.7 MB) | **Do not use as "memory".** Engine-tracked allocations only. |
| `/proc/self/status` `VmRSS` | 510,168 kB (~510 MB) | **Use** — this is FR-047's total process memory. |
| `OS.get_memory_info()` | `physical: 33.4 GB` | **Wrong thing.** Reports *system* RAM, not this process. An easy mistake to make from the name. |
| `Performance.TIME_FPS` / `Engine.get_frames_per_second()` | **stuck at 1.0** across a 480-frame run | **Do not use.** See below. |
| `Performance.TIME_PROCESS` | **0.00000** throughout | **Do not use.** Below reporting resolution here. |

**The finding that vindicates the memory clarification**: engine-tracked memory reported 44.7 MB
while the process was actually using 510 MB — an 11x gap. Had FR-047 been answered "engine-tracked
allocations", the overlay would have serenely under-reported real memory use by an order of
magnitude. The clarified answer (process memory *and* video memory, separately labelled) is the one
that reflects reality.

**The FPS trap**: `Performance.TIME_FPS` and `Engine.get_frames_per_second()` both read a constant
`1.0` for an entire 480-frame run, because Godot recomputes that counter once per wall-clock second
and the run never spanned one. Meanwhile frame time accumulated from the `delta` argument tracked
correctly and steadily (0.88–1.07 ms average). Reading the engine's FPS monitor would therefore
produce a plausible-looking but frozen number in exactly the short automated runs where the overlay
is most likely to be screenshotted as evidence.

**Consequence**: the sampler accumulates `delta` per frame; FPS on the overlay is `1000 / frame_ms`,
computed from the same samples the statistics use, so the two can never disagree.

**Platform note**: `/proc/self/status` is Linux-only. That is acceptable — host and container are
both Linux — but it makes process memory an OS service, so under constitution I it is read in
`src/Game/Infrastructure` behind a Core-declared interface, never from Core directly. It is also a
file read, so it is sampled at the overlay's 4 Hz refresh rather than per frame.

---

## R12. Whole-session percentiles in bounded memory (design conflict resolved)

**The conflict**: FR-041 requires statistics "for the session" — average, p95, p99, worst. FR-045a
requires sampling to cost "no more than a fixed-size record per frame" and not to "grow without
bound over a long session". Storing every sample to compute an exact p99 grows without bound; a ring
buffer of the last N frames is bounded but silently changes the meaning of the statistics from
"this session" to "the last N frames", which is not what FR-041 says.

At the ~1,100 fps this container reached on a trivial scene, an hour-long session would be tens of
millions of samples. Even at a realistic 60 fps it is 216,000 per hour, growing forever.

**Decision**: accumulate a fixed-bucket histogram of frame times rather than a list of samples.

| Statistic | How |
|---|---|
| Average | Running sum and count. Exact, two numbers. |
| Worst frame | Running maximum. Exact, one number. |
| p95 / p99 | Read off the histogram's cumulative counts. |
| Sample count | The count already kept for the average, and what FR-044's 1000-sample confidence threshold tests. |

Buckets of 0.1 ms from 0 to ~100 ms plus a single overflow bucket is on the order of a thousand
counters — a few kilobytes, fixed for the life of the process regardless of session length.

**Accuracy tradeoff, stated plainly**: percentiles become accurate to the bucket width (0.1 ms)
rather than exact. For a statistic whose job is to answer "were frames consistently smooth, or were
there stalls", 0.1 ms resolution is far finer than the question needs — a stall worth investigating
is measured in whole milliseconds. Average and worst frame remain exact, and worst frame is the
figure that would suffer most from bucketing, which is why it is tracked separately.

**Where it lives**: entirely in `src/Core/Diagnostics` — a histogram is arithmetic with no engine
dependency, so percentile correctness, the confidence threshold, and overflow behavior are all
fast-tier xUnit tests (constitution II).

**Alternatives considered**: keep every sample (rejected — violates FR-045a outright); ring buffer of
recent samples (rejected — bounded, but quietly redefines FR-041's "session" statistics into
last-N-frames statistics, which is the kind of silent divergence between spec and behavior that
`/speckit-analyze` exists to catch); reservoir sampling (rejected — bounded and unbiased, but gives
approximate percentiles *and* is harder to explain and test than a histogram, with no accuracy gain
at the resolution that matters here).

---

## R13. What `dotnet format` actually enforces (spike-verified)

Constitution VI (added in v1.1.0) makes `dotnet format` a commit gate and a `verify.sh` stage, and
declares `.editorconfig` plus the .NET analyzers the sole definition of code style. That raised two
questions planning had never asked: what does the command check, and does the repository's existing
configuration make it check anything worth gating on.

**Spike**: built the solution, then dropped a deliberately malformed C# file into `src/Core` and ran
`dotnet format NewGame1.sln --verify-no-changes --no-restore` against the repository's current
four-line `.editorconfig` (`root = true`, `charset = utf-8`).

**Result 1 — whitespace is caught.** Bad indentation, stray spaces before `;` and inside `( )`, and
a brace on the wrong line each produced an `error WHITESPACE` line naming file, line, column, and the
edit that would fix it.

**Result 2 — and nothing else is.** A second file with correct whitespace but real style defects —
an unused `using`, `String` where `string` belongs, and `System` directives sorted after others —
**passed with exit code 0 and no output**. The IDE analyzers that catch these default to suggestion
or silent severity, and `dotnet format` fixes at `warn` and above.

**Result 3 — populating `.editorconfig` fixes it.** Adding explicit severities
(`dotnet_diagnostic.IDE0005.severity = warning`, `IDE0049`, `IDE0055`, `dotnet_sort_system_directives_first`)
and re-running the identical file produced exit code 2 and three findings: `IMPORTS: Fix imports
ordering`, `IDE0049: Name can be simplified`, `IDE0005: Using directive is unnecessary`.

**Decision**: expanding `.editorconfig` from four lines to a real C# style configuration is a
*required task of this feature*, not a tidy-up to do later. Wiring `dotnet format` into `verify.sh`
against the current file would add a stage that passes almost unconditionally — a green gate that
checks nothing, which is worse than no gate because it is mistaken for coverage. Constitution VI says
style is defined by `.editorconfig`; today that file defines a character set.

**Command form for the gate**: `dotnet format NewGame1.sln --verify-no-changes --no-restore`.

| Aspect | Finding |
|---|---|
| Subcommands | Bare `dotnet format` runs `whitespace`, `style` and `analyzers` together. The gate wants all three; do not name a subcommand. |
| Exit code | `0` clean, `2` when changes would be made. Unlike Godot (research R4), this exit code **is** trustworthy and is the right thing to branch on. |
| Output | One line per violation, `path(line,col): severity ID: message` — already in the shape an editor or agent can jump to. Reported to stdout. |
| `--verify-no-changes` | Reports without writing. The gate must use it: a verification script that silently reformats the tree turns a check into an edit. |
| `--no-restore` | Safe here because the build stage runs first and has already restored. Saves a redundant restore on every run. |
| Generated files | Skipped unless `--include-generated` is passed. Godot's `*.g.cs` and the SDK's `AssemblyInfo`/`GlobalUsings` files are therefore out of scope automatically — no exclude list needed. |
| Project loading | The spike ran across the whole solution, Godot's `NewGame1.csproj` included, with no load error. Formatting does not need the editor or an import pass. |

**Alternatives considered**: a pre-commit hook instead of a `verify.sh` stage (rejected — the
constitution names `verify.sh` as the gate, and a hook is per-clone state that does not survive a
fresh checkout; a hook may be added later as an *additional* early warning, but it cannot be the
enforcement point); `TreatWarningsAsErrors` to fail the build on style (rejected — it conflates style
with correctness, would fail builds mid-edit during development, and `Directory.Build.props`
deliberately sets it `false`); running only `dotnet format whitespace` for speed (rejected — it is
exactly the subset the spike proved insufficient).

---

## R14. The engine test tier: how GoDotTest runs (spike-verified)

The 2026-09-01 clarification deferred an engine test tier. That decision was reversed on 2026-09-02:
the developer wants both tiers standing — a fast engine-free one and a slower Godot one. This entry
records how the slow tier actually works, because its shape constrains files this plan already names.

`Chickensoft.GoDotTest` 2.0.46 is already referenced in `NewGame1.csproj`, and the skeleton already
carries `<Compile Remove="tests/Game.Tests/**" Condition="'$(Configuration)' == 'ExportRelease'" />`.
The tier was anticipated by the repository before this feature existed; what was missing is the
project and the entry point.

**The runner lives inside the game, not outside it.** Unlike `dotnet test`, GoDotTest does not host
the assembly — Godot does. The main scene's root script inspects the command line, and if tests were
requested it hands its own assembly to the runner instead of starting the game:

| Element | Contract |
|---|---|
| Entry point | The main scene's root script `_Ready()`, guarded by `#if DEBUG`. |
| Detection | `TestEnvironment.From(OS.GetCmdlineUserArgs())`, then `Environment.ShouldRunTests`. **Note: user args, not `GetCmdlineArgs()` as the package README shows** — see the argument-delivery finding below. |
| Invocation | `GoTest.RunTests(Assembly.GetExecutingAssembly(), this, Environment)`, deferred via `CallDeferred`. |
| Flags | `--run-tests` (optionally `=Suite` or `=Suite.Method`), `--quit-on-finish`. These are GoDotTest's, not Godot's. |
| Test assembly | The **game** assembly. `tests/Game.Tests` compiles into `NewGame1`, which is why the `ExportRelease` exclusion above exists. |
| Namespace | `Chickensoft.GoDotTest` (the README uses a bare `GoDotTest` in three of its six examples; that namespace does not exist in the shipped assembly). |

**Argument delivery — the package README's form does not fit this project.** GoDotTest's example
reads `OS.GetCmdlineArgs()`, which in Godot 4 returns engine arguments and **excludes** everything
after a `--` separator; those go to `OS.GetCmdlineUserArgs()` (research R5). Measured, with the
identical build:

| Invocation | `GetCmdlineArgs` | `GetCmdlineUserArgs` | Tests ran |
|---|---|---|---|
| `... Main.tscn -- --run-tests --quit-on-finish` reading `GetCmdlineArgs` | `[Main.tscn]` | `[--run-tests, --quit-on-finish]` | **No** — and the process hung until killed at 90 s |
| `... Main.tscn --run-tests --quit-on-finish` reading `GetCmdlineArgs` | `[Main.tscn, --run-tests, --quit-on-finish]` | `[]` | Yes |
| `... Main.tscn -- --run-tests --quit-on-finish` reading **`GetCmdlineUserArgs`** | `[Main.tscn]` | `[--run-tests, --quit-on-finish]` | **Yes** |

**Decision**: build the environment from `OS.GetCmdlineUserArgs()`. `TestEnvironment.From` accepts
either array, and using user args keeps one convention across the whole feature — the screenshot
harness already receives `-- --screenshot <name>` that way (research R5). Following the README
instead would leave `Main.cs` reading two different argument arrays for two different flags, which is
the kind of avoidable inconsistency that gets one of them wrong later.

The hang in the first row is worth noting on its own: when the flag does not arrive, the game simply
starts and runs forever. A CI or agent invocation with a malformed argument does not fail — it
blocks. Every automated Godot invocation needs an external timeout regardless of `--quit-on-finish`.

**Exit codes — R4 does not apply here, and the earlier provisional contract was wrong.** This entry
originally assumed research R4's warning ("Godot's exit code is not trustworthy") carried over.
Measured, it does not: GoDotTest sets the process exit code deliberately.

| Case | Exit code | Result line |
|---|---|---|
| 2 tests, both passing | `0` | `Test results: Passed: 2 \| Failed: 0 \| Skipped: 0` |
| 1 passing, 1 throwing | `1` | `Test results: Passed: 1 \| Failed: 1 \| Skipped: 0` |
| Scene path does not exist | `1` | none — engine error before the runner starts |
| `--run-tests=NoSuchSuite` | **`0`** | `Test results: Passed: 0 \| Failed: 0 \| Skipped: 0` |

**The last row is the trap, and it is the same shape as the `.editorconfig` finding in R13**: a run
that executes nothing exits 0 and reports success. Three realistic ways to reach it — a typo in a
suite filter, a build configuration that excluded `tests/Game.Tests/**` (which `NewGame1.csproj`
does under `ExportRelease`), or every test being renamed out of discovery — all produce a green
stage that verified nothing.

**Decision**: the `verify.sh` stage branches on the exit code **and** asserts that the reported
`Passed:` count is greater than zero. Neither check alone is sufficient: the exit code misses the
empty run, and the results line is absent entirely when the engine dies before the runner starts.

**Failure output is good enough to act on.** A failing test prints the suite, the test name, the
exception message, and a stack trace carrying the source file and line
(`at NewGame1.Tests.SpikeTest.PassesTrivially() in /workspace/tests/Game.Tests/SpikeTest.cs:line 13`).
The stage should surface it verbatim rather than summarising.

**Incidental**: Godot 4.7 writes a `*.cs.uid` file next to every C# script it imports. They appeared
for both spike scripts; `.gitignore` does not cover them. Followed up and resolved in R15 — they are
committed.

**Consequence for `src/Game/Main.cs`.** Constitution VI required that file to exist so
`scenes/Main.tscn` has a root script matching its name (see the plan's v1.1.0 delta). R14 gives it a
second job: it is also the test tier's entry point. GoDotTest's own documented example names the
class `Main`, so the two requirements coincide rather than conflict. `Main.cs` therefore branches
three ways — run tests, capture a screenshot, or be the placeholder scene — and that branching is the
one piece of it worth reviewing carefully, because every automated path in this feature goes through
it.

**Consequence for `verify.sh`.** The Godot test stage is a Godot *run*, not a `dotnet` invocation, so
everything research R1 and R10 established about running the engine here applies to it: `xvfb-run`
rather than `--headless`, `--rendering-method forward_plus --rendering-driver vulkan`,
`--audio-driver Dummy`. Research R4 does **not** carry over: GoDotTest sets the exit code
deliberately, as measured above. The stage branches on the exit code *and* asserts a non-zero
`Passed:` count, because an empty run exits 0.

**What this tier unlocks that Core tests cannot reach.** The manual host checklist in
`quickstart.md` Story 2 is the direct evidence that the deferral pushed real verification onto a
human: FR-011 (the backtick that opens the console must not land in the input field) and SC-007
(open within one displayed frame) are node and input behavior with no Core representation. Godot can
synthesise input events in-process, so these become automatable. The same applies to the overlay
toggle in Story 5.

**Decision**: stand the tier up in this feature. Both tiers exist from the start, `verify.sh` runs
both, and the constitution's stage list is satisfied literally rather than by argument. The spike
below confirms this works with the packages already referenced — no csproj change at all.

**Spike record (2026-09-02)**: run with the developer's permission to create `scenes/Main.tscn`. A
minimal `Main.cs`, a two-test `SpikeTest`, and a bare `Node2D` scene were created, exercised in the
configurations tabled above, then removed; the tree is back to its pre-spike state and the import
cache was refreshed. Confirmed end to end in this container: GoDotTest launches under `xvfb-run` on
Forward+ software Vulkan, discovers suites by reflection, runs them, lets a test add a node to the
live scene tree, reports per-test results, and quits on its own with `--quit-on-finish`.

The verified invocation:

```bash
xvfb-run -a godot --rendering-method forward_plus --rendering-driver vulkan \
  --audio-driver Dummy res://scenes/Main.tscn -- --run-tests --quit-on-finish
```

**Alternatives considered**: keeping the tier deferred and the host checklist (rejected — the
developer asked for both tiers, and constitution II already requires engine tests for behavior that
cannot move into Core, which FR-011 and SC-007 cannot); a separate test-only Godot project (rejected
— GoDotTest reflects over the *executing* assembly, so tests must compile into the game assembly, and
the skeleton's `ExportRelease` exclusion already implements that); running the tier only on the host
(rejected — it would leave `verify.sh` unable to run its own gate in the container, which is where
all agent work happens).

---

## R15. Godot's `*.cs.uid` files belong in version control (spike-verified)

Found incidentally while running the R14 spike: Godot 4.7 writes a `<script>.cs.uid` file next to
every C# script it imports — 19 bytes of plain text, e.g. `uid://udjt5rorljpy`. The repository's
`.gitignore` covers `.godot/`, `bin/`, `obj/` and `artifacts/`, none of which match, so the question
had to be answered rather than left to whichever pattern happened to catch them.

**The decisive test is a rename**, because surviving a move is the entire purpose of a UID. The same
script was renamed twice, once carrying its `.uid` and once without:

| Rename | Before | After | Outcome |
|---|---|---|---|
| `.uid` moved with the script (what git does when it is committed) | `uid://bu855qri0pifd` | `uid://bu855qri0pifd` | Identity **preserved** |
| `.uid` absent (what happens if it is ignored) | `uid://bu855qri0pifd` | `uid://ck7s7n3tfmjww` | Identity **lost** — a new one is minted |

**Decision**: commit `*.cs.uid` alongside the scripts. Leave `.gitignore` unchanged.

**Rationale**: Godot 4.4+ writes `uid://` references into `.tscn` and `.tres` files rather than plain
paths, which is what lets a scene keep pointing at a script after it moves. That indirection only
holds if the UID is stable, and the `.uid` file is the only thing that makes it stable. Ignore them
and every fresh clone, CI checkout, and file rename is free to mint a different identity for the same
script — silently, with the breakage appearing later as a scene that has lost its script.

**They are not build output.** Regenerating repeatedly at a fixed path produced a byte-identical
value every time, and a grep of `.godot/` for the UID string found nothing retaining it — so these
are source-adjacent metadata, not cache, and they do not belong beside `bin/` and `obj/`.

**Practical consequence for this feature**: `src/Game/Main.cs.uid` and one file per
`tests/Game.Tests/*.cs` are expected additions in the implementation diff, not noise to be stripped.
Every future C# script adds one.

**A false lead, recorded so it is not rediscovered**: an early probe appeared to show that *editing*
a script changes its UID, which would have been alarming. That result was contaminated — the probe
had accidentally created two files declaring the same class name, which perturbs Godot's global
script class registration. Re-tested cleanly, an edit at a fixed path leaves the UID untouched. Only
the path, and the presence or absence of the `.uid` file, matter.

---

## Remaining unknowns

No `NEEDS CLARIFICATION` markers remain in the Technical Context.

Nothing is left open. The R14 runtime spike was run on 2026-09-02 and its results are recorded
there, and the `*.cs.uid` question it raised is answered in R15. Every engine claim in this document
is spike-verified rather than inferred.

