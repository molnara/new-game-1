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

Needs an interactive display, so this one is run on the host rather than in the container:

1. Launch the game and press **backtick**.
2. The console opens, the input field has focus, and no `` ` `` character appears in it (FR-011).
3. Type `help` → every registered command with a one-line summary (FR-012).
4. Type `help screenshot` → its usage.
5. Type `notacommand` → a failure naming it and pointing at `help`; the game keeps running (FR-015).
6. Press **Up** → the previous command is recalled (FR-017).
7. Press backtick again → the console closes and gameplay input resumes (FR-010).
8. Quit, then confirm the commands and their results appear in the session log (FR-018).

**In-container substitute** for what can be automated: the Core tests cover parsing, resolution,
duplicate registration, help formatting, and the bounded history — every decision in the console
except the keystrokes themselves.

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

**Expect**: `PASS` for build, Core tests, screenshot, and golden compare; `exit=0`.

Now prove it actually fails when it should:

```bash
# Break a test deliberately, then:
scripts/verify.sh; echo "exit=$?"     # expect FAIL naming the test stage, exit non-zero,
                                      # and no screenshot stage output (fail-fast, FR-030)
```

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
and must still produce statistics.

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

