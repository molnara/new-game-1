# Tasks: Developer Foundations

**Input**: Design documents from `/specs/001-dev-foundations/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Test tasks ARE included and are mandatory here — constitution II requires Core features to
ship with fast-tier tests written first, and FR-028c/SC-015 require both tiers to exist and to run
from `verify.sh`. Fast-tier (xUnit) tests are written before the Core code they cover; slow-tier
(GoDotTest) tests cover only what has no Core representation.

**Organization**: Tasks are grouped by user story so each can be implemented and validated
independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US5)
- Every task names its exact file path

## Path Conventions

Single Godot project with an engine-free core library, per plan.md *Project Structure*:
`src/Core/` (no `using Godot`), `src/Game/` (adapters), `tests/Core.Tests/` (xUnit fast tier),
`tests/Game.Tests/` (GoDotTest slow tier, compiled INTO the game assembly), `scripts/`, `scenes/`,
`tests/golden/`.

## Environment rules that bind almost every task

- Godot runs use `xvfb-run -a godot --rendering-method forward_plus --rendering-driver vulkan
  --audio-driver Dummy`, **never** `--headless` (research R1, R10). Capture is impossible under the
  dummy renderer.
- Every automated Godot invocation needs an external `timeout` — a malformed argument starts the
  game normally and blocks forever (research R14).
- Godot's exit code is not trustworthy except for the GoDotTest and `dotnet format` stages
  (research R4, R13, R14). Assert on artifacts and output.
- Godot writes a `<script>.cs.uid` beside every new `.cs` file. **Commit these**, do not gitignore
  them (research R15). Every task adding a Game-side `.cs` file adds its `.uid` to the same commit.
- New or moved asset files need `godot --headless --import` before a headless run can load them.
- Creating `scenes/Main.tscn` is cleared (developer confirmed 2026-09-02, editor not open on the
  host). CLAUDE.md's "ask before editing `.tscn` files" rule still governs later edits to an existing
  scene, since the host editor can be reopened at any time.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Make the repository's configuration match what the constitution and the research spikes
require, before any code is written against it.

- [X] T001 [P] Expand `.editorconfig` from its current four lines into the C# style configuration
      constitution VI makes authoritative: at minimum set `dotnet_diagnostic.IDE0005.severity`,
      `IDE0049`, `IDE0055` to `warning` or above and enable `dotnet_sort_system_directives_first`,
      plus indentation, brace and `var` rules for `[*.cs]` (research R13). This must land before the
      `verify.sh` style stage (T049) — against the current file the gate passes essentially
      everything.
- [X] T002 [P] Pin the renderer in `project.godot` under `[rendering]`: add
      `renderer/rendering_method="forward_plus"` (and the matching driver key) so host and container
      render the golden scene the same way (research R10). The key is currently absent, leaving the
      choice implicit.
- [X] T003 [P] Create the directory skeleton named in plan.md: `src/Core/Console/`,
      `src/Core/Diagnostics/`, `src/Core/Screenshots/`, `src/Game/Autoloads/`,
      `src/Game/Infrastructure/`, `scripts/`, `tests/Game.Tests/`, `tests/golden/`. Do **not** add a
      `.gdignore` to `tests/Game.Tests/` — that tier compiles into the game assembly (research R14).
      Do not add per-folder `README` files (constitution VI forbids them).
- [X] T004 Confirm the baseline in `/workspace`: `dotnet build NewGame1.sln` succeeds, and
      `dotnet format NewGame1.sln --verify-no-changes --no-restore` now reports against the expanded
      `.editorconfig` rather than passing vacuously — introduce a temporary unused `using` in a
      scratch file, confirm exit code 2 and an `IDE0005` line, then remove it (research R13,
      FR-028f). Depends on T001–T003.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The entry point and capture target every automated path in this feature runs through.
Nothing can start, be tested, or be photographed until these exist.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 Create `src/Game/Main.cs` — the root script class `Main`, named to match its scene file
      (constitution VI) and doubling as the GoDotTest entry point (research R14). In `_Ready()`,
      read `OS.GetCmdlineUserArgs()` — **not** `OS.GetCmdlineArgs()`, which does not see args after
      `--` and makes the process hang — and branch three ways: `--run-tests` hands the executing
      assembly to GoDotTest (guarded by `#if DEBUG`), `--screenshot` defers to the harness, otherwise
      run as the placeholder scene. This is the one file where a mistake breaks verification itself.
- [X] T006 Create `scenes/Main.tscn` — the placeholder capture target (FR-033): a root node running
      `src/Game/Main.cs`, a flat-colour background, and a `Label` identifying the build. No gameplay.
      PascalCase file name matching the root script class. (Developer confirmed 2026-09-02: creating
      scene files is fine, the host editor is not open. CLAUDE.md's "ask first" rule still applies to
      *editing* an existing `.tscn` while the editor may be running.)
- [X] T007 Set `application/run/main_scene="res://scenes/Main.tscn"` in `project.godot` so the game
      has a scene to run, and so `screenshot.sh` and the Godot test stage can name it. Depends on
      T006.
- [X] T008 Create `tests/Game.Tests/SmokeTest.cs` — one trivial GoDotTest suite proving the slow
      tier discovers and runs, then confirm end to end:
      `timeout 120 xvfb-run -a godot --rendering-method forward_plus --rendering-driver vulkan
      --audio-driver Dummy res://scenes/Main.tscn -- --run-tests --quit-on-finish` exits 0 and prints
      a non-zero `Passed:` count (FR-028c, FR-028d; research R14). No csproj change is needed — the
      GoDotTest reference and the `ExportRelease` compile exclusion are already in `NewGame1.csproj`.
      Depends on T005, T007.
- [X] T009 Confirm the placeholder runs unattended: `timeout 60 xvfb-run -a godot
      --rendering-method forward_plus --rendering-driver vulkan --audio-driver Dummy --quit-after 120`
      starts and exits without an error marker in its output. Depends on T007.

**Checkpoint**: The game runs, the scene renders, and the slow test tier executes. User stories can begin.

---

## Phase 3: User Story 1 - Read what the game did during a play session (Priority: P1) 🎯 MVP

**Goal**: Every run leaves a complete, timestamped, severity- and source-tagged session log in the
user data logs folder, readable after the fact, surviving an abrupt kill for everything that mattered.

**Independent Test**: Run the game, let it start and quit, then open the newest `session-*.log` in
`~/.local/share/godot/app_userdata/"new game 1"/logs/` and confirm it contains timestamped,
severity-tagged, source-tagged entries covering startup through shutdown.

### Tests for User Story 1 (write first, confirm failing) ⚠️

- [X] T010 [P] [US1] Write `tests/Core.Tests/Diagnostics/LogRetentionPolicyTests.cs`: keeps the
      newest `keep` files and returns the rest for deletion, orders by the timestamp embedded in the
      name, defaults `keep` to 10, and — the rule that matters — never returns Godot's own
      `godot.log` or any file not matching the session-log pattern (FR-006; research R6).

### Implementation for User Story 1

- [X] T011 [US1] Create `src/Core/Diagnostics/LogRetentionPolicy.cs` — a pure function over file
      names performing no I/O, returning the names to delete (FR-006). Makes T010 pass.
- [X] T012 [P] [US1] Create `src/Game/Infrastructure/LogPaths.cs` — resolve `user://logs/`
      (research R6), create it, and mint a per-session file name that is sortable by start time and
      unique per process so two concurrent runs never share a file (FR-001, FR-001b). If the folder
      cannot be created or written, report on stdout and return a "no file" result rather than
      throwing, so the game still starts (FR-001a).
- [X] T013 [P] [US1] Create `src/Game/Infrastructure/GodotSink.cs` — a Serilog sink bridging to
      `GD.Print`/`GD.PushError` so entries also appear on the terminal in a headless run (FR-007).
      This is the **only** file in the repository permitted to call those (constitution III).
- [X] T014 [US1] Create `src/Game/Infrastructure/WarnErrorFlushSink.cs` — a decorating sink forcing a
      disk flush via `IFlushableFileSink` when an event is `Warning` or above, so warnings and errors
      survive a kill (FR-005; research R7). If `IFlushableFileSink` proves awkward, fall back to
      `buffered: false`, which exceeds the requirement at the cost of a write per entry.
- [X] T015 [US1] Create `src/Game/Infrastructure/Logging.cs` — the static `Logging.For<T>()` entry
      point (FR-004) over a Serilog pipeline: file sink with `buffered: true` and a
      `flushToDiskInterval` of at most 1 second (FR-005), the `GodotSink` alongside it (FR-007), a
      configurable minimum level defaulting to Information (FR-003), and the four required fields per
      entry — time, severity, source system, message — one entry per line in plain text (FR-002).
      Prune via `LogRetentionPolicy` **at session start, before opening the new file**, and never
      touch a file another session holds open (FR-006).
      **Establish the configuration mechanism this feature uses throughout**: a `--log-level <level>`
      launch flag read from `OS.GetCmdlineUserArgs()`, the same convention `--screenshot` and
      `--run-tests` already use (research R5, R14), so debug is opt-in per run without a rebuild. The
      other two configurable defaults — console history (FR-019, T029) and the statistics interval
      (FR-046, T060) — reuse it rather than inventing their own. Depends on T011–T014.
- [X] T016 [US1] Wire logging into `src/Game/Main.cs`: initialise before anything else so startup is
      in the record, and call `Log.CloseAndFlush()` on `_ExitTree`/`NOTIFICATION_WM_CLOSE_REQUEST` so
      the final batch reaches disk (FR-005). Depends on T015.
- [X] T017 [US1] Add unhandled-failure logging in `src/Game/Infrastructure/Logging.cs`: subscribe to
      `AppDomain.CurrentDomain.UnhandledException`, record the failure with its detail, flush, and let
      it surface — handled errors are logged with their cause, unhandled ones are never swallowed
      (FR-008, US1 scenario 3).
- [X] T018 [US1] Validate US1 against [quickstart.md](./quickstart.md) Story 1: run the game and quit,
      confirm exactly one new session log covering startup through shutdown; run twice concurrently
      and confirm two distinct files (FR-001b); `kill -9` a run and confirm previously reported
      warnings and errors are all still on disk (SC-002); run 11+ sessions and confirm only 10 are
      retained and `godot.log` is untouched. Measure log growth over a timed run at the default
      severity and confirm the one-hour projection stays under 50 MB (SC-008) — if it does not, the
      default severity or the per-entry verbosity is wrong, and this is the cheapest moment to learn it.

**Checkpoint**: Sessions are reviewable after the fact. Every later story writes into this record.

---

## Phase 4: User Story 2 - Discover and run developer commands from inside the game (Priority: P2)

**Goal**: Backtick opens a console over the game; `help` lists every registered command; commands run,
report results, and are recalled from history; gameplay ignores typing while it is open.

**Independent Test**: Launch the game, press backtick, type `help`, confirm the command list appears,
run a listed command, confirm its output appears, press backtick and confirm gameplay input resumes.

**Depends on**: US1 (commands and results are recorded in the session log, FR-018).

### Tests for User Story 2 (write first, confirm failing) ⚠️

- [X] T019 [P] [US2] Write `tests/Core.Tests/Console/CommandLineParserTests.cs`: whitespace-separated
      tokens, runs of whitespace collapsed, double-quoted tokens containing spaces held together, and
      an unterminated quote treated as a parse failure that runs nothing (contracts/console-commands.md).
- [X] T020 [P] [US2] Write `tests/Core.Tests/Console/CommandRegistryTests.cs`: registration and
      case-insensitive resolution; a duplicate name is **rejected with the first registration
      retained** and never halts (FR-014); an unrecognized name yields a failure naming the input and
      pointing at `help` (FR-015); a handler that throws is caught at the registry boundary and
      converted to a failure carrying the exception detail (FR-016, constitution III); `All` is
      ordered by name. In the same task write
      `tests/Core.Tests/Console/CommandDescriptorTests.cs` covering the validation data-model.md
      states and T023 implements — construction throws on an empty name, an empty summary, or a name
      containing whitespace. `CommandDescriptor` is a Core feature with behavior, so constitution II
      requires it ship with tests of its own rather than inheriting the registry's.
- [X] T021 [P] [US2] Write `tests/Core.Tests/Console/HelpCommandTests.cs`: bare `help` lists every
      registered command as `name — summary` ordered by name and reflects whatever is registered at
      that moment; `help <command>` shows summary and usage; `help <unknown>` fails naming the unknown
      command and pointing back at bare `help` (FR-012).
- [X] T022 [P] [US2] Write `tests/Core.Tests/Diagnostics/BoundedLogTests.cs`: capacity is fixed at
      construction and must be > 0, entries read oldest-first, and appending at capacity drops the
      oldest (FR-019).

### Implementation for User Story 2

- [X] T023 [P] [US2] Create `src/Core/Console/CommandDescriptor.cs` — immutable record of `Name`,
      `Summary`, `Usage`, `Handler`; construction throws on an empty name, empty summary, or a name
      containing whitespace (FR-013, data-model.md). Makes the descriptor half of T020 pass.
- [X] T024 [P] [US2] Create `src/Core/Console/CommandArgs.cs` — `CommandName` plus `Positional`
      tokens with quotes already resolved. (Not named individually in plan.md's file list; it is the
      parser's output type from data-model.md.)
- [X] T025 [P] [US2] Create `src/Core/Console/CommandResult.cs` — `Succeeded`, `Message`,
      `FailureReason`, with `Ok(message)` / `Fail(reason)` constructors (FR-015, FR-016).
- [X] T026 [US2] Create `src/Core/Console/CommandLineParser.cs` — `Parse(string line)` producing
      `CommandArgs`. Makes T019 pass. Depends on T024.
- [X] T027 [US2] Create `src/Core/Console/CommandRegistry.cs` — `Register`, `TryResolve`, `All`, and
      `Execute(line)` chaining parse → resolve → invoke → `CommandResult`, catching handler
      exceptions and logging them through `ILogger<T>` (never `using Godot`). Registration is the
      whole integration for a system adding a command — no shared list, no edit to an unrelated
      system (FR-013, SC-006). Makes T020 pass. Depends on T023, T025, T026.
- [X] T028 [US2] Create `src/Core/Console/HelpCommand.cs` — `help` and `help <command>` registered
      against the registry (FR-012). Makes T021 pass. Depends on T027.
- [X] T029 [P] [US2] Create `src/Core/Diagnostics/BoundedLog.cs` — the ring buffer backing console
      output history, default capacity 1000, oldest discarded first, configurable through the launch
      flag T015 establishes rather than a mechanism of its own (FR-019). Makes T022 pass.
- [X] T030 [US2] Create `src/Game/Autoloads/DevConsole.cs` — a code-built `CanvasLayer` with
      `ProcessMode = Always`, a scrollable output history backed by `BoundedLog` and a single-line
      input field. Handle the toggle in `_UnhandledKeyInput` and call
      `GetViewport().SetInputAsHandled()` on the toggle event so the backtick never reaches the
      `LineEdit` (FR-011); grab focus while open so gameplay does not react to typing (FR-010);
      recall previously submitted commands with the history keys (FR-017). Delegate every decision to
      `CommandRegistry` — this file holds input handling and text rendering only (research R8, R9).
      Signals PascalCase, `[Export]` fields unprefixed (constitution VI). Depends on T027, T029.
- [X] T031 [US2] Register the console in `project.godot`: an `InputMap` action (default backtick,
      remappable — never a hard-coded key, FR-009 and the awkward-layout edge case) and `DevConsole`
      as an autoload so it is available from any scene without a restart. Depends on T030.
- [X] T032 [US2] Gate opening in `src/Game/Autoloads/DevConsole.cs` per FR-009a: the console is
      compiled into every build, but in an exported release build it opens only when a
      `--dev-console` launch flag explicitly enables it. Distinguish the build kind at runtime with
      Godot's feature tags — `OS.HasFeature("template_release")` for an exported release,
      `OS.HasFeature("editor")` for an editor run — never by inferring it from anything else. Log
      which mode applied at startup.
      **Confirm the feature-tag behavior before relying on it.** Unlike every other engine claim in
      this feature, this one was never spiked, and the container may lack export templates. If the
      tags cannot be verified here, say so and verify on the host rather than assuming. Depends on T030.
- [X] T033 [US2] Wire logging and the console together. In `src/Game/Autoloads/DevConsole.cs`,
      record every submitted command and the result it reported, with failures logged at Warning
      carrying their reason (FR-016, FR-018). Then register a `loglevel` command from
      `src/Game/Infrastructure/Logging.cs` — printing the current minimum severity with no argument
      and setting it with one — so the severity configured at launch in T015 is also adjustable
      mid-session (FR-003) and the logging system exposes a console command like every other system
      here (constitution III, plan.md Constitution Check). Add it to
      [contracts/console-commands.md](./contracts/console-commands.md). Depends on T030, T027, T015.
- [X] T034 [US2] Write `tests/Game.Tests/ConsoleInputTest.cs` (slow tier — this behavior has no Core
      representation, and FR-028e requires it be covered here rather than by a manual checklist):
      the toggle key opens and closes the console (FR-010); the toggle keystroke does
      **not** land in the console's input field (FR-011, the defect most likely to regress silently);
      the console is visible within a single displayed frame of the key press (SC-007, SC-016).
      Depends on T030, T031.
- [X] T035 [US2] Validate US2 against [quickstart.md](./quickstart.md) Story 2 — the parts a human
      must look at, the rest being covered by T034 — and confirm SC-006: adding a command touches only
      the registering system, no shared list.

**Checkpoint**: The running game is inspectable. US1 and US2 both work independently.

---

## Phase 5: User Story 3 - Capture a screenshot as evidence, with or without a display (Priority: P3)

**Goal**: `screenshot [name]` from the console and `scripts/screenshot.sh [name]` from the command
line both write `artifacts/<name>.png` and report the path — the latter with no display and no GPU.

**Independent Test**: With no display available, run `scripts/screenshot.sh main` and confirm a
non-empty PNG of the expected dimensions in `artifacts/` and a success exit. Separately run
`screenshot main` from the console and confirm the same file appears.

**Depends on**: US1 (paths and failures are logged, FR-022, FR-027), US2 (the console path, FR-021).
The headless path is independently valuable and does not need the console.

### Tests for User Story 3 (write first, confirm failing) ⚠️

- [X] T036 [P] [US3] Write `tests/Core.Tests/Screenshots/ScreenshotNameTests.cs`: empty or omitted
      falls back to the default name **`main`** — deliberately the same default `scripts/screenshot.sh`
      uses, so a no-argument capture from the console and from the command line write the one file the
      golden covers, rather than two files of which only one is checked; a name containing `/`, `\` or `..` is rejected so
      nothing can be written outside `artifacts/`; a name with characters illegal in a file name is
      rejected with a message naming the offending input; a name already ending `.png` is accepted
      without doubling the extension (FR-025).
- [X] T037 [P] [US3] Write `tests/Core.Tests/Screenshots/ScreenshotCommandTests.cs` against a fake
      `IScreenshotService`: success reports the full path (FR-022); an existing target is replaced and
      the message says so (FR-024); an invalid name is rejected **before** any capture is attempted;
      a service failure produces a failure result carrying the reason (FR-027). This is why the
      handler lives in Core rather than being a Game-side lambda (research R8).

### Implementation for User Story 3

- [X] T038 [P] [US3] Create `src/Core/Screenshots/IScreenshotService.cs` — the Core-declared engine
      service that keeps capture out of Core while letting the command live in the registry
      (constitution I; research R8).
- [X] T039 [US3] Create `src/Core/Screenshots/ScreenshotName.cs` — validation returning a value object
      wrapping a safe bare file name; Core never touches the filesystem and never joins paths. Makes
      T036 pass.
- [X] T040 [US3] Create `src/Core/Screenshots/ScreenshotCommand.cs` — the `screenshot [name]` handler
      registered against `CommandRegistry`, calling `IScreenshotService` and formatting the result per
      contracts/console-commands.md. Makes T037 pass. Depends on T038, T039, T027.
- [X] T041 [US3] Create `src/Game/Infrastructure/GodotScreenshotService.cs` implementing
      `IScreenshotService` from the viewport texture: capture the currently rendered view and write it
      as a PNG into `artifacts/` (FR-020), creating the folder if absent (FR-023); on any failure leave **no** empty or partial file behind — write to a temporary path and
      move into place (FR-027). A missing viewport texture (the `--headless` case) fails with that
      reason rather than producing a blank image (research R1). Depends on T038.
- [X] T042 [US3] Create `src/Game/Autoloads/ScreenshotHarness.cs` — inert unless `--screenshot <name>`
      arrives via `OS.GetCmdlineUserArgs()` (research R5). When it does, wait a **fixed, configurable
      number of fully rendered frames** — a frame count, never a wall-clock delay (FR-026) — then
      capture through `IScreenshotService` and quit with a status. If those frames cannot be produced
      at all, fail with a clear reason rather than writing a blank file. Depends on T041.
- [X] T043 [US3] Register `ScreenshotHarness` as an autoload in `project.godot` and register the
      `screenshot` command with the registry at startup from `src/Game/Main.cs`, wiring
      `GodotScreenshotService` as the `IScreenshotService` implementation. Depends on T040, T042.
- [X] T044 [US3] Create `scripts/screenshot.sh` per [contracts/cli-scripts.md](./contracts/cli-scripts.md):
      `set -euo pipefail`, runnable from any working directory, name defaults to `main`, runs under
      `xvfb-run -a` with `--rendering-method forward_plus --rendering-driver vulkan --audio-driver
      Dummy` and `-- --screenshot <name>`, wrapped in an external `timeout`. Assert success
      **positively** — the PNG exists and is non-empty — because Godot's exit code is not trustworthy
      (research R4). Print the written path as the last stdout line. Depends on T043.
- [X] T045 [US3] Write `tests/Game.Tests/CaptureTimingTest.cs` (slow tier): the harness waits its
      configured frame count before capturing — frame counting is the behavior under test and has no
      Core representation (FR-026). Depends on T042.
- [X] T046 [US3] Validate US3 against [quickstart.md](./quickstart.md) Story 3, including SC-003: ten
      consecutive `scripts/screenshot.sh` runs each produce a non-empty PNG at the expected dimensions
      showing the drawn scene rather than a blank frame. Read the image, do not just check its size.
      Then confirm FR-034's second half — replaceability: neither `scripts/screenshot.sh` nor
      `scripts/verify.sh` may name the placeholder scene, both reaching it through `project.godot`'s
      `main_scene`, so the first real scene can replace it without editing either script.

**Checkpoint**: Rendering is verifiable without a human looking at a window.

---

## Phase 6: User Story 4 - Confirm the project is healthy with one command (Priority: P4)

**Goal**: `scripts/verify.sh` runs build → code style → Core tests → Godot tests → screenshot →
golden compare, stops at the first failure, names the failing stage, and returns a status an
automated caller can branch on.

**Independent Test**: Run `scripts/verify.sh` on a healthy checkout and confirm every stage reports
PASS, the exit code is 0, and the named screenshot exists. Then break a test deliberately and confirm
it stops at that stage, names it, and exits non-zero.

**Depends on**: US1–US3 (it composes them) and Phase 2 (the Godot test stage).

- [X] T047 [P] [US4] Create `scripts/compare-golden.sh` per contracts/cli-scripts.md — the
      comparison half of FR-035, reporting how many pixels differ:
      `compare -metric AE` — **not** `magick compare`, which does not exist here (research R3).
      `compare` writes its metric to **stderr** and exits non-zero when images differ, so capture
      stderr and do not let `set -e` abort on that expected exit (research R2). Threshold defaults to
      **0**, an exact match (FR-036). A missing golden is a failure naming
      `scripts/update-golden.sh`, never an implicit pass. Print the differing pixel count and the
      threshold it was judged against.
- [X] T048 [US4] Create `scripts/update-golden.sh` per contracts/cli-scripts.md — capture via
      `screenshot.sh`, then copy over `tests/golden/<name>.png` (FR-035a). Print the reference path
      and whether it replaced an existing file; leave the existing reference untouched if the capture
      fails. It MUST NOT be a `verify.sh` stage: a gate that regenerates its own expectation cannot
      fail. Depends on T044.
- [X] T049 [US4] Generate and commit `tests/golden/main.png` — the committed reference FR-035
      requires for each capture target — by running `scripts/update-golden.sh main`
      in the container (never on the host — the host renders through a real GPU driver and will not
      match byte-for-byte, research R2). Read the image before committing it. Depends on T048.
- [X] T050 [US4] Create `scripts/verify.sh` with the six stages in order, stopping at the first
      failure (FR-028, FR-030): (1) `dotnet build`; (2) `dotnet format NewGame1.sln
      --verify-no-changes --no-restore` — bare, no subcommand, so whitespace, style and analyzers all
      run, and branching on its exit code, which **is** trustworthy — the machine check against a
      checked-in configuration that FR-028b mandates, reporting without modifying source (research
      R13); (3) `dotnet test`
      on the fast tier; (4) the GoDotTest run under `xvfb-run` with an external `timeout`;
      (5) `screenshot.sh`; (6) `compare-golden.sh`. One `PASS`/`FAIL` line per stage plus the
      screenshot path (FR-029); exit 0 only when every stage passed (FR-031); no interaction, no
      display (FR-032). Structure the stages so another can be inserted without reworking the script
      or its reporting (FR-028a). Depends on T044, T047, T049, T008.
- [X] T051 [US4] Add the anti-vacuity assertions to `scripts/verify.sh` (FR-028f) — the defect this
      feature hit twice: the Godot test stage must assert the reported `Passed:` count is greater
      than zero **as well as** branching on the exit code, because a run executing zero tests exits 0
      and prints `Passed: 0 | Failed: 0 | Skipped: 0` (research R14); the style stage must fail if
      `.editorconfig` enforces no rule at `warning` or above (research R13). Depends on T050.
- [X] T052 [US4] Validate the failure modes from [quickstart.md](./quickstart.md) Story 4: break a
      Core test → FAIL naming the Core stage, non-zero exit, later stages not run; break a
      `Game.Tests` assertion → FAIL naming the Godot stage with the suite, test, exception and source
      line surfaced verbatim (SC-015); run with `--run-tests=NoSuchSuite` → the stage must report FAIL
      despite the 0 exit; add an unused `using` and a `String`-for-`string` with correct indentation →
      FAIL at the style stage naming file, line and rule id (SC-014, FR-028b); **alter what the
      placeholder scene looks like** — change the label's text or the background colour — and confirm
      FAIL at the golden-compare stage reporting the differing pixel count, then revert (SC-010,
      FR-036); `git status --short` clean after a passing run, proving the check modified nothing.
      Confirm SC-005: the failing stage is identifiable from the output alone.
      The golden stage is the last one whose failure path is otherwise never exercised — the same
      shape as the two gates research R13 and R14 caught passing vacuously, which is why it is tested
      rather than assumed. Depends on T051.
- [X] T053 [US4] Measure a warm `scripts/verify.sh` run (assets imported, shaders compiled) and
      confirm it completes in under 5 minutes; record the cold-run figure separately (SC-004). Depends
      on T052.

**Checkpoint**: The project has a single, trustworthy quality gate. This is the constitution's
required pre-completion check for every task from here on.

---

## Phase 7: User Story 5 - See performance while playing, and read the numbers afterwards (Priority: P5)

**Goal**: An overlay toggled from the console shows frame time, FPS, draw calls and two memory
figures at 4 Hz; sampling runs from startup regardless; the session log carries interim and final
frame-time statistics records.

**Independent Test**: Launch the game, toggle the overlay with `perf`, confirm the measurements
appear and move, quit, and confirm the session log contains average, p95, p99 and worst frame as one
identifiable record.

**Depends on**: US1 (statistics are written to the session log), US2 (the overlay and both commands
are reachable only through the console, FR-038). This is the story to drop first if the feature needs
trimming — nothing else depends on it.

> **Measurement validity**: this container renders through Mesa's software rasterizers. No task below
> may assert an absolute frame-time, FPS or draw-call budget from a container run — such a test would
> pass or fail on how busy the build machine is (plan.md, *Measurement validity*). Relative
> comparisons within one environment, which is what T064 is, remain valid.

### Tests for User Story 5 (write first, confirm failing) ⚠️

- [X] T054 [P] [US5] Write `tests/Core.Tests/Diagnostics/FrameTimeHistogramTests.cs`: `Add` is
      constant-time and allocation-free; average and worst frame are **exact** while p95/p99 are
      accurate to bucket width (research R12); a sample above the top bucket lands in the overflow
      bucket and still updates `WorstMs` exactly, so a catastrophic stall is never lost; negative and
      NaN samples are rejected rather than recorded; memory use is fixed regardless of sample count
      (FR-045a).
- [X] T055 [P] [US5] Write `tests/Core.Tests/Diagnostics/FrameTimeStatisticsTests.cs`: a snapshot of
      an empty histogram is constructible, marked low-confidence, and reports percentiles as **absent
      rather than 0** — no divide-by-zero and no misleading value (the overlay-before-first-frame edge
      case); `IsLowConfidence` is true below 1000 samples (FR-044); `SampleCount` is carried on the
      record (FR-042); `Kind` distinguishes `Interim` from `Final` (FR-046a).

### Implementation for User Story 5

- [X] T056 [P] [US5] Create `src/Core/Diagnostics/FrameTimeStatistics.cs` — the immutable snapshot:
      average, p95, p99, worst, sample count, low-confidence flag, and interim/final kind. Makes T055
      pass.
- [X] T057 [US5] Create `src/Core/Diagnostics/FrameTimeHistogram.cs` — fixed 0.1 ms buckets from 0 to
      ~100 ms plus one overflow bucket, with running count, sum and maximum; `Snapshot()` produces a
      `FrameTimeStatistics` without mutating or resetting (research R12). Makes T054 pass. Depends on
      T056.
- [X] T058 [P] [US5] Create `src/Core/Diagnostics/IPerformanceCounters.cs` — `DrawCalls`,
      `VideoMemoryBytes`, `ProcessMemoryBytes`, each able to report **explicitly unavailable** rather
      than zero (FR-041a). Frame time and FPS are deliberately **not** on this interface: frame time
      comes from the engine's per-frame delta and FPS is derived as `1000 / frameMs`, because the
      engine's own FPS counter reads a frozen `1.0` in short runs (research R11).
- [X] T059 [US5] Create `src/Game/Infrastructure/GodotPerformanceCounters.cs` implementing
      `IPerformanceCounters`: `RENDER_TOTAL_DRAW_CALLS_IN_FRAME`, `RENDER_VIDEO_MEM_USED`, and process
      memory from `/proc/self/status` `VmRSS` — **not** `OS.GetMemoryInfo()`, which reports system
      RAM, and **not** `MEMORY_STATIC`, which under-reported by 11x in the spike (FR-047; research
      R11). `VmRSS` is a file read, so poll it at the overlay's 4 Hz refresh, never per frame. Depends
      on T058.
- [X] T060 [US5] Create `src/Game/Autoloads/PerfMonitor.cs` (sampling half) — accumulate each frame's
      `delta` into a `FrameTimeHistogram` from startup in every build, whether or not the overlay is
      visible (FR-045), and write statistics to the session log on an interval defaulting to 30
      seconds — configurable through the launch flag T015 establishes — plus one final record at
      shutdown (FR-046). Interim records must be visibly
      distinguishable from the final one (FR-046a) and must reach disk as written rather than waiting
      in a batch, or an abrupt kill discards exactly what FR-046 exists to preserve (FR-046b). Make
      each record carry the session's average, 95th percentile, 99th percentile and worst single frame
      (FR-041) as one identifiable, searchable line stating its sample count (FR-042, SC-012).
      Depends on T057, T015.
- [X] T061 [US5] Add the overlay to `src/Game/Autoloads/PerfMonitor.cs` — a `CanvasLayer` refreshing
      about 4 times per second, showing each interval's **average** alongside that interval's **worst
      single frame** so a brief stall is not smoothed away (FR-039, FR-039a), displaying frame time,
      FPS, draw calls, process memory and video memory with the two memory figures separately
      labelled (FR-037, FR-047), and rendering unavailable metrics as explicitly unavailable
      (FR-041a). Values stay legible over bright, dark or busy scene content (FR-039b). Off by default
      (FR-038). Depends on T059, T060.
- [ ] T062 [US5] Register `perf` and `perfstats` with `CommandRegistry` from
      `src/Game/Autoloads/PerfMonitor.cs` per contracts/console-commands.md: `perf` toggles display
      only and says which state it is now in, with sampling unaffected either way (FR-038);
      `perfstats` prints the current statistics with the sample count and without needing the overlay
      visible, saying so explicitly when below 1000 samples and reporting "no samples yet" before the
      first frame (FR-043, FR-044). Depends on T061, T027.
- [ ] T063 [US5] Register `PerfMonitor` as an autoload in `project.godot`. Depends on T061.
- [ ] T064 [US5] Write `tests/Game.Tests/OverlayToggleTest.cs` (slow tier): the overlay toggles on and
      off across the node lifecycle, and sampling continues regardless of its visibility (FR-038,
      FR-045). Depends on T061, T062.
- [ ] T065 [US5] Establish the overlay's own cost per FR-040/SC-013: compare mean frame time over at
      least **1000 frames** with `src/Game/Autoloads/PerfMonitor.cs`'s overlay visible against the
      same count with it hidden, on `scenes/Main.tscn` under identical run conditions, and confirm the difference is under 1 ms. A
      single-frame or short-sample comparison is not sufficient given software-rendering variance.
      This is a relative comparison within one environment, so it is valid here — record the method
      and the numbers in the task's commit message. Depends on T061.
- [ ] T066 [US5] Validate US5 against [quickstart.md](./quickstart.md) Story 5: a container run that
      never opens the console still writes statistics (FR-045); `kill -9` mid-run leaves the most
      recent interim record on disk (FR-046b); a run of well under 1000 frames still writes a record
      marked low-confidence (FR-044); a headless run records unavailable metrics as unavailable and
      crashes nothing (FR-041a). The live overlay checks need a display and are run on the host,
      including SC-011: the four measurements are readable within 5 seconds of opening the console,
      with no restart.

**Checkpoint**: All five user stories are independently functional.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T067 [P] Extend `README.md` with the "running the project" section constitution VI requires:
      how to run the game, capture a screenshot, run `scripts/verify.sh`, and where session logs land.
      The container workflow already there stays. Do not add per-folder `README` files or ADRs.
- [ ] T068 [P] Add XML doc comments to the public `src/Core` surface only — `CommandRegistry`,
      `FrameTimeHistogram`, `IScreenshotService`, `IPerformanceCounters` — and to nothing on the Game
      side (constitution VI).
- [ ] T069 Confirm Core purity: `grep -rn "using Godot" src/Core/` returns nothing (constitution I).
      If it ever will not, the missing piece is a Core-declared interface implemented in
      `src/Game/Infrastructure`.
- [ ] T070 Confirm every `*.cs.uid` Godot generated beside a new script is staged rather than dropped
      — `git status --short` should show one per Game-side `.cs` file and none ignored (research R15).
- [ ] T071 Run [quickstart.md](./quickstart.md) end to end, all five stories, and read the screenshot
      it produces. Time a cold walkthrough as part of it — open the console, run `help`, produce a
      screenshot, using nothing but `help` and no source or docs — and confirm it takes under 2
      minutes (SC-001). Then run `scripts/verify.sh` and confirm a clean pass — the constitution's
      pre-completion gate for the feature as a whole.

- [ ] T072 Audit SC-009 across the feature: walk every failure path named in the spec's acceptance
      scenarios — unwritable log destination (FR-001a), duplicate command registration (FR-014),
      unknown command (FR-015), failing command (FR-016), rejected screenshot name (FR-025), failed
      capture (FR-027), each failing verification stage (FR-030), unavailable performance metrics
      (FR-041a) — and confirm each produces a message a developer can actually read in the console,
      the terminal, or the session log. No developer-facing failure in these systems may be silent.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup. **Blocks every user story** — nothing runs, is
  tested, or is photographed without `Main.cs` and `Main.tscn`.
- **US1 (Phase 3)**: depends on Foundational only. The MVP.
- **US2 (Phase 4)**: depends on Foundational; needs US1 for FR-018 (commands and results in the log).
- **US3 (Phase 5)**: depends on Foundational; needs US2 for the console path (FR-021) and US1 for
  logged paths and failures. Its headless path is independently valuable.
- **US4 (Phase 6)**: composes US1–US3 and the Phase 2 test tier. Genuinely last — it has nothing to
  run until they exist.
- **US5 (Phase 7)**: depends on Foundational, US1 (log records) and US2 (the console is the only way
  to reach it, FR-038). Nothing depends on US5 — it is the safe story to defer.
- **Polish (Phase 8)**: after all desired stories.

These are real dependencies, not scheduling preference: this feature builds the substrate the later
stories stand on, so the stories are sequential rather than parallel. Each remains independently
*testable* at its checkpoint.

### Within Each User Story

- Fast-tier tests are written and failing before the Core code that satisfies them (constitution II).
- Core types before Game adapters; adapters before autoload registration; registration before
  slow-tier tests.
- Before adding any slow-tier test, ask constitution II's question first: could this be a Core test
  instead? The four slow-tier tests here (T008, T034, T045, T064) are the ones that cannot.

### Parallel Opportunities

- Setup: T001, T002, T003 touch different files.
- US1: T010 (test) then T012 and T013 together.
- US2: all four test files T019–T022 in parallel; then T023, T024, T025 and T029 in parallel.
- US3: T036 and T037 in parallel; T038 alongside them.
- US5: T054 and T055 in parallel; T056 and T058 in parallel.
- Polish: T067 and T068.

## Parallel Example: User Story 2

```bash
# Write all four fast-tier test files together, confirm they fail:
Task: "tests/Core.Tests/Console/CommandLineParserTests.cs"
Task: "tests/Core.Tests/Console/CommandRegistryTests.cs"
Task: "tests/Core.Tests/Console/HelpCommandTests.cs"
Task: "tests/Core.Tests/Diagnostics/BoundedLogTests.cs"

# Then the independent Core types together:
Task: "src/Core/Console/CommandDescriptor.cs"
Task: "src/Core/Console/CommandArgs.cs"
Task: "src/Core/Console/CommandResult.cs"
Task: "src/Core/Diagnostics/BoundedLog.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 Setup — `.editorconfig` and the renderer pin, both of which everything downstream assumes.
2. Phase 2 Foundational — `Main.cs`, `Main.tscn`, the slow-tier bootstrap.
3. Phase 3 US1 — session logging.
4. **STOP and VALIDATE**: run the game, quit, read the log. Sessions are now reviewable.

`verify.sh` does not exist yet at that point, so the constitution's gate is satisfied by running its
stages by hand until T050 lands. Say so explicitly rather than skipping the check.

### Incremental Delivery

Setup + Foundational → US1 (logs) → US2 (console) → US3 (screenshots) → US4 (the gate) → US5
(profiling). Each story adds evidence the next one can use, and each is demonstrable at its
checkpoint.

### Trimming

If the feature needs to shrink, US5 is the intended cut — the constitution does not require it and
nothing else depends on it. US4 cannot be cut: constitution "Development Workflow & Quality Gates"
names `scripts/verify.sh` as the gate for every task.

---

## Notes

- Commit after each completed phase with a conventional message, including the tasks.md checkbox
  updates. Never commit failing tests (CLAUDE.md).
- `scripts/verify.sh` must pass before any task is reported complete, once it exists. A task with a
  failing or unrun gate is not complete (constitution). If verification fails for reasons outside the
  change, say so explicitly rather than skipping it.
- Two gates in this feature can ship green while checking nothing — the style stage against a thin
  `.editorconfig` (research R13) and the Godot test stage on a zero-test run (research R14). T051
  exists because both were found by spikes rather than by review.
- `artifacts/` is gitignored and disposable; `tests/golden/` is committed. Never swap them.
