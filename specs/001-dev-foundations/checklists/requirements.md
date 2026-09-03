# Specification Quality Checklist: Developer Foundations

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-01
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`.

### Validation record (iteration 1 — initial spec)

- **No implementation details**: the spec names no language, engine, framework, or library. The
  concrete nouns it does use — `artifacts/`, the user data `logs` folder, PNG, the backtick key —
  come from the feature description itself and are observable outcomes the developer checks, not
  design choices. Passed.
- **Testable and unambiguous**: one wording fix applied during validation. FR-004 originally read
  "without wiring it through global state by hand", which described a mechanism rather than an
  observable outcome; it was rewritten to state the outcome.
- **No clarification markers**: zero were needed at that point. Eight open questions were resolved
  as documented defaults in the Assumptions section instead.
- **Scope bounded**: an Out of Scope section was added beyond the template's sections.

### Validation record (iteration 2 — after /speckit-clarify)

Five clarifications integrated; all 16 items still passing. Two phrases ("compiled out", "gated at
compile time") were reworded to keep the *no implementation details* item honestly checked.

### Validation record (iteration 3 — performance profiling brought into scope)

**Result: 16/16 → 14/16.** Two items regressed, both for the same reason, and both by design.

- **REGRESSED — No [NEEDS CLARIFICATION] markers remain**: three markers were added deliberately, on
  FR-045, FR-046, and FR-047. Each was left open rather than defaulted because each has two
  defensible answers with materially different consequences, and guessing would bake a decision into
  the requirements that the developer never actually made:
  - **FR-045 (when sampling runs)** — always-on makes every session's log useful but costs something
    in every session including shipped builds; overlay-only costs nothing but loses the data
    precisely when nobody anticipated needing it.
  - **FR-046 (when statistics are written)** — shutdown-only is clean but a killed session, the one
    most worth investigating, would leave nothing behind.
  - **FR-047 (which memory figure)** — process, engine-tracked, and video memory diverge
    substantially and answer different questions.
- **REGRESSED — Requirements are testable and unambiguous**: unchecked as a direct consequence of
  the three markers above, not for any separate defect. The other 44 functional requirements are
  unaffected. Both items clear together once the three questions are answered.
- **Acceptance scenarios**: Story 5 scenario 4 was reworded to say "and frame sampling was active
  during it", so the scenario does not silently presume an answer to FR-045. Without that change the
  spec would have contradicted its own open question.
- **Scope bounded**: still passing. The Out of Scope entry was narrowed rather than deleted — the
  live overlay and end-of-session statistics moved into scope, while per-system timing breakdowns,
  flame graphs, allocation tracking, and cross-session trend history remain explicitly excluded, so
  "profiling" does not expand without limit.
- **Success criteria measurable**: still passing. SC-011 (5 seconds to read the numbers), SC-012
  (findable in one search), and SC-013 (under 1 ms of distortion) are all checkable.

### Validation record (iteration 4 — after /speckit-clarify session 2026-09-02)

**Result: 14/16 → 16/16.** Both regressions from iteration 3 cleared, as predicted, and for the
reason predicted: the three markers were answered.

- **NEWLY PASSING — No [NEEDS CLARIFICATION] markers remain**: FR-045 (sampling always runs from
  startup), FR-046 (periodic snapshots plus a final record), and FR-047 (two labelled memory
  figures — process and video) are all resolved. Zero markers remain in the spec.
- **NEWLY PASSING — Requirements are testable and unambiguous**: beyond the three markers, two vague
  adjectives were quantified in the same session. FR-039's "a cadence a human can actually read"
  became a stated ~4 Hz refresh, and FR-044's "fewer samples than a percentile meaningfully needs"
  became 1000 frames. Both were unwritable as tests before and are checkable now.
- **Consistency repairs made while integrating**: Story 5 scenario 4 had been hedged with "and frame
  sampling was active during it" to avoid presuming FR-045's answer; with sampling now always on,
  the hedge was removed. FR-046b was added because periodic statistics written at Information level
  would have been batched under FR-005's flush policy and lost on exactly the abrupt kill FR-046
  exists to survive — the decision would not have delivered what it promised without it.
- **Requirements added by this session**: FR-039a, FR-039b, FR-045a, FR-046a, FR-046b, plus a ninth
  Story 5 acceptance scenario and a killed-mid-run edge case.

### Recommended next step

Re-run `/speckit-plan`. `plan.md` predates the profiling story entirely — it has no overlay in its
Project Structure, no profiling row in its constitution gate table, and no research into where the
engine's frame-time, draw-call, and memory counters come from. `/speckit-tasks` run against the
current plan would silently generate no profiling work.

### Constitution alignment

The project constitution (v1.0.0) already assumes this feature's outputs exist — logging through a
per-system logger, a dev console command for every gameplay system, screenshot evidence, and a
verification command as the quality gate. Principle III's "every gameplay system exposes a console
command" is satisfied by the profiling story's toggle and statistics commands.

### Validation record (iteration 3 — constitution v1.1.0, principle VI)

Re-validated after the constitution added **VI. Standards Are Automated** and expanded the
`verify.sh` gate to include `dotnet format`. The spec was amended to match; every box above still
holds.

- **Requirements added by this session**: FR-028b (style enforced by a machine check against a
  checked-in configuration, reporting without modifying source) and SC-014 (a style violation in any
  checked-in source file is reported with file, line and rule, and nothing is rewritten by the
  check). FR-028's stage list and the "Verification stages" assumption were amended to include the
  stage.
- **Still passing — no implementation details**: FR-028b names no tool, language, or file. It says
  "a checked-in configuration file" and "a machine check", which is the outcome; `dotnet format` and
  `.editorconfig` appear only in `plan.md` and the contracts, where implementation belongs.
- **Still passing — success criteria are measurable**: SC-014 is checkable by introducing one
  violation and reading the output, and it deliberately asserts the negative half too (no file
  modified), because a check that silently reformats would otherwise satisfy the positive half.
- **Scope grew, and it is recorded**: one verification stage and one configuration file. The plan's
  "Constitution v1.1.0 delta" section carries the reasoning and names the work; this is the second
  time the constitution has outranked the spec on this feature, after golden images in iteration 2.

### Superseded

The iteration-2 "Recommended next step" (re-run `/speckit-plan` for the profiling story) has been
carried out — `plan.md` now covers the overlay, its constitution rows, and research R11/R12. The
"Constitution alignment" note below was written against constitution v1.0.0; v1.1.0 adds principle
VI, whose effect on this feature is recorded in the plan's delta section and in iteration 3 above.

### Validation record (iteration 4 — engine test tier, deferral reversed)

The 2026-09-01 clarification "fast engine-free tests only for now" was reversed on 2026-09-02: the
developer wants both tiers standing. Re-validated after the amendment; every box above still holds.

- **Requirements added**: FR-028c (two distinct tiers, both runnable independently and both stages of
  verification), FR-028d (the engine tier runs unattended without a display), FR-028e (console
  keystroke isolation and single-frame open belong to the engine tier, not a manual checklist), plus
  SC-015 (a broken test in either tier fails the command) and SC-016 (console open/close verified
  without a human).
- **Requirements amended**: FR-028's stage list; FR-028a narrowed to stages not yet foreseen, since
  the tier it was written to accommodate now exists; the "Verification stages" assumption. The
  superseded clarification is marked in place rather than deleted, so the reversal is legible.
- **Removed from Out of Scope**: the engine-based test tier bullet.
- **What the original deferral got wrong**: it rested on "nothing in this feature needs an engine
  test", while `quickstart.md` Story 2 simultaneously routed FR-011 and SC-007 to an eight-step
  manual checklist run on the host. Those are node and input behavior with no Core representation —
  constitution II's stated condition for the slow tier. The work was never absent, only unautomated.
  A spec whose Out of Scope and whose validation guide disagree about the same behavior is the
  inconsistency `/speckit-analyze` looks for, and it survived two prior iterations unnoticed.
- **Still passing — the slow tier stays the exception**: five slow-tier cases are named, each with a
  stated reason it cannot be a Core test. Registry, parser, help, history, retention and percentiles
  all remain fast-tier.
- **Open item, deliberately marked**: research R14 records that GoDotTest has not been runtime-spiked
  under `xvfb-run` here, unlike every other engine claim in that document. It is blocked on
  permission to create `scenes/Main.tscn`.

### Validation record (iteration 5 — R14 spike run)

The engine-tier spike deferred in iteration 4 was run on 2026-09-02 with the developer's permission
to create `scenes/Main.tscn`. Spike files were removed afterwards; the tree is back to its pre-spike
state. No requirement changed — the spike confirmed the tier is buildable as specified — but two
contract details were corrected.

- **Corrected**: the Godot test stage had provisionally inherited research R4's "exit codes are not
  trustworthy". Measured, GoDotTest sets the exit code deliberately (0 pass, 1 fail), so R4 does not
  apply to this stage. The contract said the right thing for the wrong reason.
- **Corrected**: argument delivery. Reading `OS.GetCmdlineArgs()` as the package README shows does
  not see arguments after `--`; the tests silently do not run and the process hangs. The tier reads
  `OS.GetCmdlineUserArgs()`, matching the screenshot harness convention.
- **New risk, now covered**: a run executing zero tests exits 0 and reports success. FR-028c is
  satisfiable by a stage that never runs a test, so the contract now requires asserting a non-zero
  passed count. SC-015 ("a broken test in either tier fails the command") already implied this;
  `quickstart.md` now exercises it explicitly. This is the second gate in this feature that could
  have shipped green while verifying nothing — see R13 for the first.
- **Resolved, not handed off**: whether Godot's generated `*.cs.uid` files are committed. Tested by
  renaming a script with and without its `.uid`; without it, a new UID is minted and the old
  reference is dead. They are committed, `.gitignore` is unchanged, and the expected additions are
  named in the plan (research R15).
