# Bug Fix: MSBuild warnings (issue #3)

- **Slug**: msbuild-warnings
- **Fixed**: 2026-09-03
- **Assessment**: ./assessment.md
- **Status**: applied

This bug was fixed over two cycles. **Cycle 1** repaired the 7 warnings visible in the reporter's
screenshot; `/speckit-bug-test` then returned `partial`, finding the screenshot was a *sample* of the
warning surface rather than its definition. The assessment was rewritten at whole-solution scope and
**cycle 2** cleared the 22 remaining warnings — 14 emitted, 8 hidden behind a suppression — and added
the two gate controls that keep the count at zero.

Cycle 1's work is unchanged and needed no rework; it is preserved below in full. Cycle 2 is the work
performed against the current (2026-09-03) assessment.

---

## Cycle 2 — whole-solution scope (current assessment)

### Summary

`dotnet build NewGame1.sln --no-incremental` now reports **0 warnings across the whole solution**,
with **no diagnostic suppressed anywhere in the repo** — `.editorconfig` no longer contains a single
`severity = none` line. The 14 emitted warnings in `tests/Core.Tests/` were fixed at the source, the 8
CA1848 warnings that `.editorconfig` had been hiding were migrated to `[LoggerMessage]`, and
`scripts/verify.sh` gained two controls so the count cannot drift back silently: a `--no-incremental
-warnaserror` build ratchet, and an audit that rejects any future suppression lacking a written
justification with an expiry condition.

### Decisions taken before starting

The assessment left two questions open. Both were put to the developer:

- **CA1861 in the test project** — fix, don't suppress. All 8 `new[] { … }` assertion literals were
  hoisted to `private static readonly string[]` fields. This is why the repo now has zero
  suppressions rather than one justified test-scoped one.
- **Ratchet scope** — `scripts/verify.sh` only. Confirmed on inspection that the repo has no
  `.github/workflows/` and no CI configuration of any kind, so `verify.sh` is in fact the only gate
  there is; nothing would have inherited a CI-level ratchet.

### Changes

| File | Change | Notes |
|------|--------|-------|
| `tests/Core.Tests/Console/CommandLineParserTests.cs` | modified | 3× CA1861: `ExpectedTopicsOnly`, `ExpectedTopicsAndExtra`, `ExpectedQuotedShot` hoisted to static readonly fields. |
| `tests/Core.Tests/Console/CommandRegistryTests.cs` | modified | 1× CA1861 (`ExpectedNamesInOrder`); 2× CS8604 → `ShouldNotBeNull().ShouldContain(…)`. |
| `tests/Core.Tests/Console/HelpCommandTests.cs` | modified | 1× CS8604 → `ShouldNotBeNull().ShouldContain(…)` (applied to both assertions in the pair, see Deviations). |
| `tests/Core.Tests/Screenshots/ScreenshotCommandTests.cs` | modified | 1× CS8604 → `ShouldNotBeNull().ShouldContain(…)`. |
| `tests/Core.Tests/Diagnostics/BoundedLogTests.cs` | modified | 2× CA1861: `ExpectedThreeOldestFirst`, `ExpectedAfterOldestDropped`. |
| `tests/Core.Tests/Diagnostics/LogRetentionPolicyTests.cs` | modified | 2× CA1861: `ExpectedLiveProcessLogRetained`, `ExpectedPidlessLogEligible`. |
| `tests/Core.Tests/Diagnostics/FrameTimeHistogramTests.cs` | modified | 2× CS8629: `stats.P95Ms.Value.ShouldBeInRange(…)` → `stats.P95Ms.ShouldNotBeNull().ShouldBeInRange(…)`, same for `P99Ms`. |
| `src/Core/Console/CommandRegistry.cs` | modified | `sealed class` → `sealed partial class`; 2× CA1848 → `LogRegistrationRejected` / `LogCommandThrew`. |
| `src/Game/Main.cs` | modified | 3× CA1848 → `LogStartupReady` / `LogShutdownCloseRequested` / `LogShutdownExitingTree`; the two `_logger?.` sites became explicit `if (_logger is not null)` blocks. |
| `src/Game/Autoloads/DevConsole.cs` | modified | 1× CA1848 → `LogCommandFailed`. Joins `LogDevConsoleGating`/`LogCommandSucceeded` from cycle 1 — this was the sibling site in the same file that the suppression had hidden. |
| `src/Game/Autoloads/ScreenshotHarness.cs` | modified | 1× CA1848 → `LogCaptureFailed`, with the `_logger?.` rewritten to an explicit null check. Also a same-file sibling of a cycle-1 fix. |
| `src/Game/Infrastructure/GodotScreenshotService.cs` | modified | `sealed class` → `sealed partial class`; 1× CA1848 → `LogTempFileDeleteFailed`, with `Logging.TryFor<T>()?.LogWarning(…)` hoisted to a local plus an `is not null` check. |
| `.editorconfig` | modified | **Removed** the 5-line `dotnet_diagnostic.CA1848.severity = none` block (lines 36–40) and its now-obsolete justification. No `severity = none` line remains in the repo. |
| `scripts/verify.sh` | modified | `stage_build` gained `--no-incremental -warnaserror -v minimal`; `stage_style` gained the suppression audit. |

### Diff Highlights

The ratchet — both flags load-bearing, for different reasons:

```bash
stage_build() {
    # --no-incremental and -warnaserror are both load-bearing (issue #3). Up-to-date projects are
    # not recompiled, so a warm build re-emits none of their diagnostics: the same tree that
    # cold-builds with 14 warnings reports "0 Warning(s)" on the second run. Forcing a full
    # recompile is what makes the count real; -warnaserror is what makes it blocking.
    dotnet build NewGame1.sln --no-incremental -warnaserror -v minimal
}
```

The second control, closing the ratchet's blind spot (`-warnaserror` cannot promote a warning that
the analyzer never emitted). A suppression stays permissible — it just has to be re-auditable:

```bash
    unjustified="$(awk '
        /^[[:space:]]*#/ {
            if (!in_comment) { in_comment = 1; expiry = 0 }
            if (tolower($0) ~ /expiry:/) { expiry = 1 }
            next
        }
        /^[[:space:]]*dotnet_diagnostic\.[A-Za-z0-9]+\.severity[[:space:]]*=[[:space:]]*none([[:space:]]|$)/ {
            if (!in_comment || !expiry) { print "  .editorconfig:" FNR ": " $0 }
            next
        }
        { in_comment = 0; expiry = 0 }
    ' "${repo_root}/.editorconfig")"
```

The CS8629 rewrite, which strengthens the assertion rather than silencing it — `.Value` on a null
`double?` throws `InvalidOperationException` with no context, whereas `ShouldNotBeNull()` fails as a
readable assertion naming the member:

```csharp
stats.P95Ms.ShouldNotBeNull().ShouldBeInRange(9.9, 10.1);
stats.P99Ms.ShouldNotBeNull().ShouldBeInRange(9.9, 10.1);
```

### Tests Added or Updated

No new test files. Per the assessment this is warning cleanup with no intended behavior change; the
gate is the regression test. Six existing assertions were strengthened in place:

- `Console/CommandRegistryTests.cs::UnrecognizedNameYieldsFailureNamingInputAndPointingAtHelp` and
  `::HandlerThatThrowsIsCaughtAndConvertedToFailureCarryingDetail` — a null `FailureReason` now fails
  as an assertion instead of as a `NullReferenceException` inside Shouldly.
- `Console/HelpCommandTests.cs::HelpWithUnknownCommandFailsNamingItAndPointingBackAtBareHelp` — same.
- `Screenshots/ScreenshotCommandTests.cs::ServiceFailureProducesAFailureResultCarryingTheReason` — same.
- `Diagnostics/FrameTimeHistogramTests.cs` (P95/P99) — an absent percentile now fails as an assertion
  instead of an `InvalidOperationException` from `Nullable<double>.Value`.

The **ratchet itself was tested in both directions**, since a gate that cannot fail is worthless:

- Reintroduced one raw `_logger.LogInformation(…)` call in `Main.cs` → `stage_build` produced
  `error CA1848` and exited **1**. Probe reverted.
- Appended an unjustified `dotnet_diagnostic.CA1848.severity = none` to `.editorconfig` → the new
  `stage_style` audit printed `.editorconfig:37: …` and failed the stage. With a preceding comment
  block containing an `Expiry:` line, the same suppression was **accepted**. Probe reverted.

### Local Verification

- `dotnet build NewGame1.sln --no-incremental -v:m` **before** the fix → `14 Warning(s)`, matching
  the assessment's inventory site-for-site.
- Same command **after** step 1 → `0 Warning(s), 0 Error(s)`.
- With `CA1848` temporarily lifted to `warning`, the build surfaced exactly the 8 hidden sites the
  assessment predicted, at the predicted file/line/column. After migration, same command → **`0
  Warning(s), 0 Error(s)`**.
- `dotnet test tests/Core.Tests/…` → **61 passed, 0 failed**.
- `dotnet format NewGame1.sln --verify-no-changes --no-restore` → **exit 0**. This settles the one
  claim the assessment flagged as reasoned-but-unmeasured (that `dotnet format`'s exit 2 resolves
  once no CA1848 diagnostics remain). It does.
- **Default-severity probe**: with the `.editorconfig` block deleted, a deliberately reintroduced raw
  log call still produced `warning CA1848`. This confirms CA1848 is enabled by default under
  `AnalysisLevel=latest-recommended`, so *deleting* the line genuinely enforces the rule rather than
  quietly reverting it to unenforced. Had this come back clean, an explicit `= warning` line would
  have been required instead.
- `scripts/verify.sh` → **all 6 stages PASS** (Build, Code style, Core tests, Godot tests, Screenshot,
  Golden compare), exit 0. Screenshot at `artifacts/main.png` inspected: the placeholder scene renders
  as expected, no visual regression, and Golden compare passed independently.
- **Generated code inspected** (`-p:EmitCompilerGeneratedFiles=true`) for both projects. All 8 new
  methods match cycle 1's verified pattern: a cached `LoggerMessage.Define…` delegate with
  `SkipEnabledCheck = true`, wrapped in a generated `if (logger.IsEnabled(…))`. Exception parameters
  are correctly routed to the callback's exception slot rather than treated as template arguments
  (`__LogRegistrationRejectedCallback(logger, message, ex)`).
- **Log rendering unchanged**, checked against pre-migration session logs:
  `2026-09-03 00:46:11.762 [INF] NewGame1.Main: Startup: Main ready` — identical level, category and
  message text to runs recorded before the change.
- `git status` — `project.godot` untouched; no new `.cs` files, so no `.cs.uid` sidecars were needed
  and `godot --headless --import` was not required.

**Build, analyzer, test and runtime warnings, quoted verbatim, per CLAUDE.md**: the final
`--no-incremental` whole-solution build emits `0 Warning(s)` and `0 Error(s)`; `dotnet test` reports
no warnings; all six `verify.sh` stages print only `PASS:` lines. There is no warning text to quote
because none was emitted.

### Deviations from Assessment

1. **`.editorconfig` deletion was verified rather than assumed.** The assessment said to remove line
   40. Removing a `severity = none` line only enforces the rule if the rule is on by default —
   otherwise it silently reverts from "suppressed loudly" to "suppressed quietly", which would have
   defeated the entire point of the issue. I probed this explicitly (see Local Verification) and
   confirmed CA1848 *is* enabled by default at `latest-recommended`, so plain deletion is correct. I
   removed the whole 5-line block including its stale justification comment, not just line 40.

2. **Step 4 was implemented as an automated check, not only a written convention.** The assessment
   proposed "adopt a convention that every `severity = none` line carries a justification and an
   expiry condition, and re-audit periodically". A convention that only lives in a document is
   exactly the failure mode this issue is about, and the repo already has precedent for encoding this
   kind of rule as an anti-vacuity check in `verify.sh` (FR-028f). So the convention is enforced by
   `stage_style`. It is deliberately permissive about *what* the justification says — it only
   requires an `Expiry:` line in the adjacent comment block, so the control cannot be satisfied by an
   empty comment but also does not try to referee the reasoning.

3. **CS8604 fixes were applied to whole assertion pairs, not only to the flagged line.** In
   `CommandRegistryTests` and `HelpCommandTests` the compiler flags only the *first*
   `FailureReason.ShouldContain(…)` of each adjacent pair — after that call, nullable flow analysis
   already treats the member as non-null, so the second line was never flagged. Fixing only the
   flagged line would leave two visually identical adjacent assertions written differently for a
   reason invisible in the source. Both lines in each pair were converted. This touches 2 lines more
   than the strict minimum and changes no behavior.

4. **`CommandRegistry` and `GodotScreenshotService` were made `partial`** rather than given a
   separate `…Log` host class. The assessment offered both. The host-class pattern exists in
   `PerfMonitor.cs` only because CA1873's guard recognition needs extension-method call syntax there;
   neither of these sites has a CA1873 guard requirement, so the simpler option applies. Note
   `CommandRegistry` has a primary constructor — `sealed partial class CommandRegistry(ILogger<…>?
   logger = null)` compiles fine, as only one part declares it.

### Follow-ups

- `EventId` at all 8 newly-migrated sites changed from 0 to a generator-assigned hash (e.g.
  `1788970517` for `LogStartupReady`), same as cycle 1's 6 sites. Invisible today because
  `Logging.LogLineTemplate` does not render `EventId`; it would matter to any future consumer that
  filters on event id.
- The repo has no CI. `verify.sh` is the only thing enforcing the zero-warning invariant, and it only
  runs when someone runs it. If CI is ever added, running `scripts/verify.sh` is sufficient — the
  ratchet is inside it, not alongside it.
- `GodotScreenshotService.LogTempFileDeleteFailed` and `DevConsole.LogCommandFailed` sit on
  error/warning paths that are hard to exercise headlessly; both were verified at the generated-code
  level rather than by observed output, as cycle 1 did for 3 of its 6 sites.
- The suppression audit keys on the literal string `Expiry:`. If a future suppression is added to a
  *different* file (e.g. a test-scoped `.editorconfig`), the check must be pointed at it too — it
  currently reads only the repo-root `.editorconfig`.

---

## Cycle 1 — screenshot scope (superseded assessment)

Fixed 2026-09-03 against the assessment committed in `86a5cff`, since rewritten. Status: applied.
Retained unchanged; the current assessment confirms this work needed no rework.

### Summary

`dotnet build NewGame1.csproj` now produces 0 warnings (down from 7). The 6 CA1873 sites were migrated to `[LoggerMessage]` source-generated logging methods as the assessment prescribed. The CS0618 site was resolved differently than proposed — the assessment's "extra `hooks: null` argument selects a non-obsolete overload" claim was verified false (that overload is `internal`, not visible outside `Serilog.Sinks.File.dll`), so a justified `#pragma warning disable/restore CS0618` was used instead. No behavior change in either case.

### Changes

| File | Change | Notes |
|------|--------|-------|
| `src/Game/Infrastructure/Logging.cs` | modified | `static class Logging` → `static partial class`; `FileSink` construction wrapped in `#pragma warning disable/restore CS0618` with a justification comment (deviates from assessment, see below); `OnUnhandledException`'s `LogCritical(...)` call replaced by a `[LoggerMessage]`-generated `LogUnhandledException` extension method. |
| `src/Game/Autoloads/ScreenshotHarness.cs` | modified | The two `_logger?.LogInformation(...)` calls in `RunAsync` replaced by calls to new `LogScreenshotReplaced`/`LogScreenshotWritten` `[LoggerMessage]` methods (declared as plain static methods, not extensions — see below), guarded by an explicit `_logger is not null` check in place of `?.`. |
| `src/Game/Autoloads/DevConsole.cs` | modified | `DetermineOpenAllowed`'s `logger.LogInformation(...)` and `OnSubmitted`'s `_logger.LogInformation(...)` replaced by calls to new `LogDevConsoleGating`/`LogCommandSucceeded` `[LoggerMessage]` methods. |
| `src/Game/Autoloads/PerfMonitor.cs` | modified | `WriteStatistics`'s `_logger.LogInformation(...)` replaced by `_logger.LogFrameTimeStatistics(...)`, an extension method declared on a new `internal static partial class PerfMonitorLog` in the same file, called inside an explicit `if (_logger.IsEnabled(LogLevel.Information))` guard (needed in addition to the `[LoggerMessage]` migration — see below). |

### Diff Highlights

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

### Tests Added or Updated

None. Per the assessment, this is a warning-only, no-behavior-change fix; `dotnet build` going from 7 warnings to 0 is itself the regression check (confirmed below), and the existing Core.Tests and Godot test suites (61 + Godot in-engine tests, all passing) confirm no behavioral regression in the touched code paths (dev console gating/history, screenshot capture, perf-stat logging, startup logging).

### Local Verification

- `dotnet build NewGame1.csproj -v:q -t:Rebuild` → **Build succeeded, 0 Warning(s), 0 Error(s)** (was 7 warnings before the fix).
- `dotnet test tests/Core.Tests/NewGame1.Core.Tests.csproj` → **61 passed, 0 failed** (pre-existing warnings in that project are unrelated to this bug's 7 flagged sites — none of them are in `NewGame1.csproj`).
- `scripts/verify.sh` → **all 6 stages PASS** (Build, Code style incl. `dotnet format --verify-no-changes`, Core tests, Godot tests, Screenshot, Golden compare). Screenshot inspected: matches the golden baseline, no visual regression.
- `git diff --stat project.godot` → empty; no editor-session rewrite to worry about.
- No new `.cs` files were added, so no `.cs.uid` sidecars were needed.

### Deviations from Assessment

1. **CS0618 remediation was factually wrong and could not be applied as written.** The assessment's preferred fix ("passing `hooks: null` resolves to [a] second, non-obsolete overload... Reflecting over the installed package confirms [this]") does not hold up: I reflected over `Serilog.Sinks.File.dll` 7.0.0 directly (`FileSink.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)`) and found the 6-argument `hooks`-accepting constructor is `internal` (`ctor.IsAssembly == true`, `IsPublic == false`), not callable from `NewGame1.csproj`. I also checked the two other public types the CS0618 message's "use `WriteTo.File()` instead" hint could plausibly point at (`SharedFileSink`, `PeriodicFlushToDiskSink`) — both are themselves type-level `[Obsolete]` in this package version, for the same reason. Serilog 7.0.0 genuinely has no public, non-obsolete way to construct a directly flushable buffered file sink outside the `WriteTo.File()` fluent pipeline, and that pipeline doesn't expose the sink instance this code needs for `WarnErrorFlushSink`/`FlushNow()`/`Shutdown()`. Given the root cause itself (an obsolete-marked constructor) was otherwise correctly diagnosed, and the assessment already documented the "suppress per call site with a justification" alternative as an acceptable pattern for CA1873, I applied that same alternative to the CS0618 site instead of the disproven "preferred" fix, and recorded the reasoning above rather than stopping the whole fix and requesting reassessment. This is a low-risk substitution: no behavior changes either way, and it does not touch the CA1873 remediation, which was verified correct.

2. **`[LoggerMessage]` extension methods could not be declared directly on `ScreenshotHarness`, `DevConsole`, or `PerfMonitor`** as the assessment's "decorated with `[LoggerMessage]` on each of the four classes" wording implied, because `this ILogger logger` extension-method parameters require a non-generic **static** container (CS1106), and these three are instance `Node`/`CanvasLayer` subclasses. For `ScreenshotHarness` and `DevConsole` the generated methods were declared as plain (non-extension) `private static partial` methods taking an explicit `ILogger`/`ILogger<T>` parameter instead — functionally identical, just called as `LogXxx(logger, ...)` rather than `logger.LogXxx(...)`.

3. **`PerfMonitor.WriteStatistics` needed an explicit `IsEnabled` guard in addition to the `[LoggerMessage]` migration** — the assessment treated the `[LoggerMessage]` migration alone as sufficient to resolve CA1873 for all 6 sites, but this one call's arguments include further method invocations (`FormatPercentile`, `FormatCount`, `FormatBytes`), which CA1873 continues to flag as unconditionally-evaluated even when the outer call is to a generated `[LoggerMessage]` method (correctly — C# evaluates all arguments before any method call). This required wrapping the call in `if (_logger.IsEnabled(LogLevel.Information))`. That guard was only recognized by the analyzer once the call used true extension-method syntax (`_logger.LogFrameTimeStatistics(...)`, receiver matching the guard's `_logger.IsEnabled(...)`) rather than a plain static call — which is why `PerfMonitor.cs` additionally gained a small `internal static partial class PerfMonitorLog` to host the extension method (see deviation 2: `PerfMonitor` itself can't host it directly).

Also worth logging separately from the code fix: the CS0618/CA1873 asymmetry above (constructor accessibility, and the guard-recognition heuristic's dependence on extension-method call syntax) are generically useful facts about this Serilog/analyzer version pairing that weren't in the assessment; no other bug or file is affected.

### Follow-ups

- If a future Serilog upgrade exposes a public non-obsolete way to build a directly-flushable buffered file sink (or `WriteTo.File()` gains a way to retrieve the constructed sink), the `#pragma warning disable CS0618` in `Logging.cs` can be dropped in favor of that API.
- None of the touched call sites are on a hot path today (screenshot capture, dev-console commands/gating, ~30s perf-stat interval, startup, unhandled-exception handler); if `PerfMonitor` ever starts logging per-frame instead of per-interval, re-confirm the `IsEnabled` guard around `WriteStatistics` still holds — it's now load-bearing for CA1873, not just an optimization.
