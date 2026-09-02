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

## ScreenshotName

Validated capture name (FR-025).

| Rule | Behavior |
|---|---|
| Empty or omitted | Falls back to the default name `screenshot` (FR-021). |
| Contains `/`, `\`, or `..` | Rejected — must not write outside `artifacts/`. |
| Contains characters illegal in a file name | Rejected with a message naming the offending input. |
| Already ends in `.png` | Accepted; the extension is not doubled. |

**Returns** a value object wrapping the safe bare file name. Path joining happens on the Game side;
Core never touches the filesystem.

---

## Engine-side types (`src/Game`, not modelled as data)

| Type | Role |
|---|---|
| `DevConsole` (autoload, `CanvasLayer`) | Input handling, text display, focus management. Delegates every decision to `CommandRegistry`. |
| `ScreenshotHarness` (autoload, `Node`) | Reads user cmdline args, counts frames, calls `IScreenshotService`, quits with a status. Inert when `--screenshot` is absent. |
| `GodotScreenshotService` | Implements Core's `IScreenshotService` using the viewport texture. |
| `Logging` | Static `Logging.For<T>()` entry point (FR-004). |
| `GodotSink` | Serilog sink bridging to `GD.Print`/`GD.PushError`. The only place those calls are permitted (constitution III). |
| `WarnErrorFlushSink` | Decorator forcing a disk flush at Warning and above (FR-005). |

---

## Session Log (file, not a type)

One file per run under `user://logs/` (research R6).

| Aspect | Decision |
|---|---|
| Naming | Sortable start-time stamp, distinct from Godot's `godot.log`. |
| Entry shape | Timestamp, level, source system, message — the four fields FR-002 requires. |
| Durability | Warning+ flushed immediately; Debug/Info batched and flushed on interval and at shutdown (FR-005). |
| Retention | Newest 10 kept, older pruned by `LogRetentionPolicy` (FR-006). |
