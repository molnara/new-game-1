# Implementation Plan: Developer Foundations

**Branch**: `001-dev-foundations` | **Date**: 2026-09-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-dev-foundations/spec.md`

## Summary

Deliver the four developer-facing systems the constitution already assumes exist: structured
session logging, an in-game dev console, a screenshot harness that works without a display, and a
one-command verification gate.

The technical approach keeps every decision-bearing piece engine-free. `CommandRegistry` and the
command-line parser are plain C# in `src/Core/Console`, unit-tested with xUnit. Logging is consumed
through `Microsoft.Extensions.Logging.Abstractions` in Core and configured with Serilog (file sink
plus a Godot sink) in `src/Game/Infrastructure`. The console and screenshot harness are code-built
autoloads in `src/Game/Autoloads` — the console a `CanvasLayer`, the harness activated by a user
command-line argument. Verification is a shell script chaining build, Core tests, and a captured
screenshot compared against a golden image with ImageMagick.

A spike run during planning proved the riskiest assumption in the feature and corrected it: capture
does **not** work under `--headless`, only under `xvfb-run` with the OpenGL3 driver. See
[research.md](./research.md) for the evidence.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`), Godot 4.7.2 (.NET / Mono build)

**Primary Dependencies**: `Microsoft.Extensions.Logging.Abstractions` 10.0.11 (Core-facing
abstraction), Serilog 4.4.0 + `Serilog.Extensions.Logging` 10.0.0 + `Serilog.Sinks.File` 7.0.0
(Game-side implementation), xUnit 2.9.3 + Shouldly 4.3.0 (tests). All are already referenced in
`NewGame1.csproj` / `NewGame1.Core.csproj`; this feature adds no new NuGet package.

**System tooling**: ImageMagick 6.9.12 (`compare`, `convert`, `identify` — note: the IM7 `magick`
wrapper is **not** installed), `xvfb-run`, Mesa llvmpipe (OpenGL 4.5 software).

**Storage**: Plain-text rolling log files under the Godot user data directory
(`~/.local/share/godot/app_userdata/new game 1/logs/`, confirmed by spike). PNG artifacts under
`artifacts/` (gitignored). Golden reference PNGs under `tests/golden/` (committed).

**Testing**: xUnit against `src/Core` (`dotnet test`). No engine test tier in this feature — the
clarified decision defers `tests/Game.Tests` until something genuinely needs it. Engine-side
behavior is evidenced by the screenshot harness and `verify.sh` instead.

**Target Platform**: Linux dev container (no GPU, no display, software rendering only); Windows
host runs the same engine and SDK versions.

**Project Type**: Single Godot game project with an engine-free core library.

**Performance Goals**: 60 fps unaffected by logging — routine log entries must not cause a
per-frame disk write (SC-007: console opens within one frame).

**Constraints**: Core must not reference Godot (constitution I). Warnings and errors must reach
disk immediately; debug/info may batch (clarified, FR-005). Capture must wait a fixed frame count,
never a wall-clock delay (clarified, FR-026). Verification must run unattended with no display
(FR-032).

**Scale/Scope**: Single developer, single-player game. ~15 new source files, ~3 shell scripts, one
placeholder scene. Log retention 10 sessions; console history bounded.

## Constitution Check

*GATE: evaluated before Phase 0 and re-evaluated after Phase 1 design.*

| Principle | Gate | Verdict |
|---|---|---|
| **I. Core/Adapter Separation** | No `using Godot` in `src/Core`; rules live in Core; engine services defined as Core interfaces implemented in `src/Game/Infrastructure` | **PASS** — `CommandRegistry`, the input parser, and the bounded history are pure C#. Core logs through `ILogger<T>` abstractions only. `IScreenshotService` is declared in Core and implemented in Game so the `screenshot` command can live in the registry without dragging the engine into Core. |
| **II. Test-First, Two Tiers** | Core features ship with xUnit tests written first; the slow tier is the exception | **PASS** — registry, parser, help formatting, ring buffer and log-path policy are all Core-testable and get tests first. Engine tier deliberately absent (clarified decision, FR-028a). |
| **III. Observability by Default** | Everything logs via `Logging.For<T>()`; `GD.Print` only in `src/Game/Infrastructure`; no swallowed errors | **PASS** — this feature *builds* that mechanism. `GD.Print`/`GD.PushError` appear only inside `GodotSink.cs` in Infrastructure. Every system added here registers at least one console command. |
| **IV. Visual Verification** | Rendering changes verified by a screenshot that is actually read; golden images updated intentionally | **PASS** — the feature delivers the harness, and the placeholder scene gets the first golden image in `tests/golden/main.png`. |
| **V. Simplicity** | No new framework/addon without written justification | **PASS with justification** — see Complexity Tracking. Serilog is a third-party logging library and requires an entry; it is already referenced in the repo skeleton. No DI container, no ECS, no Godot addon. |

**Post-Phase-1 re-evaluation**: unchanged, all gates still PASS. The design added `IScreenshotService`
as a Core-declared interface specifically to avoid an engine dependency leaking into Core, which
strengthens gate I rather than straining it.

### Scope deviation requiring the developer's attention

The feature description for this plan asks for **golden screenshot comparison with ImageMagick**.
The spec's Out of Scope section explicitly excludes it ("Automatic comparison of screenshots against
golden reference images, and any diffing or approval workflow around them").

Constitution IV independently *requires* golden reference images, so the plan follows the
constitution and the newer instruction and brings golden comparison in. `spec.md` has been amended
in the same commit to match — the Out of Scope bullet is replaced and FR-035/FR-036 added — so the
two documents do not contradict each other going into `/speckit-tasks`. This is flagged rather than
silently absorbed because it grew the feature.

## Project Structure

### Documentation (this feature)

```text
specs/001-dev-foundations/
├── plan.md              # This file
├── research.md          # Phase 0 output — decisions + spike evidence
├── data-model.md        # Phase 1 output — entities and state
├── quickstart.md        # Phase 1 output — how to prove it works
├── contracts/
│   ├── console-commands.md   # Command surface + result contract
│   └── cli-scripts.md        # screenshot.sh / verify.sh / compare-golden.sh contracts
├── checklists/
│   └── requirements.md
└── tasks.md             # NOT created by /speckit-plan
```

### Source Code (repository root)

```text
src/Core/                          # engine-free; no `using Godot`
├── Console/
│   ├── CommandRegistry.cs         # registration, lookup, duplicate detection (FR-013, FR-014)
│   ├── CommandDescriptor.cs       # name, summary, usage, handler
│   ├── CommandResult.cs           # success/failure + message (FR-015, FR-016)
│   ├── CommandLineParser.cs       # tokenizer: quotes, whitespace
│   └── HelpCommand.cs             # `help` and `help <cmd>` (FR-012)
├── Diagnostics/
│   ├── BoundedLog.cs              # ring buffer for console history (FR-019)
│   └── LogRetentionPolicy.cs      # which session logs to prune (FR-006)
└── Screenshots/
    ├── IScreenshotService.cs      # Core-declared engine service
    └── ScreenshotName.cs          # name validation (FR-025)

src/Game/
├── Autoloads/
│   ├── DevConsole.cs              # code-built CanvasLayer (FR-009..FR-019)
│   └── ScreenshotHarness.cs       # cmdline-activated capture (FR-020..FR-027)
└── Infrastructure/
    ├── Logging.cs                 # Logging.For<T>() entry point (FR-004)
    ├── GodotSink.cs               # ONLY place GD.Print/GD.PushError is allowed
    ├── WarnErrorFlushSink.cs      # immediate flush for Warning+ (FR-005)
    ├── LogPaths.cs                # user:// log dir resolution
    └── GodotScreenshotService.cs  # IScreenshotService implementation

scenes/
└── Main.tscn                      # placeholder capture target (FR-033, FR-034)

scripts/
├── screenshot.sh                  # xvfb-run wrapper around the harness
├── compare-golden.sh              # ImageMagick AE comparison under threshold
└── verify.sh                      # build -> Core tests -> screenshot -> golden compare

tests/
├── Core.Tests/Console/            # xUnit: registry, parser, help, validation
├── Core.Tests/Diagnostics/        # xUnit: ring buffer, retention policy
└── golden/main.png                # committed golden reference (artifacts/ is gitignored)
```

**Structure Decision**: The existing skeleton already separates `src/Core` (engine-free library,
excluded from the Godot compile via `<Compile Remove="src/Core/**" />`) from the Godot project at the
repository root. This feature fills in that structure rather than changing it, adding
`src/Game/Autoloads` and `src/Game/Infrastructure` as the adapter layer the constitution names, and
`scripts/` for the verification tooling it mandates. Golden images live in `tests/golden/` because
`artifacts/` is gitignored and goldens must be committed.

## Complexity Tracking

> Constitution V requires written justification for any third-party library or framework.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| **Serilog** (+ `Serilog.Extensions.Logging`, `Serilog.Sinks.File`) as a third-party logging library | FR-005 needs per-level flush control (warnings/errors immediately, debug/info batched), FR-006 needs bounded session-file retention, and FR-001 needs one timestamped file per run. Serilog provides all three as configuration, and it is already referenced in the repo skeleton. | Hand-rolling a `StreamWriter` logger was considered and rejected: getting rolling-file retention, per-level flush policy, and safe concurrent writes correct is materially more code than configuring an existing sink, and it is code with no consumer outside this project. `GD.print` alone was rejected because it cannot satisfy FR-002 (structured fields) or FR-005 (flush policy), and constitution III forbids it outside Infrastructure. |
| **`Microsoft.Extensions.Logging.Abstractions`** referenced by Core | Constitution I forbids Godot in Core, but Core code must still log. An abstraction package with no engine ties is the mechanism that makes that possible. | A hand-written `ILog` interface was rejected: it would be a near-identical reimplementation of a standard interface, and would force an adapter layer between Core's logger and Serilog that the standard abstraction already provides for free. |
| **ImageMagick** as a system dependency for golden comparison | Constitution IV requires golden reference screenshots; comparing PNGs pixel-wise needs an image differ. ImageMagick is already installed in the container. | Writing a C# pixel comparator was rejected as unnecessary: the comparison happens in shell scripts outside the game process, where invoking an installed tool is one line. It adds no NuGet dependency and no code to maintain. |

Note: ImageMagick 6 is installed, which provides `compare`/`convert`/`identify` as separate binaries
and **not** the IM7 `magick` wrapper. Scripts must call `compare` directly.
