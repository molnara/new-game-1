# Contract: Verification Scripts

**Feature**: 001-dev-foundations | **Plan**: [../plan.md](../plan.md)

Three scripts in `scripts/`. These are the interface an automated caller — CI, or an agent working in
the container — depends on, so their exit codes and output shape are the contract.

All three: `set -euo pipefail`, runnable from any working directory, no interactive prompts.

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
| **threshold** | Maximum differing pixel count tolerated. Defaults to a small non-zero value. |
| **Exit 0** | Differing pixels ≤ threshold. |
| **Exit non-zero** | Over threshold, dimensions differ, or golden missing. |
| **stdout** | The differing pixel count and the threshold it was judged against. |

**Implementation constraints** (research R2, R3):

- Uses `compare -metric AE`, not `magick compare` — ImageMagick 6 is installed and the IM7 `magick`
  wrapper does not exist on this machine.
- `compare` writes its metric to **stderr** and exits non-zero when images differ. The script must
  capture stderr and must not let `set -e` abort on that expected non-zero exit.
- A missing golden is a failure with a message saying how to generate one — never an implicit pass.

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
| 2 | Core tests | `dotnet test` on the engine-free tier succeeds. |
| 3 | Screenshot | `screenshot.sh` produces a non-empty PNG. |
| 4 | Golden compare | `compare-golden.sh` is within threshold. |

| Aspect | Contract |
|---|---|
| **Exit 0** | Every stage passed (FR-031). |
| **Exit non-zero** | A stage failed; its name is printed and later stages did not run. |
| **stdout** | One `PASS`/`FAIL` line per stage, plus the screenshot path (FR-029). |
| **Interaction** | None. Runs unattended with no display (FR-032). |

**Extension point** (FR-028a): stages are defined so an engine-based test tier can be inserted
between stages 2 and 3 without reworking the script or its reporting. No such tier exists yet — that
was a deliberate clarified decision, not an oversight.

**Godot stages must not trust exit codes** (research R4): running with no main scene defined printed
a fatal error and still exited 0. Godot-invoking stages assert on expected output artifacts and scan
for error markers.
