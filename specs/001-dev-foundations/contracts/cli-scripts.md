# Contract: Verification Scripts

**Feature**: 001-dev-foundations | **Plan**: [../plan.md](../plan.md)

Four scripts in `scripts/`. These are the interface an automated caller — CI, or an agent working in
the container — depends on, so their exit codes and output shape are the contract.

All four: `set -euo pipefail`, runnable from any working directory, no interactive prompts.

---

## `scripts/screenshot.sh`

```
scripts/screenshot.sh [name]
```

Captures a screenshot of the placeholder main scene without a display.

| Aspect | Contract |
|---|---|
| **Argument** | Optional capture name; defaults to `main`. |
| **Output file** | `artifacts/<name>.png` |
| **Exit 0** | The PNG exists and is non-empty. |
| **Exit non-zero** | Capture failed; reason printed to stderr. No partial file left. |
| **stdout** | The path of the file written, as the last line. |

**Implementation constraints** (from research R1 — these are correctness requirements, not style):

- Runs under `xvfb-run -a`, **not** `--headless`. The dummy renderer cannot produce a viewport
  texture and capture is impossible there.
- Passes `--rendering-method forward_plus --rendering-driver vulkan`, matching the renderer the host
  editor uses, so the golden is not captured under a different renderer than the developer sees
  (research R10). Mesa's lavapipe provides software Vulkan; no GPU is needed.
- Passes `--audio-driver Dummy`. Omitting it makes the process stall on ALSA probing in a container
  with no sound card.
- Activates the harness with `-- --screenshot <name>` so the argument arrives via
  `OS.GetCmdlineUserArgs()`.
- Success is asserted positively — the file exists and is non-empty — because Godot's exit code is
  not trustworthy (research R4).

---

## `scripts/compare-golden.sh`

```
scripts/compare-golden.sh <candidate.png> <golden.png> [threshold]
```

Compares a capture against its committed reference.

| Aspect | Contract |
|---|---|
| **threshold** | Maximum differing pixel count tolerated. **Defaults to 0 — an exact match** (spec FR-036). Container captures are byte-identical across runs (research R2), so the default demands exactness and catches single-pixel regressions; the argument exists for the host case, which `verify.sh` never exercises. |
| **Exit 0** | Differing pixels ≤ threshold. |
| **Exit non-zero** | Over threshold, dimensions differ, or golden missing. |
| **stdout** | The differing pixel count and the threshold it was judged against. |

**Implementation constraints** (research R2, R3):

- Uses `compare -metric AE`, not `magick compare` — ImageMagick 6 is installed and the IM7 `magick`
  wrapper does not exist on this machine.
- `compare` writes its metric to **stderr** and exits non-zero when images differ. The script must
  capture stderr and must not let `set -e` abort on that expected non-zero exit.
- A missing golden is a failure with a message saying how to generate one — that message names
  `scripts/update-golden.sh` — never an implicit pass.

---

## `scripts/update-golden.sh`

```
scripts/update-golden.sh [name]
```

Regenerates a capture target's committed reference from a fresh capture (spec FR-035a). This is the
one supported way to resolve an intentional change to what is captured — replacing the placeholder
scene under FR-034, for instance — and the command `compare-golden.sh` names when a golden is
missing.

| Aspect | Contract |
|---|---|
| **name** | Capture target name, matching `screenshot.sh`. Defaults to `main`. |
| **Effect** | Captures via `screenshot.sh`, then copies the result over `tests/golden/<name>.png`. |
| **Exit 0** | The reference was written; the path is printed. |
| **Exit non-zero** | The capture failed; the existing reference is left untouched. |
| **stdout** | The reference path written, and whether it replaced an existing file. |

**Constraints**:

- It MUST NOT run as a stage of `verify.sh`. A gate that regenerates its own expectation cannot fail
  (spec FR-028f is the same defect class).
- It is a regeneration command, not a review workflow — reviewing the new reference is the
  developer's job at commit time (spec, Out of Scope).
- Goldens are container artifacts: this script is run in the container, never on the host (research R2).

---

## `scripts/verify.sh`

```
scripts/verify.sh
```

The quality gate from constitution "Development Workflow & Quality Gates".

**Stages, in order, stopping at the first failure (FR-030):**

| # | Stage | Passes when |
|---|---|---|
| 1 | Build | `dotnet build` succeeds. |
| 2 | Code style | `dotnet format NewGame1.sln --verify-no-changes --no-restore` exits 0, against an `.editorconfig` that enforces at least one rule at `warning` or above (FR-028b, FR-028f). |
| 3 | Core tests | `dotnet test` on the engine-free tier succeeds. |
| 4 | Godot tests | The GoDotTest run under `xvfb-run` exits 0 **and** reports a non-zero passed count (FR-028f). |
| 5 | Screenshot | `screenshot.sh` produces a non-empty PNG. |
| 6 | Golden compare | `compare-golden.sh` is within threshold — 0 by default, so any pixel difference fails. |

Stage 2 is required by constitution VI, and its position — immediately after build — is the order
that section states. It also happens to be the cheap one: the build has already restored, so the
stage runs with `--no-restore` and fails fast before the slower test and capture stages.

| Aspect | Contract |
|---|---|
| **Exit 0** | Every stage passed (FR-031). |
| **Exit non-zero** | A stage failed; its name is printed and later stages did not run. |
| **stdout** | One `PASS`/`FAIL` line per stage, plus the screenshot path (FR-029). |
| **Interaction** | None. Runs unattended with no display (FR-032). |

**Code style stage constraints** (research R13):

- Bare `dotnet format` — no subcommand — so `whitespace`, `style` and `analyzers` all run. Naming
  only `whitespace` was measured to miss unused usings, `String` for `string`, and misordered
  directives entirely.
- `--verify-no-changes` is mandatory. Without it the command rewrites source files, which turns a
  verification script into an editing one; a gate must never mutate the tree it is judging.
- The exit code **is** trustworthy here — 0 clean, 2 when changes would be made — unlike the Godot
  stages below. Branch on it directly.
- Violations are reported one per line as `path(line,col): severity ID: message`; surface them
  verbatim so the developer can jump straight to each.
- This stage is only as strong as `.editorconfig`. Against the four-line file the repository had
  before this feature, it passes essentially everything (research R13), so the expanded
  `.editorconfig` must land before or with the stage — never after.

**Godot test stage constraints** (FR-028c, FR-028d; research R14):

- Invoked as a Godot run, not a `dotnet` one. Spike-verified form:

  ```bash
  xvfb-run -a godot --rendering-method forward_plus --rendering-driver vulkan \
    --audio-driver Dummy res://scenes/Main.tscn -- --run-tests --quit-on-finish
  ```

  The same renderer and audio flags the screenshot stage uses, for the same reasons (research R1,
  R10).
- `--run-tests` and `--quit-on-finish` are GoDotTest's flags, not Godot's. They **must** be passed
  after `--`, and `Main.cs` **must** read them from `OS.GetCmdlineUserArgs()`. Reading
  `OS.GetCmdlineArgs()` as the package README shows does not see args after `--`: the tests silently
  do not run and the process hangs until killed (research R14).
- **Exit code 0/1 is reliable here** — GoDotTest sets it. Research R4's warning does not apply to
  this stage.
- **But exit 0 is not sufficient.** A run executing zero tests exits 0 and prints
  `Test results: Passed: 0 | Failed: 0 | Skipped: 0`. The stage MUST also assert the `Passed:` count
  is greater than zero. Both checks are needed: the exit code misses the empty run, and the results
  line is absent entirely if the engine dies before the runner starts.
- Every Godot invocation needs an external timeout. A malformed argument does not fail the run — it
  starts the game normally and blocks forever (research R14).
- On failure, surface the runner's output verbatim: it names the suite, the test, the exception
  message, and the source file and line.
- Stage 3 runs first and is far faster, so an error reachable by a Core test is reported before the
  engine is ever launched.

**Extension point** (FR-028a): stages are defined so further stages can be inserted without
reworking the script or its reporting. The engine tier this originally anticipated is now stage 4.

**Godot stages must not trust exit codes** (research R4): running with no main scene defined printed
a fatal error and still exited 0. Godot-invoking stages assert on expected output artifacts and scan
for error markers.
