# Bug Fix: MSBuild warnings (CS0618 obsolete Serilog FileSink ctor, CA1873 eager log-argument evaluation)

- **Slug**: msbuild-warnings
- **Fixed**: 2026-09-03
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

`dotnet build NewGame1.csproj` now produces 0 warnings (down from 7). The 6 CA1873 sites were migrated to `[LoggerMessage]` source-generated logging methods as the assessment prescribed. The CS0618 site was resolved differently than proposed — the assessment's "extra `hooks: null` argument selects a non-obsolete overload" claim was verified false (that overload is `internal`, not visible outside `Serilog.Sinks.File.dll`), so a justified `#pragma warning disable/restore CS0618` was used instead. No behavior change in either case.

## Changes

| File | Change | Notes |
|------|--------|-------|
| `src/Game/Infrastructure/Logging.cs` | modified | `static class Logging` → `static partial class`; `FileSink` construction wrapped in `#pragma warning disable/restore CS0618` with a justification comment (deviates from assessment, see below); `OnUnhandledException`'s `LogCritical(...)` call replaced by a `[LoggerMessage]`-generated `LogUnhandledException` extension method. |
| `src/Game/Autoloads/ScreenshotHarness.cs` | modified | The two `_logger?.LogInformation(...)` calls in `RunAsync` replaced by calls to new `LogScreenshotReplaced`/`LogScreenshotWritten` `[LoggerMessage]` methods (declared as plain static methods, not extensions — see below), guarded by an explicit `_logger is not null` check in place of `?.`. |
| `src/Game/Autoloads/DevConsole.cs` | modified | `DetermineOpenAllowed`'s `logger.LogInformation(...)` and `OnSubmitted`'s `_logger.LogInformation(...)` replaced by calls to new `LogDevConsoleGating`/`LogCommandSucceeded` `[LoggerMessage]` methods. |
| `src/Game/Autoloads/PerfMonitor.cs` | modified | `WriteStatistics`'s `_logger.LogInformation(...)` replaced by `_logger.LogFrameTimeStatistics(...)`, an extension method declared on a new `internal static partial class PerfMonitorLog` in the same file, called inside an explicit `if (_logger.IsEnabled(LogLevel.Information))` guard (needed in addition to the `[LoggerMessage]` migration — see below). |

## Diff Highlights

`Logging.cs` — CS0618 suppression (corrected from the assessment's proposed fix):

```csharp
// Serilog.Sinks.File 7.0.0 has no public, non-obsolete way to construct a directly
// flushable buffered file sink: the only other FileSink constructor is internal, and
// WriteTo.File() doesn't hand back the sink instance FlushNow()/WarnErrorFlushSink need.
#pragma warning disable CS0618 // FileSink(string, ITextFormatter, long?, Encoding?, bool) is obsolete
_fileSink = new FileSink(resolution.FilePath!, formatter, fileSizeLimitBytes: null, Encoding.UTF8, buffered: true);
#pragma warning restore CS0618
```

`PerfMonitor.cs` — the one site that needed an `IsEnabled` guard on top of the `[LoggerMessage]` migration, because its arguments include further method calls (`FormatPercentile`, `FormatCount`, `FormatBytes`):

```csharp
if (_logger.IsEnabled(LogLevel.Information))
{
    _logger.LogFrameTimeStatistics(
        kind, stats.AverageMs, FormatPercentile(stats.P95Ms), FormatPercentile(stats.P99Ms),
        stats.WorstMs, stats.SampleCount, stats.IsLowConfidence,
        FormatCount(_counters.DrawCalls), FormatBytes(_counters.ProcessMemoryBytes), FormatBytes(_counters.VideoMemoryBytes));
}
```

## Tests Added or Updated

None. Per the assessment, this is a warning-only, no-behavior-change fix; `dotnet build` going from 7 warnings to 0 is itself the regression check (confirmed below), and the existing Core.Tests and Godot test suites (61 + Godot in-engine tests, all passing) confirm no behavioral regression in the touched code paths (dev console gating/history, screenshot capture, perf-stat logging, startup logging).

## Local Verification

- `dotnet build NewGame1.csproj -v:q -t:Rebuild` → **Build succeeded, 0 Warning(s), 0 Error(s)** (was 7 warnings before the fix).
- `dotnet test tests/Core.Tests/NewGame1.Core.Tests.csproj` → **61 passed, 0 failed** (pre-existing warnings in that project are unrelated to this bug's 7 flagged sites — none of them are in `NewGame1.csproj`).
- `scripts/verify.sh` → **all 6 stages PASS** (Build, Code style incl. `dotnet format --verify-no-changes`, Core tests, Godot tests, Screenshot, Golden compare). Screenshot inspected: matches the golden baseline, no visual regression.
- `git diff --stat project.godot` → empty; no editor-session rewrite to worry about.
- No new `.cs` files were added, so no `.cs.uid` sidecars were needed.

## Deviations from Assessment

1. **CS0618 remediation was factually wrong and could not be applied as written.** The assessment's preferred fix ("passing `hooks: null` resolves to [a] second, non-obsolete overload... Reflecting over the installed package confirms [this]") does not hold up: I reflected over `Serilog.Sinks.File.dll` 7.0.0 directly (`FileSink.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)`) and found the 6-argument `hooks`-accepting constructor is `internal` (`ctor.IsAssembly == true`, `IsPublic == false`), not callable from `NewGame1.csproj`. I also checked the two other public types the CS0618 message's "use `WriteTo.File()` instead" hint could plausibly point at (`SharedFileSink`, `PeriodicFlushToDiskSink`) — both are themselves type-level `[Obsolete]` in this package version, for the same reason. Serilog 7.0.0 genuinely has no public, non-obsolete way to construct a directly flushable buffered file sink outside the `WriteTo.File()` fluent pipeline, and that pipeline doesn't expose the sink instance this code needs for `WarnErrorFlushSink`/`FlushNow()`/`Shutdown()`. Given the root cause itself (an obsolete-marked constructor) was otherwise correctly diagnosed, and the assessment already documented the "suppress per call site with a justification" alternative as an acceptable pattern for CA1873, I applied that same alternative to the CS0618 site instead of the disproven "preferred" fix, and recorded the reasoning above rather than stopping the whole fix and requesting reassessment. This is a low-risk substitution: no behavior changes either way, and it does not touch the CA1873 remediation, which was verified correct.

2. **`[LoggerMessage]` extension methods could not be declared directly on `ScreenshotHarness`, `DevConsole`, or `PerfMonitor`** as the assessment's "decorated with `[LoggerMessage]` on each of the four classes" wording implied, because `this ILogger logger` extension-method parameters require a non-generic **static** container (CS1106), and these three are instance `Node`/`CanvasLayer` subclasses. For `ScreenshotHarness` and `DevConsole` the generated methods were declared as plain (non-extension) `private static partial` methods taking an explicit `ILogger`/`ILogger<T>` parameter instead — functionally identical, just called as `LogXxx(logger, ...)` rather than `logger.LogXxx(...)`.

3. **`PerfMonitor.WriteStatistics` needed an explicit `IsEnabled` guard in addition to the `[LoggerMessage]` migration** — the assessment treated the `[LoggerMessage]` migration alone as sufficient to resolve CA1873 for all 6 sites, but this one call's arguments include further method invocations (`FormatPercentile`, `FormatCount`, `FormatBytes`), which CA1873 continues to flag as unconditionally-evaluated even when the outer call is to a generated `[LoggerMessage]` method (correctly — C# evaluates all arguments before any method call). This required wrapping the call in `if (_logger.IsEnabled(LogLevel.Information))`. That guard was only recognized by the analyzer once the call used true extension-method syntax (`_logger.LogFrameTimeStatistics(...)`, receiver matching the guard's `_logger.IsEnabled(...)`) rather than a plain static call — which is why `PerfMonitor.cs` additionally gained a small `internal static partial class PerfMonitorLog` to host the extension method (see deviation 2: `PerfMonitor` itself can't host it directly).

Also worth logging separately from the code fix: the CS0618/CA1873 asymmetry above (constructor accessibility, and the guard-recognition heuristic's dependence on extension-method call syntax) are generically useful facts about this Serilog/analyzer version pairing that weren't in the assessment; no other bug or file is affected.

## Follow-ups

- If a future Serilog upgrade exposes a public non-obsolete way to build a directly-flushable buffered file sink (or `WriteTo.File()` gains a way to retrieve the constructed sink), the `#pragma warning disable CS0618` in `Logging.cs` can be dropped in favor of that API.
- None of the touched call sites are on a hot path today (screenshot capture, dev-console commands/gating, ~30s perf-stat interval, startup, unhandled-exception handler); if `PerfMonitor` ever starts logging per-frame instead of per-interval, re-confirm the `IsEnabled` guard around `WriteStatistics` still holds — it's now load-bearing for CA1873, not just an optimization.
