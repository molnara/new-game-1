# Quickstart: Validating Developer Foundations

**Feature**: 001-dev-foundations | **Plan**: [plan.md](./plan.md)

How to prove this feature works once it is implemented. Each section maps to a user story in
[spec.md](./spec.md) and can be run on its own.

## Prerequisites

Inside the dev container. Godot 4.7.2 (.NET) on PATH, .NET SDK 10, ImageMagick 6, `xvfb-run` — all
already present and verified during planning.

```bash
dotnet build          # baseline; should already be green
```

---

## The 30-second check

```bash
scripts/verify.sh; echo "exit=$?"
```

Expect a `PASS` line per stage, a screenshot path, and `exit=0`.

---

## Story 1 — Session logs are readable after quitting

```bash
# Run the game briefly, then quit
timeout 20 xvfb-run -a godot --rendering-driver opengl3 --audio-driver Dummy --quit-after 120

# Read the newest session log
LOGDIR=~/.local/share/godot/app_userdata/"new game 1"/logs
ls -lt "$LOGDIR"
cat "$(ls -t "$LOGDIR"/session-*.log | head -1)"
```

**Expect**: a session file separate from Godot's own `godot.log`, with entries carrying timestamp,
level, source system, and message (FR-002).

**Retention (FR-006)**: run the game 12 times and confirm 10 session files remain — and that
`godot.log` was *not* pruned.

**Durability (FR-005)**: start the game, `kill -9` it, and confirm warnings and errors written before
the kill are present. Losing the tail of debug/info chatter is expected and correct.

---

## Story 2 — Console lists and runs commands

Most of this is automated. The slow test tier covers the parts that need the engine, and it runs in
the container:

```bash
xvfb-run -a godot --rendering-method forward_plus --rendering-driver vulkan \
  --audio-driver Dummy res://scenes/Main.tscn -- --run-tests=ConsoleInputTest --quit-on-finish
```

**Expect**: every suite reported passing — the toggle opens and closes the console (FR-010), the
toggle keystroke does not land in the input field (FR-011), and the open completes within a single
displayed frame (SC-007). `scripts/verify.sh` runs the same tier as stage 4, so a regression here
fails the gate rather than waiting for someone to notice by hand.

The **fast tier** covers every console decision that does not need the engine — parsing, resolution,
duplicate registration, help formatting, the bounded history:

```bash
dotnet test tests/Core.Tests
```

What remains genuinely manual is only what a human has to *look at*, and it is run on the host:

1. Launch the game and press **backtick**.
2. The console opens, the input field has focus, and no `` ` `` character appears in it (FR-011).
3. Type `help` → every registered command with a one-line summary (FR-012).
4. Type `help screenshot` → its usage.
5. Type `notacommand` → a failure naming it and pointing at `help`; the game keeps running (FR-015).
6. Press **Up** → the previous command is recalled (FR-017).
7. Press backtick again → the console closes and gameplay input resumes (FR-010).
8. Quit, then confirm the commands and their results appear in the session log (FR-018).

Steps 3-5 and 8 above are already asserted by the two tiers; repeat them by hand only when changing
how output is presented. Steps 2, 6 and 7 are the ones worth a human eye — whether the console
*reads* well, not whether it works.

---

## Story 3 — Screenshot capture, with and without a display

```bash
scripts/screenshot.sh main
ls -l artifacts/main.png
```

**Expect**: exit 0, a non-empty PNG, and the path printed as the last stdout line.

Verify it is not a blank frame — during planning a rendered spike scene reported mean RGB matching
its source colour exactly, and the same check applies here:

```bash
identify -verbose artifacts/main.png | grep -E "Geometry|mean"
convert artifacts/main.png -format %k info:   # distinct colours; 1 would mean a flat blank frame
```

Then **look at it**. Constitution IV requires the screenshot to be read, not merely produced.

**Failure paths worth exercising**:

```bash
scripts/screenshot.sh "../escape"   # rejected, nothing written outside artifacts/ (FR-025)
scripts/screenshot.sh main && scripts/screenshot.sh main   # second run reports replacement (FR-024)
```

**The trap this feature exists to avoid**: `godot --headless` cannot capture at all — the dummy
renderer has no viewport texture (research R1). If a capture ever appears to hang, check that
`--audio-driver Dummy` is being passed.

---

## Story 4 — One-command verification


```bash
scripts/verify.sh; echo "exit=$?"
```

**Expect**: `PASS` for build, code style, Core tests, Godot tests, screenshot, and golden compare;
`exit=0`.

Now prove it actually fails when it should:

```bash
# Break a Core test deliberately, then:
scripts/verify.sh; echo "exit=$?"     # expect FAIL naming the Core test stage, exit non-zero,
                                      # and no Godot/screenshot stage output (fail-fast, FR-030)
```

**Both tiers must be able to fail the gate** (SC-015). Test the slow one separately, because it is
the one that can silently pass by running nothing:

```bash
# Break a Game.Tests assertion deliberately, then:
scripts/verify.sh; echo "exit=$?"     # expect FAIL naming the Godot test stage, exit non-zero
```

**Expect** the failing suite, test name, exception message and source line in the output, and
`Test results: Passed: N | Failed: 1 | ...`.

Then check the trap the spike found (research R14) — a run executing **zero** tests exits 0 and
reports success:

```bash
xvfb-run -a godot --rendering-method forward_plus --rendering-driver vulkan \
  --audio-driver Dummy res://scenes/Main.tscn -- --run-tests=NoSuchSuite --quit-on-finish
echo "exit=$?"     # 0, with "Passed: 0 | Failed: 0 | Skipped: 0"
```

`verify.sh` must report **FAIL** for that, not PASS. If it passes, the stage is asserting only on the
exit code and would stay green if the tests ever stopped being discovered.

**The code-style stage** (FR-028b, SC-014). Prove it catches more than whitespace — that is the
specific way this stage fails silently (research R13):

```bash
# In any checked-in .cs file, add an unused `using System.Text;` and change one
# `string` to `String`, keeping the indentation correct. Then:
scripts/verify.sh; echo "exit=$?"
```

**Expect**: `FAIL` at the style stage naming file, line and rule id (`IDE0005`, `IDE0049`), exit
non-zero, and the test and screenshot stages not run. If this passes, `.editorconfig` is not setting
those rules to `warning` or above and the stage is checking almost nothing.

Then confirm the check did not edit anything on its way past:

```bash
git status --short     # after a clean run: no modified files
```

The gate uses `--verify-no-changes`; to actually apply the fixes, run `dotnet format` yourself
without that flag. Style is settled by editing `.editorconfig`, never by argument — constitution VI.

**Golden images**: regenerate in the container, never on the host — the container rasterizes in
software through llvmpipe while the host's editor uses its real GPU driver, so the two will not
produce identical pixels (research R2). Captures must use `forward_plus`, the same renderer the host
editor uses; capturing under `gl_compatibility` instead would add a second, avoidable source of
drift that grows as the real scene gains lighting and effects (research R10).

```bash
scripts/screenshot.sh main && cp artifacts/main.png tests/golden/main.png
```

Constitution IV requires a golden change to be intentional and explained in the PR description.

---

## Story 5 — Performance overlay and frame-time statistics

The toggle and the always-on sampling are covered by the slow tier
(`--run-tests=OverlayToggleTest`, FR-038 and FR-045), so what follows is the part a human has to
look at.

**Live overlay** (needs a display, so run on the host):

1. Launch the game, press backtick, type `perf`.
2. The overlay appears with frame time, FPS, draw calls, process memory and video memory (FR-037).
3. Values refresh about 4 times a second, each showing that interval's average plus its worst frame
   (FR-039, FR-039a) — readable, not a blur.
4. Type `perf` again to hide it. Type `perfstats` to print the numbers with the overlay hidden
   (FR-043).

**Statistics in the log** (works in the container):

```bash
timeout 30 xvfb-run -a godot --rendering-method forward_plus --rendering-driver vulkan \
  --audio-driver Dummy --quit-after 600
LOGDIR=~/.local/share/godot/app_userdata/"new game 1"/logs
grep -i "frame" "$(ls -t "$LOGDIR"/session-*.log | head -1)"
```

**Expect**: interim statistics records during the run and one final record at shutdown, visibly
distinguishable (FR-046, FR-046a), each carrying average, p95, p99, worst and a sample count.

**Prove sampling does not depend on the overlay** (FR-045): the run above never opened the console,
and must still produce statistics. `--run-tests=OverlayToggleTest` asserts the same thing without a
display.

**Prove crash-survival** (FR-046b): start the game, `kill -9` it after a few seconds, and confirm the
most recent interim record is on disk despite no clean shutdown.

**Prove the confidence rule** (FR-044): a run of well under 1000 frames must still write a record,
marked low-confidence, rather than reporting a confident-looking p99.

> **Do not read container frame times as game performance.** This container renders through Mesa's
> software rasterizers, so the numbers describe llvmpipe on the build machine, not your game. What
> these checks verify is that sampling, percentiles, record-writing and crash-survival all work.
> Absolute performance figures have to come from the host. See the plan's *Measurement validity*
> constraint — and note the container reached ~1,100 fps on a trivial scene during the spike, which
> is exactly the kind of meaningless-but-impressive number this warning exists to head off.

