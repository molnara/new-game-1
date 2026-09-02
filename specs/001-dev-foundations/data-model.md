# Phase 1 Data Model: Developer Foundations

**Date**: 2026-09-02 | **Plan**: [plan.md](./plan.md)

Entities from [spec.md](./spec.md) mapped to concrete types. Everything in this document lives in
`src/Core` unless marked otherwise — the engine-side types hold no state worth modelling.

---

## CommandDescriptor

One registered developer command. Immutable record.

| Field | Type | Rules |
|---|---|---|
| `Name` | `string` | Required. Lowercase, no whitespace. Unique within a registry (FR-014). |
| `Summary` | `string` | Required, non-empty. One line, shown by bare `help` (FR-012). |
| `Usage` | `string` | Required. Argument shape shown by `help <name>` (FR-012). |
| `Handler` | `Func<CommandArgs, CommandResult>` | Required. Never returns null. |

**Validation**: constructing with an empty name, empty summary, or a name containing whitespace
throws. Registration is the only path that enforces uniqueness.

---

## CommandArgs

Parsed tokens for one invocation.

| Field | Type | Notes |
|---|---|---|
| `CommandName` | `string` | First token. |
| `Positional` | `IReadOnlyList<string>` | Remaining tokens, quotes already resolved. |

Produced by `CommandLineParser.Parse(string line)`. The parser handles double-quoted tokens
containing spaces and collapses runs of whitespace. An unterminated quote is a parse failure, not a
silently accepted token.

---

## CommandResult

Outcome of running a command. Never an exception at the console boundary (FR-016).

| Field | Type | Notes |
|---|---|---|
| `Succeeded` | `bool` | |
| `Message` | `string` | Shown in the console; empty allowed on success. |
| `FailureReason` | `string?` | Set only when `Succeeded` is false. Logged at Warning (FR-016). |

**Construction**: `CommandResult.Ok(message)` / `CommandResult.Fail(reason)`. A handler that throws
is caught by the registry and converted to a `Fail` carrying the exception message — the exception
is logged with its detail, never swallowed silently (constitution III).

---

## CommandRegistry

The set of registered commands; what `help` enumerates and the console resolves against.

| Member | Behavior |
|---|---|
| `Register(CommandDescriptor)` | Adds. Throws `DuplicateCommandException` if the name is taken (FR-014). |
| `TryResolve(string name, out CommandDescriptor)` | Case-insensitive lookup. |
| `All` | Commands ordered by name, for `help` output. |
| `Execute(string line)` | Parse → resolve → invoke → `CommandResult`. Unknown name yields a failure naming the input and pointing at `help` (FR-015). |

**State transitions**: registration is append-only within a session; commands are never unregistered.
Registration happens during startup as each system initialises, so `help` reflects whatever is
registered at the moment it runs (the "console opened before systems are ready" edge case).

---

## BoundedLog

Ring buffer backing console output history (FR-019).

| Field | Type | Rules |
|---|---|---|
| `Capacity` | `int` | Required, > 0. Fixed at construction. |
| `Entries` | `IReadOnlyList<string>` | Oldest first. Never exceeds `Capacity`. |

**State transition**: appending at capacity drops the oldest entry. This is the only mutation.

---

## LogRetentionPolicy

Decides which session log files to delete (FR-006). Pure function over file names — it performs no
I/O, which is what makes it fast-tier testable.

| Input | Type |
|---|---|
| `existing` | `IReadOnlyList<string>` (session log file names) |
| `keep` | `int` (default 10) |

**Returns**: the file names to delete — the oldest beyond `keep`, ordered by the timestamp embedded
in the name.

**Critical rule**: only files matching this project's own session-log naming pattern are candidates.
Godot writes its own `godot.log` into the same directory (see research R6) and it must never be
pruned by this policy.

---

## FrameTimeHistogram

Bounded-memory accumulator for a whole session's frame times (FR-041 + FR-045a; see research R12 for
why a histogram rather than a sample list).

| Field | Type | Rules |
|---|---|---|
| `BucketWidthMs` | `double` | Fixed at construction, default 0.1. > 0. |
| `BucketCount` | `int` | Fixed. Covers 0 to ~100 ms; one extra overflow bucket above that. |
| `Count` | `long` | Total samples recorded. |
| `SumMs` | `double` | Running sum, for an exact average. |
| `WorstMs` | `double` | Running maximum, tracked exactly rather than bucketed. |

| Member | Behavior |
|---|---|
| `Add(double frameMs)` | Increments the matching bucket, count, sum, and max. Constant time, no allocation. |
| `Snapshot()` | Produces a `FrameTimeStatistics` without mutating or resetting. |

**Rules**: a negative or NaN sample is rejected rather than recorded. Samples above the top bucket
land in the overflow bucket and still count toward `WorstMs` exactly, so a catastrophic stall is
never lost to bucketing. Memory use is fixed for the life of the process regardless of session
length — that is the entire point of the type.

---

## FrameTimeStatistics

An immutable snapshot of a histogram. What gets written to the log and shown by `perfstats`.

| Field | Type | Notes |
|---|---|---|
| `AverageMs` | `double` | Exact (sum / count). |
| `P95Ms` | `double` | Read from cumulative bucket counts; accurate to bucket width. |
| `P99Ms` | `double` | Same. |
| `WorstMs` | `double` | Exact. |
| `SampleCount` | `long` | What the confidence rule tests. |
| `IsLowConfidence` | `bool` | True when `SampleCount < 1000` (FR-044). |
| `Kind` | `Interim` \| `Final` | Distinguishes a mid-session snapshot from the end-of-session record (FR-046a). |

**Validation rules**: with zero samples the statistics are still constructible and marked
low-confidence — an empty session must not throw or divide by zero (the "overlay enabled before the
first frame" edge case). Percentiles of an empty histogram are reported as absent, not as 0.

---

## IPerformanceCounters (Core-declared engine service)

The seam that keeps Core engine-free while still reading engine and OS numbers (constitution I).

| Member | Returns | Source (research R11) |
|---|---|---|
| `DrawCalls` | `long` | `Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME` |
| `VideoMemoryBytes` | `long` | `Performance.RENDER_VIDEO_MEM_USED` |
| `ProcessMemoryBytes` | `long` | `/proc/self/status` `VmRSS` — **not** `OS.get_memory_info()`, which reports system RAM, and **not** `MEMORY_STATIC`, which under-reported by 11x in the spike |

**Not on this interface**: frame time and FPS. Frame time comes from the engine's per-frame delta and
FPS is derived from it as `1000 / frameMs`, because the engine's own FPS counter reads a frozen
`1.0` in short runs (research R11). Deriving FPS from the same samples the statistics use means the
overlay and the log can never disagree with each other.

**Sampling cadence**: `ProcessMemoryBytes` reads a file, so it is polled at the overlay's 4 Hz
refresh, never per frame.

---

## ScreenshotName


Validated capture name (FR-025).

| Rule | Behavior |
|---|---|
| Empty or omitted | Falls back to the default name `main` (FR-021) — deliberately the same default `scripts/screenshot.sh` uses, so a no-argument capture from either path writes the one file the golden reference covers. |
| Contains `/`, `\`, or `..` | Rejected — must not write outside `artifacts/`. |
| Contains characters illegal in a file name | Rejected with a message naming the offending input. |
| Already ends in `.png` | Accepted; the extension is not doubled. |

**Returns** a value object wrapping the safe bare file name. Path joining happens on the Game side;
Core never touches the filesystem.

---

## Engine-side types (`src/Game`, not modelled as data)

| Type | Role |
|---|---|
| `Main` (root script of `scenes/Main.tscn`) | Placeholder capture target (FR-033). Named to match its scene file, as constitution VI requires of every scene root. |
| `DevConsole` (autoload, `CanvasLayer`) | Input handling, text display, focus management. Delegates every decision to `CommandRegistry`. |
| `ScreenshotHarness` (autoload, `Node`) | Reads user cmdline args, counts frames, calls `IScreenshotService`, quits with a status. Inert when `--screenshot` is absent. |
| `GodotScreenshotService` | Implements Core's `IScreenshotService` using the viewport texture. |
| `Logging` | Static `Logging.For<T>()` entry point (FR-004). |
| `GodotSink` | Serilog sink bridging to `GD.Print`/`GD.PushError`. The only place those calls are permitted (constitution III). |
| `PerfMonitor` (autoload) | Samples every frame into the histogram from startup regardless of overlay visibility (FR-045), draws the overlay `CanvasLayer` at 4 Hz, and writes interim and final statistics records. |
| `GodotPerformanceCounters` | Implements Core's `IPerformanceCounters`. |
| `WarnErrorFlushSink` | Decorator forcing a disk flush at Warning and above (FR-005). |

**Naming (constitution VI)** applies to every row above: scene files are PascalCase and match their
root script's class name, signals are PascalCase, and `[Export]` fields carry no prefix. C# style
beyond that is not stated here or anywhere else in prose — it lives in `.editorconfig` and is
enforced by the `verify.sh` style stage (research R13).

---

## Session Log (file, not a type)

One file per run under `user://logs/` (research R6).

| Aspect | Decision |
|---|---|
| Naming | Sortable start-time stamp, distinct from Godot's `godot.log`. |
| Entry shape | Timestamp, level, source system, message — the four fields FR-002 requires. |
| Durability | Warning+ flushed immediately; Debug/Info batched and flushed on interval and at shutdown (FR-005). |
| Retention | Newest 10 kept, older pruned by `LogRetentionPolicy` (FR-006). |
