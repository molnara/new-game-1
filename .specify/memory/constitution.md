<!--
Sync Impact Report
==================
Version change: (unversioned template) → 1.0.0
Bump rationale: Initial ratification. The prior file was the unfilled scaffold with no
project-specific values, so this is the first governing version rather than an amendment.

Modified principles:
  - [PRINCIPLE_1_NAME] → I. Core/Adapter Separation (NON-NEGOTIABLE)
  - [PRINCIPLE_2_NAME] → II. Test-First, Two Tiers
  - [PRINCIPLE_3_NAME] → III. Observability by Default
  - [PRINCIPLE_4_NAME] → IV. Visual Verification
  - [PRINCIPLE_5_NAME] → V. Simplicity

Added sections:
  - Additional Constraints (was [SECTION_2_NAME])
  - Development Workflow & Quality Gates (was [SECTION_3_NAME])

Removed sections: none

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

## Additional Constraints

- **Stack**: Godot 4.7.2 (.NET) with C# targeting `net10.0`. Host and container run identical
  versions.
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

`scripts/verify.sh` — build, Core tests, Godot tests, screenshot — MUST pass before any task is
reported complete. A task with a failing or unrun `verify.sh` is not complete, and reporting it
as complete is a violation of this constitution regardless of how small the change looked.

When verification fails for reasons outside the change (broken tooling, environment drift), the
implementer MUST say so explicitly rather than silently skipping the gate.

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

**Version**: 1.0.0 | **Ratified**: 2026-09-01 | **Last Amended**: 2026-09-01
