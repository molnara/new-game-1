# Bug Assessment: MSBuild warnings — whole-solution scope (re-assessment)

- **Slug**: msbuild-warnings
- **Created**: 2026-09-03 (supersedes the 2026-09-02 assessment committed in `86a5cff`)
- **Source**: https://github.com/molnara/new-game-1/issues/3 (host: github.com, policy branch: allowlisted) — **not re-fetched in this pass**; the issue body was fetched and transcribed in the superseded assessment, and that transcription is reused here. Additional input: `./test.md`, the verification report from the first fix cycle, which recommended this re-assessment.
- **Verdict**: valid
- **Severity**: medium

## Why This Assessment Was Rewritten

The first assessment scoped issue #3 to the 7 warnings visible in the reporter's editor "Problems" panel screenshot. `/speckit-bug-fix` repaired exactly those 7. `/speckit-bug-test` then returned **partial** and found the screenshot was a *sample* of the warning surface, not its *definition*: **22 warnings remain outstanding**, and none of them were ever assessed.

The screenshot was a symptom of the problem, not a specification of it. For an issue whose premise is "warnings are being ignored", the first step is establishing the complete warning inventory — emitted *and* suppressed. This assessment does that.

The prior fix is sound and needs no rework. It is kept as-is; this assessment covers only what remains.

## Report (summarized)

Issue #3, "Fix MSBuild Warnings" (opened 2026-09-02, label `bug`). The body carries no prose — only a screenshot of an editor Problems panel listing 7 diagnostics across 4 files in `src/Game/`. The acceptance criterion the title states is unqualified: **the build produces no warnings.**

Read literally and correctly, that criterion covers the whole solution and is not satisfied by hiding diagnostics from the build.

## Symptom

A cold whole-solution build emits **14 warnings**, and a further **8 are suppressed** project-wide before MSBuild ever sees them. The build succeeds and the game runs correctly — there is no functional misbehavior — but the warning count is not zero, and, more importantly, three independent mechanisms make that fact easy to miss.

**Expected**: `dotnet build NewGame1.sln` reports 0 warnings, with no diagnostic silenced except under a written, current justification.

## Reproduction

1. `dotnet build NewGame1.sln --no-incremental -v:n` → **14 warnings**, all in `tests/Core.Tests/`.
2. `dotnet build NewGame1.sln -v:q` immediately afterwards (warm) → **`0 Warning(s)`**. Same tree, same command, different answer.
3. In a scratch copy, change `.editorconfig:40` from `dotnet_diagnostic.CA1848.severity = none` to `= warning` and rebuild → **8 further warnings**, all in production source.

All three reproduced in this assessment on the current working tree (which includes the uncommitted first-cycle fix). No `[NEEDS CLARIFICATION]` items.

## Current Warning Inventory (measured, not inherited)

| | Count | Where | Status |
|---|---|---|---|
| Warnings named in the superseded assessment | 7 | `src/Game/` (4 files) | **fixed** (uncommitted working tree) |
| Warnings still emitted | 14 | `tests/Core.Tests/` | **outstanding** |
| Warnings hidden by `.editorconfig` | 8 | `src/Core/` + `src/Game/` (5 files) | **outstanding** |
| **Total outstanding** | **22** | | |

### A. 14 emitted warnings — `tests/Core.Tests/`

`src/Core/NewGame1.Core.csproj` and `NewGame1.csproj` are both clean. All 14 are in the test project.

**CA1861** — "Prefer 'static readonly' fields over constant array arguments" (8 sites):

| File | Line,Col |
|---|---|
| `Console/CommandLineParserTests.cs` | 13,34 · 21,34 · 29,34 |
| `Console/CommandRegistryTests.cs` | 77,51 |
| `Diagnostics/BoundedLogTests.cs` | 32,30 · 44,30 |
| `Diagnostics/LogRetentionPolicyTests.cs` | 136,27 · 150,27 |

All are `…ShouldBe(new[] { … })` assertion literals.

**CS8604** — "Possible null reference argument" (4 sites). Every one is a `string? FailureReason` passed to Shouldly's `ShouldContain(string actual, …)`:
`Console/CommandRegistryTests.cs:53,9` · `:66,9` · `Console/HelpCommandTests.cs:64,9` · `Screenshots/ScreenshotCommandTests.cs:77,9`

**CS8629** — "Nullable value type may be null" (2 sites), `Diagnostics/FrameTimeHistogramTests.cs:50,9` and `:51,9` — `stats.P95Ms.Value` / `stats.P99Ms.Value` on `double?`.

### B. 8 hidden warnings — CA1848, all production source

Measured by rebuilding a scratchpad copy of the current tree with the suppression lifted to `warning`:

| File | Line,Col | Call |
|---|---|---|
| `src/Core/Console/CommandRegistry.cs` | 43,13 | `_logger.LogError(ex, …)` |
| `src/Core/Console/CommandRegistry.cs` | 75,13 | `_logger.LogWarning(ex, …)` |
| `src/Game/Main.cs` | 19,9 · 45,21 · 52,17 | `LogInformation(…)` startup/shutdown |
| `src/Game/Autoloads/DevConsole.cs` | 205,13 | `LogWarning(…)` |
| `src/Game/Autoloads/ScreenshotHarness.cs` | 100,17 | `LogError(…)` |
| `src/Game/Infrastructure/GodotScreenshotService.cs` | 109,54 | `Logging.TryFor<T>()?.LogWarning(ex, …)` |

CA1848 is the **same defect class** as the CA1873 warnings the first fix repaired — both say "route this through `LoggerMessage`". The fix migrated 6 call sites and left 8 siblings untouched only because a suppression hid them; `DevConsole.cs` and `ScreenshotHarness.cs` each had one site fixed and another, in the same file, still flagged.

## Suspected Code Paths

The 22 diagnostic sites above are the code paths. The three sites that let them go unnoticed:

- `scripts/verify.sh:36-38` — `stage_build()` runs bare `dotnet build NewGame1.sln`: no `--no-incremental`, no `-warnaserror`. It passes on a 14-warning build, and on a warm tree does not even print them.
- `.editorconfig:36-40` — `dotnet_diagnostic.CA1848.severity = none`. The only `severity = none` line in the repo. Its justification is now half-obsolete (see below).
- `Directory.Build.props:7-9` — `EnableNETAnalyzers=true`, `AnalysisLevel=latest-recommended`, `TreatWarningsAsErrors=false`. Analyzers are on; nothing makes their output blocking.

## Root Cause Hypothesis

**Confidence: high** — every claim below was measured in this session, not inferred.

There is no single code defect. The 22 warnings are ordinary, independently-introduced diagnostics; the *bug* is that nothing in the repo forces them to zero or keeps them visible. Three mechanisms hide them, and they compose:

1. **Incremental builds report nothing.** Up-to-date projects are not recompiled, so their diagnostics are never re-emitted. Verified: cold `14 Warning(s)`, warm `0 Warning(s)`, same tree. Whoever runs a build second sees a clean one. This alone explains most of "warnings that never get mentioned".
2. **`severity = none` suppresses at the analyzer**, before MSBuild sees a warning at all — so it is invisible in build output *by construction*, and invisible to `-warnaserror` too (there is nothing to promote).
3. **No gate fails on warnings**, so drift back to nonzero costs nothing.

The CA1848 suppression's stated rationale is now **half true**. Its tooling half still holds — verified: with 8 live CA1848 sites, `dotnet format NewGame1.sln --verify-no-changes --no-restore` **exits 2** while reporting no formatting diff, which would indeed break verify.sh's style stage. Its judgment half ("not worth hand-rolling LoggerMessage delegates") is now obsolete: the first fix demonstrated the `[LoggerMessage]` source generator does this cleanly, with no hand-rolled delegates.

That ordering constraint is the one real sequencing hazard in this bug: **the suppression cannot be lifted before the call sites are migrated**, or the style gate goes red.

## Proposed Remediation

**Preferred** — four steps, in this order. Steps 1 and 2 are independent of each other; step 3 must follow step 1, and the suppression removal inside step 2 must follow that step's migration.

**1. Clear the 14 test warnings.** Mechanical and low-risk.
- 8× CA1861: hoist each `new[] { … }` literal to a `private static readonly string[]` field.
- 4× CS8604: `result.FailureReason.ShouldNotBeNull().ShouldContain(…)`. Shouldly 4.3.0's `ShouldNotBeNull()` returns the non-null value, so this chains. Prefer it over `!` — the null-forgiving operator converts a null into a `NullReferenceException` instead of a readable assertion failure, so this *strengthens* the tests rather than silencing them.
- 2× CS8629: `stats.P95Ms.ShouldNotBeNull().ShouldBeInRange(9.9, 10.1)`, same reasoning.

Worth deciding deliberately whether CA1861 earns its keep in a test project. If not, silence it in a **test-scoped `.editorconfig` with a written justification** — not silently, and not solution-wide.

**2. Migrate the 8 CA1848 sites to `[LoggerMessage]`, then delete the suppression.** Reuse the pattern the first fix established, including the two deviations `fix.md` documented (extension-method log helpers need a non-generic static host class; guard recognition depends on extension-method call syntax). Host-class notes per site:
- `Main` is already `partial` — add the methods directly.
- `CommandRegistry` and `GodotScreenshotService` are `sealed class`, **not** `partial` — either add `partial`, or declare an `internal static partial class …Log` host in the same file (the pattern `PerfMonitor.cs` already uses).
- `GodotScreenshotService.cs:109` is `Logging.TryFor<T>()?.LogWarning(…)` — the same null-conditional shape the fix already had to rewrite in `ScreenshotHarness`; expect to hoist to a local and use an explicit `is not null` check.
- `src/Core` already references `Microsoft.Extensions.Logging.Abstractions` 10.0.11, so the generator is available in both projects.

Then remove `.editorconfig:40` and confirm `dotnet format --verify-no-changes` exits 0. **This last part is reasoned from the observed mechanism, not yet measured** — the exit-2 behavior was verified *with* live diagnostics; that it returns to 0 once none remain follows from the same mechanism but should be confirmed before relying on it.

**3. Add the ratchet.** Change `scripts/verify.sh:37` from `dotnet build NewGame1.sln` to:

```bash
dotnet build NewGame1.sln --no-incremental -warnaserror -v minimal
```

Verified today: exits **1**, promoting all 14 to `error CA1861` / `error CS8604` / `error CS8629`. `--no-incremental` matters as much as `-warnaserror` — a warm build skips up-to-date projects and proves nothing. Keep this in the gate rather than in `Directory.Build.props`: one place covers every project, and a developer's inner-loop `dotnet build` stays fast and non-blocking while `verify.sh` and CI stay strict. Land it **after** step 1, or the gate is red the moment it arrives.

**4. Close the ratchet's blind spot.** `-warnaserror` is silent on anything at `severity = none` — verified: run against `NewGame1.csproj` and `src/Core/NewGame1.Core.csproj`, which hold all 8 CA1848 sites, it exits 0. The ratchet enforces *"no warning is ignored"* but not *"no warning is hidden"*; those are two failure modes needing two controls. Adopt a convention that every `severity = none` line carries a justification **and an expiry condition**, and re-audit them periodically by lifting each to `warning` — the section B measurement is the repeatable procedure. Adding step 3 without doing step 2 would leave this failure mode fully intact and *harder* to notice, since the build would then report green.

**Alternatives considered**:
- *Set `TreatWarningsAsErrors=true` in `Directory.Build.props`.* Covers every build including the inner loop, but makes routine editing painful and tempts per-project `NoWarn` escapes — which is the exact failure mode this issue is about. The gate-level flag gets the enforcement without the friction.
- *Fix the 14 and leave CA1848 suppressed.* Cheaper, and the build would honestly report 0. But it leaves 8 known diagnostics hidden behind a rationale that is now half-obsolete, which fails the issue's actual intent.

**Files likely to change**:
- `tests/Core.Tests/Console/CommandLineParserTests.cs`, `Console/CommandRegistryTests.cs`, `Console/HelpCommandTests.cs`, `Diagnostics/BoundedLogTests.cs`, `Diagnostics/LogRetentionPolicyTests.cs`, `Diagnostics/FrameTimeHistogramTests.cs`, `Screenshots/ScreenshotCommandTests.cs`
- `src/Core/Console/CommandRegistry.cs`, `src/Game/Main.cs`, `src/Game/Autoloads/DevConsole.cs`, `src/Game/Autoloads/ScreenshotHarness.cs`, `src/Game/Infrastructure/GodotScreenshotService.cs`
- `.editorconfig` (remove line 40; possibly add a test-scoped section)
- `scripts/verify.sh` (line 37)

**Tests to add or update**:
- No new behavioral test — this is warning cleanup with no intended behavior change. The 6 assertion rewrites in step 1 strengthen existing tests in place.
- The ratchet in step 3 **is** the regression test: it makes "the solution builds clean" a gate rather than a habit. Verify it fails before step 1 and passes after.
- After the CA1848 migration, confirm generated code matches the prior fix's verified pattern (`-p:EmitCompilerGeneratedFiles=true`, inspect `LoggerMessage.g.cs`) and that log rendering is unchanged.

## Risks & Considerations

- **`EventId` changes at every migrated site**, from 0 to a generator-assigned hash. Invisible today because `Logging.LogLineTemplate` does not render `EventId`, but any consumer filtering on event id would see it. Already true of the first fix's 6 sites.
- **Ordering hazard**: lifting the CA1848 suppression before migrating breaks verify.sh's style stage (`dotnet format` exit 2) — measured. Landing the ratchet before step 1 blocks every stage behind a red build.
- **`GodotScreenshotService.cs:109` and `DevConsole.cs:205` are error/warning paths** that are hard to exercise headlessly. Expect generated-code-level verification rather than observed output, as the first cycle did for 3 of its 6 sites.
- **CA1861 in tests is a genuine judgment call**, not a defect. Hoisting 8 assertion literals to fields costs readability for no measurable gain in a test project. Suppressing it test-scoped with a justification is a legitimate outcome; doing so silently is not.
- No security, migration, performance, or observability impact. Message content and log levels stay unchanged.
- **Uncommitted state**: the first cycle's fix is in the working tree, not committed (`fix.md`, `test.md` untracked; 4 modified `.cs` files). Every measurement in this assessment was taken against that tree. Commit it before starting, or the baselines here will not match.
- No new `.cs` files are anticipated, so no `.cs.uid` sidecars — but per CLAUDE.md, run `godot --headless --import` and commit any that appear if that changes.

## Open Questions

- [NEEDS CLARIFICATION: Should CA1861 be fixed in the test project (8 hoists) or suppressed test-scoped with a written justification? Both satisfy "no unjustified suppression"; this is a taste call for the developer.]
- [NEEDS CLARIFICATION: Should the ratchet also land in CI, or is `scripts/verify.sh` the only gate that matters for this repo?]
