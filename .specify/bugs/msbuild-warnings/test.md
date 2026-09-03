# Bug Verification: MSBuild warnings (CS0618 obsolete Serilog FileSink ctor, CA1873 eager log-argument evaluation)

- **Slug**: msbuild-warnings
- **Tested**: 2026-09-03
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: partial

## Summary

The 7 warnings named in the assessment are genuinely gone, and that part of the fix is sound and well-built. But the bug is **not resolved**, because the goal of issue #3 is *zero warnings in the build* — the 7 in the reporter's screenshot were a **sample of what the Problems panel happened to show, not the scope of the work**. After the fix, `dotnet build NewGame1.sln` still emits **14 warnings**, and a further **8 are hidden** by a project-wide analyzer suppression in `.editorconfig`. **22 warnings remain outstanding.**

The assessment scoped this bug to the 4 files visible in the screenshot, and the fix inherited that scope. This report initially repeated the same mistake — labelling the remaining 14 "outside issue #3's scope" — which is precisely the behavior the issue exists to stop. That framing was wrong and is corrected below.

## Scope Correction (read this first)

**Issue #3's actual acceptance criterion: the build produces no warnings, and nothing is silently suppressed to get there.**

| | Count | Where | Status |
|---|---|---|---|
| Warnings named in the assessment | 7 | `NewGame1.csproj` (4 files) | **fixed** |
| Warnings still emitted by the build | 14 | `tests/Core.Tests/` | **not addressed — never assessed** |
| Warnings suppressed project-wide in `.editorconfig` | 8 | `src/Core/` + `src/Game/` (5 files) | **not addressed — hidden by `CA1848 = none`** |
| **Total outstanding** | **22** | | |

**How 22 warnings stayed invisible.** Three independent mechanisms, each verified in this session:

1. **Incremental builds report nothing.** `dotnet build NewGame1.sln` on a warm tree prints `0 Warning(s)` — the projects are up to date, so they are not recompiled and their diagnostics are never re-emitted. The *same command* on a cold tree prints `14 Warning(s)`. Whoever ran a build second that day saw a clean one. This alone explains most of "warnings that never get mentioned".
2. **Analyzer suppression in `.editorconfig`.** `CA1848 = none` removes 8 production-code diagnostics before MSBuild ever sees them (section B).
3. **Scoping to a screenshot.** The assessment took the reporter's Problems-panel screenshot as the definition of scope, so the 14 in `tests/Core.Tests/` were never looked at.

Neither the assessment nor the fix mentions the 14 or the 8. A build that reports `0 Warning(s)` for `NewGame1.csproj` alone is not the same thing as a clean build, and it is not what the issue asks for.

## Outstanding Work — Full Inventory

### A. 14 warnings still emitted (`tests/Core.Tests/`)

Verified present after the fix via `dotnet build NewGame1.sln -v:n -t:Rebuild`. `src/Core/NewGame1.Core.csproj` is clean; all 14 are in the test project.

**CA1861** — "Prefer 'static readonly' fields over constant array arguments" (8 sites):

| File | Line | Code |
|---|---|---|
| `Console/CommandLineParserTests.cs` | 13 | `args.Positional.ShouldBe(new[] { "topics" });` |
| `Console/CommandLineParserTests.cs` | 21 | `args.Positional.ShouldBe(new[] { "topics", "extra" });` |
| `Console/CommandLineParserTests.cs` | 29 | `args.Positional.ShouldBe(new[] { "my shot" });` |
| `Console/CommandRegistryTests.cs` | 77 | `registry.All.Select(d => d.Name).ShouldBe(new[] { "alpha", "mid", "zeta" });` |
| `Diagnostics/BoundedLogTests.cs` | 32 | `log.Entries.ShouldBe(new[] { "one", "two", "three" });` |
| `Diagnostics/BoundedLogTests.cs` | 44 | `log.Entries.ShouldBe(new[] { "two", "three" });` |
| `Diagnostics/LogRetentionPolicyTests.cs` | 136 | `toDelete.ShouldBe(new[] { "session-20260102T000000000-222.log" });` |
| `Diagnostics/LogRetentionPolicyTests.cs` | 150 | `toDelete.ShouldBe(new[] { "session-20260101T000000000.log" });` |

Suggested remediation: hoist each literal to a `private static readonly string[]` field, which is what the rule asks for and reads fine in tests. Worth deciding deliberately whether CA1861 earns its keep in a test project at all — if the answer is no, silence it *in a test-scoped `.editorconfig` with a written justification*, not silently.

**CS8604** — "Possible null reference argument" (4 sites). All are `result.FailureReason` (a `string?`) passed to Shouldly's `ShouldContain(string actual, …)`:

| File | Line | Argument |
|---|---|---|
| `Console/CommandRegistryTests.cs` | 53 | `result.FailureReason.ShouldContain("nosuchcommand");` |
| `Console/CommandRegistryTests.cs` | 66 | `result.FailureReason.ShouldContain("kaboom");` |
| `Console/HelpCommandTests.cs` | 64 | `result.FailureReason.ShouldContain("nosuchcommand");` |
| `Screenshots/ScreenshotCommandTests.cs` | 77 | `result.FailureReason.ShouldContain("no viewport texture available");` |

Suggested remediation: `result.FailureReason.ShouldNotBeNull().ShouldContain(…)` (Shouldly 4.3.0 returns the non-null value from `ShouldNotBeNull()`). This strengthens the tests rather than merely quieting them — prefer it over a `!` null-forgiving operator, which would turn a null into a `NullReferenceException` instead of a readable assertion failure.

**CS8629** — "Nullable value type may be null" (2 sites), `Diagnostics/FrameTimeHistogramTests.cs`:

```csharp
stats.P95Ms.Value.ShouldBeInRange(9.9, 10.1);   // line 50
stats.P99Ms.Value.ShouldBeInRange(9.9, 10.1);   // line 51
```

Suggested remediation: `stats.P95Ms.ShouldNotBeNull().ShouldBeInRange(9.9, 10.1);` — same reasoning as CS8604.

### B. 8 warnings hidden by a project-wide suppression

`.editorconfig:40` carries `dotnet_diagnostic.CA1848.severity = none` ("use the LoggerMessage delegates"), with this justification:

```
# CA1848 (use LoggerMessage delegates) has no registered code fixer. dotnet format
# --verify-no-changes exits 2 whenever it's present regardless of whether an actual diff would
# result, which would make scripts/verify.sh's style stage permanently fail. Silenced rather than
# hand-rolling LoggerMessage delegates for a handful of non-hot-path log calls (constitution V).
```

I measured what this hides by re-enabling it at `warning` in a throwaway copy of the fixed tree. **8 CA1848 diagnostics appear, all in production source** (not tests):

| File | Line | Call |
|---|---|---|
| `src/Core/Console/CommandRegistry.cs` | 43 | `LogError(ILogger, Exception?, string?, params object?[])` |
| `src/Core/Console/CommandRegistry.cs` | 75 | `LogWarning(ILogger, Exception?, string?, params object?[])` |
| `src/Game/Main.cs` | 19 | `LogInformation(…)` |
| `src/Game/Main.cs` | 45 | `LogInformation(…)` |
| `src/Game/Main.cs` | 52 | `LogInformation(…)` |
| `src/Game/Autoloads/DevConsole.cs` | 205 | `LogWarning(…)` |
| `src/Game/Autoloads/ScreenshotHarness.cs` | 100 | `LogError(…)` |
| `src/Game/Infrastructure/GodotScreenshotService.cs` | 109 | `LogWarning(…)` |

Two things make this squarely part of this bug:

1. **CA1848 is the same defect class as the CA1873 warnings this fix just repaired** — both say "route this log call through `LoggerMessage`". The fix migrated 6 call sites to `[LoggerMessage]` and left 8 sibling call sites untouched only because a suppression hid them. `DevConsole.cs` and `ScreenshotHarness.cs` each had one call site fixed and another, in the same file, still flagged.
2. **The suppression's stated rationale is now half-obsolete.** It says LoggerMessage delegates were not worth hand-rolling — but this fix demonstrates the `[LoggerMessage]` source generator does the work, cleanly, with no hand-rolled delegates and no style-gate breakage.

**A real constraint the proper fix must handle:** I confirmed the tooling half of that comment still holds. With `CA1848 = warning`, `dotnet format NewGame1.sln --verify-no-changes --no-restore` **exits 2** even though it reports no actual formatting diff — so `scripts/verify.sh`'s style stage would fail. The clean sequence is therefore: **migrate all 8 call sites to `[LoggerMessage]` first, then remove the suppression** — with zero CA1848 diagnostics left to trip on, `dotnet format` should exit 0. (That last step is reasoned from the observed mechanism, not yet measured; verify it before relying on it.)

## Checks Performed

| Check | Command / Action | Result | Notes |
|-------|------------------|--------|-------|
| Reproduction of the 7 named warnings (post-fix) | `dotnet build NewGame1.csproj -v:q -t:Rebuild` | pass | `0 Warning(s), 0 Error(s)`. |
| Pre-fix control | Same command against a detached `git worktree` at `HEAD` (fdb64e2) | pass | Reproduced all 7 — confirms the zero is the fix's doing, not analyzers going missing. |
| **Whole-solution warning count** | `dotnet build NewGame1.sln -v:n -t:Rebuild` | **fail (vs. issue goal)** | **14 warnings remain.** 21 → 14 across the fix; the 7-warning delta is exactly the assessed set. |
| **Suppressed-warning audit** | Re-enabled `CA1848` at `warning` in a scratchpad copy of the fixed tree, rebuilt | **fail (vs. issue goal)** | **8 further warnings surface**, all in production source. |
| Non-source warnings (MSBuild / NuGet / SDK) | `dotnet build NewGame1.sln -v:n -t:Rebuild --no-incremental`, filtered for `MSB*`/`NU*`/`NETSDK*` | pass | None. All 14 are C# compiler/analyzer diagnostics. |
| **Incremental builds hide warnings** | `dotnet build NewGame1.sln -v:q` run twice, then with `--no-incremental` | **confirmed** | Cold: `14 Warning(s)`. Immediately re-run warm: **`0 Warning(s)`**. With `--no-incremental`: `14 Warning(s)` again. Same tree, same command, three different answers. |
| **Warnings-as-errors ratchet** | `dotnet build NewGame1.sln --no-incremental -warnaserror -v minimal` | **fails, as intended** | **Exit code 1**; all 14 warnings promoted to `error CA1861` / `error CS8604` / `error CS8629`. This is the enforcement mechanism — verified working today, no project-file changes needed. |
| Ratchet's blind spot | Same flag against `NewGame1.csproj` and `src/Core/NewGame1.Core.csproj` alone | **passes — 0 errors** | Both projects hold CA1848 sites, but `-warnaserror` reports nothing: `severity = none` stops the diagnostic being emitted at all, so there is no warning to promote. See below. |
| `dotnet format` under re-enabled CA1848 | `dotnet format … --verify-no-changes --no-restore` in the scratchpad copy | informational | **Exit code 2** with no actual formatting diff — confirms naively lifting the suppression breaks verify.sh's style stage. |
| Generated-code correctness (all 6 migrated sites) | Rebuild with `-p:EmitCompilerGeneratedFiles=true`, inspect `LoggerMessage.g.cs` | pass | Levels and templates verbatim; `LogUnhandledException` is `Critical` and routes the `Exception` into the exception slot; `LogFrameTimeStatistics` (10 args, past `Define`'s 6-arg cap) correctly uses the state-struct + `logger.Log(…)` path. All under `IsEnabled` guards. |
| Runtime log-output equivalence | Compared post-fix session logs against pre-fix logs from 23:44 / 23:48 | pass | Identical rendering, including `:F3` formatting and the pre-existing `("Final")` enum quoting (present before the fix — not introduced by it). |
| Regression suite | `scripts/verify.sh` | pass | All 6 stages green in 7.6s. Note this gate does **not** fail on warnings — see Residual Risks. |
| Lint / type-check | verify.sh "Code style" stage (`dotnet format --verify-no-changes`) | pass | Migration introduced no style violations. |
| Screenshot inspection | Read `artifacts/main.png` | pass | Placeholder scene renders correctly; golden compare also passed. |
| `.cs.uid` sidecars | `git status --short` | n/a | No new `.cs` files, so none needed. |
| Unhandled-exception path at runtime | — | not-run | Would require deliberately crashing the game, i.e. modifying source — barred by this command's guardrails. Verified at the generated-code level instead. |

## Output Excerpts

Assessed scope, post-fix — clean:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Whole solution, post-fix — **not** clean:

```
tests/Core.Tests/Console/CommandLineParserTests.cs(13,34): warning CA1861
tests/Core.Tests/Console/CommandRegistryTests.cs(53,9):   warning CS8604
tests/Core.Tests/Diagnostics/FrameTimeHistogramTests.cs(50,9): warning CS8629
        … (14 total)
    14 Warning(s)
    0 Error(s)
```

Same tree with `dotnet_diagnostic.CA1848.severity = none` lifted to `warning`:

```
src/Core/Console/CommandRegistry.cs(43,13): warning CA1848: For improved performance, use the
    LoggerMessage delegates instead of calling 'LoggerExtensions.LogError(…)'
src/Game/Main.cs(19,9): warning CA1848: …
        … (8 total, all production source)
    22 Warning(s)
```

Runtime log, post-fix — migrated call sites render unchanged:

```
[INF] NewGame1.Autoloads.DevConsole: Dev console gating: exportedRelease=false editorRun=true devConsoleFlag=false allowed=true
[INF] NewGame1.Autoloads.ScreenshotHarness: Screenshot harness replaced existing screenshot /workspace/artifacts/main.png
[INF] NewGame1.Autoloads.PerfMonitor: Frame time statistics ("Final"): average=13.102ms p95=16.600ms p99=16.600ms worst=100.000ms samples=10 lowConfidence=true drawCalls=2 processMemory=538.9 MB videoMemory=13.3 MB
```

## Residual Risks

- **Nothing in the repo fails a build on warnings.** `scripts/verify.sh`'s Build stage runs plain `dotnet build NewGame1.sln` and passes on a 14-warning build, which is how warnings drift back to zero attention the moment nobody reads the console. The fix is a flag, not a refactor: `dotnet build NewGame1.sln --no-incremental -warnaserror -v minimal` — verified to exit 1 today with all 14 promoted to errors. `--no-incremental` matters as much as `-warnaserror`: an incremental build skips up-to-date projects and reports none of their warnings, so a clean incremental run proves nothing.
- **`-warnaserror` cannot see `.editorconfig` suppressions — this is the one hole it leaves.** Verified directly: the same flag against `NewGame1.csproj` and `src/Core/NewGame1.Core.csproj` exits 0 with `0 Warning(s), 0 Error(s)`, even though those two projects contain all 8 CA1848 sites. `severity = none` suppresses the diagnostic at the analyzer, before MSBuild ever sees a warning to promote. So the ratchet enforces *"no warning is ignored"* but not *"no warning is hidden"* — the two failure modes in this issue need two different controls, and adding the flag without also clearing the suppression would leave the second one fully intact and now harder to notice, since the build would be reporting green.
- **`.editorconfig` suppressions are invisible in build output by construction.** `CA1848` is currently the only one, but any future `severity = none` line silently shrinks the warning surface. Worth a convention that each such line carries a justification *and* an expiry condition — the existing CA1848 comment is a good template, and it is now partly outdated.
- **Three of the six migrated call sites were not exercised at runtime.** `LogCommandSucceeded` needs interactive dev-console input (no headless command-execution flag exists), `LogUnhandledException` needs a deliberate crash, and `LogScreenshotWritten` only fires on a first capture (its sibling `LogScreenshotReplaced` did fire, and `LogScreenshotWritten` appears in a pre-fix log at 23:44). All three verified at the generated-code level — strong, but not observed output.
- **`EventId` changed from 0 to generator-assigned hashes** at all six migrated sites. Invisible in current output because `Logging.LogLineTemplate` does not render `EventId`, but any consumer filtering on event id would see the change.
- **The `#pragma warning disable CS0618` in `Logging.cs` suppresses rather than resolves** the obsolete-API use. This one is well-justified and documented in fix.md (Serilog 7.0.0 offers no public non-obsolete alternative), but it is a suppression and belongs on the same audit list as the `.editorconfig` entry.
- Minor bookkeeping: assessment.md cites `DevConsole.cs:125` and `:195`; on current `HEAD` those sites are at lines 132 and 202 (drift from `a40c2df`/`fdb64e2`). Same call sites, no impact.

## Recommendation

**Do not close the bug. Re-run `/speckit-bug-assess` for issue #3 with the corrected scope** — "the solution builds with zero warnings, and no warning is suppressed without a written, current justification" — using the 22-warning inventory above as the input. The existing fix is good work and should be kept as-is; it needs no rework, only extension.

Suggested sequencing for the follow-up fix:

1. **The 14 in `tests/Core.Tests/`** — mechanical and low-risk (8× CA1861 hoist-to-field, 4× CS8604 and 2× CS8629 resolved with `ShouldNotBeNull()`, which strengthens the assertions rather than just silencing them). Do this first; it is independent of everything else.
2. **The 8 CA1848 sites** — migrate to `[LoggerMessage]`, reusing the pattern this fix established (including the deviations fix.md already documented: extension methods need a non-generic static host, and CA1873's guard recognition depends on extension-method call syntax). *Then* delete `dotnet_diagnostic.CA1848.severity = none` from `.editorconfig`, and confirm `dotnet format --verify-no-changes` exits 0 with no diagnostics left to trip on.
3. **Add the ratchet** — change `scripts/verify.sh`'s `stage_build` from `dotnet build NewGame1.sln` to:

   ```bash
   dotnet build NewGame1.sln --no-incremental -warnaserror -v minimal
   ```

   Verified working: it exits 1 today and turns all 14 remaining warnings into build errors. Do this **after** step 1, or the gate is red from the moment it lands (which is arguably the honest state, but it blocks every other stage behind it). No `Directory.Build.props` or per-project `TreatWarningsAsErrors` is needed — the flag covers every project in the solution from one place, and keeping it in the gate rather than the project files means a developer's inner-loop `dotnet build` stays fast and non-blocking while CI and `verify.sh` stay strict.
4. **Close the ratchet's blind spot** — `-warnaserror` is silent on anything set to `severity = none`, so it will never re-flag CA1848 (or any future suppression) on its own. Pair it with a standing rule that every `.editorconfig` suppression carries a justification and an expiry condition, and re-audit them by temporarily lifting each to `warning` — the measurement in section B above is the repeatable procedure.

Also worth correcting the record on the process failure this exposed: the assessment treated a screenshot of an editor Problems panel as the definition of scope rather than as one symptom of it, and never ran a whole-solution build or audited existing suppressions. For an issue whose entire premise is "warnings are being ignored without telling me", establishing the full warning inventory — emitted *and* suppressed — is the first step of the assessment, not an optional extra.
