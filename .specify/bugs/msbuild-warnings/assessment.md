# Bug Assessment: MSBuild warnings (CS0618 obsolete Serilog FileSink ctor, CA1873 eager log-argument evaluation)

- **Slug**: msbuild-warnings
- **Created**: 2026-09-02
- **Source**: https://github.com/molnara/new-game-1/issues/3 (host: github.com, branch: allowlisted)
- **Verdict**: valid
- **Severity**: low

## Report (verbatim or summarized)

Issue #3, "Fix MSBuild Warnings" (opened 2026-09-02, label `bug`). The issue body carries no text, only an editor "Problems" panel screenshot (`https://github.com/user-attachments/assets/decd342b-fa74-4c24-b062-b99f1bea19b4`, fetched and inspected as an image since it is same-host `user-attachments` content on the allowlisted `github.com` issue). It lists 7 problems across 4 files:

- `src/Game/Infrastructure/Logging.cs` (2 issues)
  - `'FileSink.FileSink(string, ITextFormatter, long?, Encoding?, bool)' is obsolete: 'This type and constructor will be removed from the public API in a future version; use `WriteTo.File()` instead.'` (73,25)
  - `Evaluation of this argument may be expensive and unnecessary if logging is disabled` (CA1873) (127,9)
- `src/Game/Autoloads/ScreenshotHarness.cs` (2 issues) — CA1873 at (84,21), (88,21)
- `src/Game/Autoloads/DevConsole.cs` (2 issues) — CA1873 at (125,9), (195,13)
- `src/Game/Autoloads/PerfMonitor.cs` (1 issue) — CA1873 at (201,9)

## Symptom

`dotnet build NewGame1.csproj` emits 7 compiler/analyzer warnings (1 `CS0618` obsolete-API warning, 6 `CA1873` "avoid potentially expensive logging" warnings). No functional misbehavior — the build succeeds and the game runs — but the warning count is non-zero, which is what the reporter wants driven to zero.

## Reproduction

1. `dotnet build NewGame1.csproj -v:q`
2. Observe 7 warnings, exactly matching the file/line/column list in the issue screenshot (confirmed by running the build in this assessment — see below).

```
src/Game/Infrastructure/Logging.cs(73,25): warning CS0618: 'FileSink.FileSink(string, ITextFormatter, long?, Encoding?, bool)' is obsolete: ...
src/Game/Autoloads/ScreenshotHarness.cs(84,21): warning CA1873: Evaluation of this argument may be expensive ...
src/Game/Autoloads/ScreenshotHarness.cs(88,21): warning CA1873: ...
src/Game/Infrastructure/Logging.cs(127,9): warning CA1873: ...
src/Game/Autoloads/PerfMonitor.cs(201,9): warning CA1873: ...
src/Game/Autoloads/DevConsole.cs(125,9): warning CA1873: ...
src/Game/Autoloads/DevConsole.cs(195,13): warning CA1873: ...
    7 Warning(s)
```

No `[NEEDS CLARIFICATION]` items — the report reproduces exactly as shown, and the codebase state fully explains all 7 entries.

## Suspected Code Paths

- `src/Game/Infrastructure/Logging.cs:73` — `_fileSink = new FileSink(resolution.FilePath!, formatter, fileSizeLimitBytes: null, Encoding.UTF8, buffered: true);` calls the `Serilog.Sinks.File.FileSink` 5-arg constructor, which carries `[Obsolete]` in Serilog.Sinks.File 7.0.0.
- `src/Game/Infrastructure/Logging.cs:127-128` — `Factory.CreateLogger("UnhandledException").LogCritical(e.ExceptionObject as Exception, "Unhandled exception (terminating: {IsTerminating})", e.IsTerminating);` — classic `ILogger` extension-method call with a non-constant argument.
- `src/Game/Autoloads/ScreenshotHarness.cs:84,88` — `_logger?.LogInformation("Screenshot harness replaced existing screenshot {Path}", result.Path);` and the sibling "wrote {Path}" call.
- `src/Game/Autoloads/DevConsole.cs:125` — `logger.LogInformation("Dev console gating: ...", isExportedRelease, isEditorRun, devConsoleFlag, allowed);` inside `DetermineOpenAllowed`.
- `src/Game/Autoloads/DevConsole.cs:195` — `_logger.LogInformation("Command {Line} succeeded: {Message}", line, result.Message);` inside command execution.
- `src/Game/Autoloads/PerfMonitor.cs:201` — the multi-line `_logger.LogInformation("Frame time statistics (...)", kind, stats.AverageMs, FormatPercentile(...), ...)` inside `WriteStatistics`.

## Root Cause Hypothesis

**Confidence: high** (verified directly, not just inferred from the report).

Two independent, unrelated causes bundled under one issue:

1. **CS0618** — Serilog.Sinks.File 7.0.0 marks the 5-argument `FileSink` constructor `[Obsolete]` in favor of the `LoggerSinkConfiguration.File()` fluent API. `Logging.Initialize()` constructs a raw `FileSink` directly (rather than via `WriteTo.File()`) because it needs the concrete instance for two things the fluent API doesn't expose: wrapping it in `WarnErrorFlushSink` (immediate flush on Warning/Error, FR-005) and calling `FlushToDisk()` directly from `FlushNow()`/`Shutdown()`. Reflecting over the installed package confirms a second, **non-obsolete** overload exists with the same five parameters plus a trailing `FileLifecycleHooks hooks` parameter — passing `hooks: null` resolves to that overload and removes the warning with no behavior change.

2. **CA1873** ("Avoid potentially expensive logging", enabled by default under the SDK-style project's default analysis level) fires on every `ILogger.LogInformation`/`LogCritical` call whose message-template arguments aren't provably free of computation, because the classic extension-method call sites in this codebase evaluate their arguments unconditionally before the logger's own level check. All 6 flagged call sites are today low-frequency (once at startup, once in the unhandled-exception handler, once per screenshot capture, once per dev-console command, once per ~30s stats interval per `PerfMonitor.DefaultStatisticsIntervalSeconds`) — none are literally expensive today, but the analyzer can't know that from the call shape. Other `Log*` calls in the same files (e.g. `LogError`/`LogWarning` at `ScreenshotHarness.cs:97` and `DevConsole.cs:200`) are not flagged, consistent with this being a known quirk of the CA1873 heuristic rather than a real perf bug.

## Proposed Remediation

**Preferred**:
- Fix CS0618 by passing the extra `hooks: null` argument to the `FileSink` constructor at `Logging.cs:73`, selecting the non-obsolete overload. No behavior change; `WarnErrorFlushSink` and `FlushNow()`/`Shutdown()` keep working exactly as before since they only depend on `FileSink` implementing `ILogEventSink`/`IFlushableFileSink`, not on which constructor built it.
- Fix the 6 CA1873 sites by migrating them to the `[LoggerMessage]` source-generator pattern (`Microsoft.Extensions.Logging.Abstractions`, already transitively available via `Serilog.Extensions.Logging`): declare `static partial` logging methods (e.g. `LogScreenshotReplaced`, `LogScreenshotWritten`, `LogDevConsoleGating`, `LogCommandSucceeded`, `LogFrameTimeStatistics`, `LogUnhandledException`) decorated with `[LoggerMessage(EventId = ..., Level = LogLevel.Information, Message = "...")]` on each of the four classes (`ScreenshotHarness`, `DevConsole`, and `PerfMonitor` are already `partial`; `Logging` needs `partial` added to its `static class` declaration). This is the idiomatic, zero-runtime-cost fix Microsoft's own CA1873 documentation recommends, and it removes the warning by construction (the generated code checks `IsEnabled` before touching the arguments).

**Alternatives**:
- Suppress CA1873 per call site with `#pragma warning disable/restore CA1873` (or `[SuppressMessage]`) and a one-line justification, since none of the 6 sites are hot paths today. Less code churn than the source-generator migration, but loses the analyzer's protection if one of these call sites later moves onto a hot path (e.g. if `PerfMonitor` ever logs per-frame instead of per-interval).
- Disable `CA1873` project-wide via `.editorconfig` (`dotnet_diagnostic.CA1873.severity = none`). Not recommended — throws away the signal for the whole codebase rather than the 6 sites actually flagged.

**Files likely to change**:
- `src/Game/Infrastructure/Logging.cs` (CS0618 fix; `partial` keyword + `LogUnhandledException` source-gen method)
- `src/Game/Autoloads/ScreenshotHarness.cs` (2 source-gen methods)
- `src/Game/Autoloads/DevConsole.cs` (2 source-gen methods)
- `src/Game/Autoloads/PerfMonitor.cs` (1 source-gen method)

**Tests to add or update**:
- No new behavioral test is warranted — this is a warning-only, no-behavior-change fix. The existing build should simply go from 7 warnings to 0; `/speckit-bug-test` (or `scripts/verify.sh` if it checks warning count) should assert `dotnet build NewGame1.csproj` produces zero warnings.
- If the project has (or gains) a CI step that treats warnings as a signal, confirm no new `TreatWarningsAsErrors`/`WarningsAsErrors` regression is introduced by the `[LoggerMessage]` migration (source-generator diagnostics, if any, would surface as new warnings — check the build output after the change).

## Risks & Considerations

- `[LoggerMessage]` source-generated methods require the .NET SDK's Microsoft.Extensions.Logging source generator to be active for `net10.0` — already implicitly available since `Serilog.Extensions.Logging` 10.0.0 depends on current `Microsoft.Extensions.Logging.Abstractions`; low risk, but worth a `godot --headless --import` + build check after editing since `Logging.cs` and the three Autoload scripts are existing `.cs` files (no new `.cs.uid` sidecars needed, but a rebuild is required to confirm the generator fires cleanly under the Godot.NET.Sdk).
- `DevConsole.cs:125`'s call is a `private static` method taking `ILogger<DevConsole> logger` as a parameter rather than an instance field — the source-generated partial method there should be declared to take `this ILogger<DevConsole> logger` (extension-style) or as a static partial method with `logger` as an explicit parameter; either works with `[LoggerMessage]`, but pick one style consistently across the 3 Autoload files.
- No security, migration, or observability impact — this is warning cleanup only, no message content or log level changes.

## Open Questions

None — the report reproduced exactly and the root cause for both warning classes was confirmed against the installed package and the current source.
