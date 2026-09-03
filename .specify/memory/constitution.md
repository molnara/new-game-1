<!--
Sync Impact Report
==================
Version change: 1.1.0 → 1.1.1
Bump rationale: PATCH. The zero-warning rule clarifies an existing gate rather than adding a
new one: `scripts/verify.sh` already had to pass before a task could be reported complete, and
the build is already warning-gated. This amendment states in prose what "pass" means for
warnings and names `.editorconfig` as the only place a suppression may live. No principle is
added, removed, or redefined, and no new obligation is created beyond what the existing gate
already enforced.

Modified principles:
  - None renamed, added, or redefined.

Added sections: none

Removed sections: none

Other changes:
  - Development Workflow & Quality Gates: added the zero-warning completion rule — warnings are
    fixed or suppressed in `.editorconfig` with a written justification, never left in place.
    Scope stated as build, analyzer, and Godot runtime warnings, matching the reporting rule
    already in `CLAUDE.md`.

Deferred TODOs: none. All placeholders resolved.
-->

# NewGame1 Constitution

NewGame1 is a small 2D game built in Godot 4.7 (.NET) with C#, developed by a solo developer
working with Claude Code. These principles exist to keep that arrangement productive: fast
feedback, evidence over assumption, and no more machinery than the game needs.

## Core Principles

### I. Core/Adapter Separation (NON-NEGOTIABLE)

All game rules, state, math, and data MUST live in `src/Core`, a plain C# library with no
reference to Godot. Godot scripts in `src/Game` MUST be thin adapters: they read input, call
into Core, and update nodes. Adapters MUST NOT contain game rules, and Core MUST NOT contain
`using Godot`.

When a Core feature needs an engine service (time, randomness with engine seeding, file access,
audio, scene loading), Core MUST define the interface and `src/Game/Infrastructure` MUST provide
the implementation. Dependencies point inward only: `src/Game` depends on `src/Core`; `src/Core`
depends on neither Godot nor `src/Game`.

**Rationale**: Core is testable in milliseconds without an engine, and the game logic stays
portable across engine versions and rewrites of the presentation layer.

### II. Test-First, Two Tiers

Every Core feature MUST ship with xUnit tests in `tests/Core.Tests`, written before or alongside
the implementation — never bolted on after the feature is reported done. Node behavior that
genuinely cannot be pushed into Core gets a GoDotTest in `tests/Game.Tests`.

Bugs MUST be fixed by first writing the failing test that reproduces them, then making it pass.
A bug fix without a reproducing test is incomplete.

The fast tier is the default; the slow tier is the exception. Before writing a `Game.Tests` test,
the author MUST first ask whether the logic under test could move into Core instead.

**Rationale**: the fast tier catches most bugs at negligible cost; the slow tier exists only for
what truly needs the engine, and it stays small so it stays runnable.

### III. Observability by Default

Every system MUST log through `Logging.For<T>()` with structured messages at appropriate levels.
`GD.Print` MUST NOT be used outside `src/Game/Infrastructure`.

Every gameplay system MUST expose at least one dev console command for inspecting or manipulating
its state. Errors MUST NOT be swallowed silently: an exception is either handled with a logged
explanation or allowed to propagate. Empty `catch` blocks and bare `catch { }` are prohibited.

**Rationale**: a solo dev debugs by reading logs and poking the console, not by attaching a
debugger to a running game.

### IV. Visual Verification

Any change affecting what is rendered MUST be verified by a screenshot captured via
`scripts/screenshot.sh`, and that screenshot MUST be read and confirmed by the implementer before
the task is reported done. "The build passed" is not visual verification; neither is a screenshot
that was captured but not looked at.

Main scenes have golden reference screenshots. A change that alters a golden image MUST update it
intentionally in the same PR, with a note in the PR description explaining what changed and why.

**Rationale**: the developer cannot see the game from inside the container; screenshots are the
only evidence of visual correctness.

### V. Simplicity

Prefer plain C# and Godot's built-in features. No new framework, DI container, ECS, or
third-party addon may be introduced without a written justification in the plan's Complexity
Tracking section naming the specific problem it solves and the simpler options rejected.

Absent such a justification, the simplest thing that works is the correct thing.

**Rationale**: this is a small game; every dependency is maintenance burden and a learning cost
paid by one person.

### VI. Standards Are Automated

Code style is defined by `.editorconfig` and the .NET analyzers, not by prose in this or any
other document. `dotnet format` MUST pass before a commit, and it runs as part of
`scripts/verify.sh`. A style disagreement is settled by changing the configuration, never by a
review comment or a remembered convention.

Godot naming is fixed and MUST be followed: scene files are PascalCase and match the class name
of their root script; signals are PascalCase; exported fields carry no prefix.

Documentation is minimal and lives where it is generated. `specs/` is the design record. The root
`README` covers running the project. Comments explain **why**, not **what**. XML doc comments
appear only on public `src/Core` APIs. Per-folder `README` files and separate ADR documents MUST
NOT be created.

**Rationale**: this is a solo hobby project. A standard that has to be remembered or interpreted
will not survive; a standard that is generated and machine-checked will.

## Additional Constraints

- **Stack**: Godot 4.7.2 (.NET) with C# targeting `net10.0`. Host and container run identical
  versions.
- **Code style**: defined by `.editorconfig` and the analyzer configuration; enforced by
  `dotnet format`, not by prose or review comments.
- **Repository layout**: `src/Core` (engine-free logic), `src/Game` (Godot adapters),
  `src/Game/Infrastructure` (engine-service implementations), `tests/Core.Tests` (xUnit),
  `tests/Game.Tests` (GoDotTest), `scripts/` (verification tooling), `scenes/`, `assets/`.
- **Environment**: development happens in a Podman container with no GPU and no real display.
  Rendering works only through `xvfb-run` (software).
- **Container ownership**: `container/` MUST NOT be modified without asking the developer first.
- **Asset changes**: adding assets requires re-running `godot --headless --import` before the
  change is considered buildable.

## Development Workflow & Quality Gates

Work happens on spec-kit feature branches and merges to `master` by squash PR.

`scripts/verify.sh` — build, `dotnet format`, Core tests, Godot tests, screenshot — MUST pass
before any task is reported complete. A task with a failing or unrun `verify.sh` is not complete,
and reporting it as complete is a violation of this constitution regardless of how small the
change looked.

When verification fails for reasons outside the change (broken tooling, environment drift), the
implementer MUST say so explicitly rather than silently skipping the gate.

A task is not complete while it introduces warnings. Warnings are fixed or explicitly suppressed
with a justification in `.editorconfig`; they are never left. This applies to build, analyzer, and
Godot runtime warnings alike.

## Governance

This constitution supersedes other practice documents in this repository. Where `CLAUDE.md`, a
plan, or a spec conflicts with it, this file wins, and the conflicting document is corrected.

Amendments require updating this file with a version bump and a note recording the rationale in
the Sync Impact Report. Versioning follows semantic versioning:

- **MAJOR**: a principle is removed or redefined in a backward-incompatible way.
- **MINOR**: a principle or section is added, or existing guidance is materially expanded.
- **PATCH**: clarifications, wording, and non-semantic refinements.

Every PR is reviewed against these principles before merge. Deviations MUST be recorded in the
plan's Complexity Tracking section with a justification, not discovered after the fact in the
diff.

**Version**: 1.1.1 | **Ratified**: 2026-09-01 | **Last Amended**: 2026-09-03
