# Feature Specification: Developer Foundations

**Feature Branch**: `001-dev-foundations`

**Created**: 2026-09-01

**Status**: Draft

**Input**: User description: "Developer foundations: the game has structured file logging, an in-game dev console toggled with backtick that lists and runs commands, a screenshot harness for headless verification, and a verify script that runs build, tests, and a screenshot. A developer can open the console, type help, see commands, run \"screenshot main\", and find the PNG in artifacts/. Logs from a play session are readable in the user data logs folder after quitting."

## Clarifications

### Session 2026-09-01

- Q: When the game is killed abruptly, how much of the most recent log output is acceptable to lose in exchange for logging that never stalls a frame? → A: Flush warnings and errors to disk immediately; batch debug and information entries, flushing them on a short interval and at shutdown.
- Q: Should this feature build the placeholder scene the screenshot harness and verify script capture, or does a real game scene arrive separately? → A: This feature ships a minimal placeholder main scene (flat background plus an identifying label) as the capture target, to be replaced by the first real scene.
- Q: In a headless run, what should the harness wait for before capturing, so the image is not taken while the scene is still blank? → A: Wait for a fixed, configurable number of fully rendered frames (machine-speed independent), then capture.
- Q: If a build is exported and given to another person, should the developer console still be in it? → A: Present in all builds and never compiled out, but in a distributed build it opens only when explicitly enabled by a launch flag or setting.
- Q: Should verification stand up an engine-based test tier now, or run only the fast engine-free tests? → A: Fast engine-free tests only for now; the verification command is structured so an engine tier can be added as a later stage without restructuring. — **SUPERSEDED 2026-09-02, see below.**

### Session 2026-09-02

- Q: Should frame times be measured from startup on every run, or only while the overlay is switched on? → A: Always sample, from startup, in every build, so every session's log has statistics whether or not the overlay was opened.
- Q: When should frame-time statistics be written to the log — only at shutdown, or repeatedly during the session? → A: A periodic snapshot during the session plus a final record at shutdown, so a crashed or killed session still leaves usable statistics.
- Q: Which memory figure should the overlay and log report? → A: Two labelled figures — total process memory and video memory — so both runaway allocation and texture/mesh bloat are visible.
- Q: How often should the overlay refresh its numbers? → A: About 4 times per second, showing each interval's average, with that interval's worst frame displayed separately so brief stalls are not averaged away.
- Q: How many frames must a session have before its 99th-percentile figure is treated as trustworthy? → A: 1000 frames (~17 seconds at 60 fps); below that the record is still written but marked low-confidence.
- Q: Should this feature stand up the engine-based test tier, reversing the 2026-09-01 answer? → A: Yes. Both tiers ship from the start — a fast engine-free tier and a slower Godot tier — and verification runs both. **This supersedes the 2026-09-01 answer** ("fast engine-free tests only for now"), which had left node and input behavior (FR-011, SC-007) to a manual checklist run on the host.

## User Scenarios & Testing *(mandatory)*

The user of this feature is the developer working on the game (and any automated agent acting on
their behalf). The game's players are not affected by it.

### User Story 1 - Read what the game did during a play session (Priority: P1)

A developer runs the game, plays for a while, hits a problem, and quits. They open the logs folder
in the game's user data directory and read a complete, timestamped record of that session: which
systems did what, in what order, and what went wrong. They can do this after the fact, without
having had a debugger attached and without having predicted in advance which run would be the
interesting one.

**Why this priority**: Nothing else can be diagnosed without it. The developer cannot see the game
from inside the development container, so a written record of each run is the primary evidence
channel. Every later story writes to this record, so it must exist first.

**Independent Test**: Run the game, let it start and quit, then open the newest file in the user
data logs folder and confirm it contains timestamped, severity-tagged, source-tagged entries for
that session. Delivers value on its own: sessions become reviewable after the fact.

**Acceptance Scenarios**:

1. **Given** the game has never been run on this machine, **When** the developer runs it once and
   quits normally, **Then** a logs folder exists in the user data directory containing exactly one
   session log file, and that file contains entries covering startup through shutdown.
2. **Given** a session is in progress, **When** a system reports an event, **Then** the resulting
   log entry records the time, the severity, the reporting system, and the message.
3. **Given** the game exits because of an unhandled failure, **When** the developer opens the
   session log afterwards, **Then** the failure is recorded with its details, and every warning and
   error reported before it is present.
4. **Given** many sessions have been run, **When** the developer opens the logs folder, **Then**
   session logs are individually identifiable by start time and only a bounded number of recent
   sessions are retained.
5. **Given** the game is run without a display, **When** the developer watches the terminal,
   **Then** the same entries appear there as in the session log file.

---

### User Story 2 - Discover and run developer commands from inside the game (Priority: P2)

A developer playing the game presses the backtick key. A console panel appears over the game. They
type `help` and see every available command with a one-line description of what it does. They type
one of those commands, press Enter, and see its result in the console. They press backtick again
and are back in the game.

**Why this priority**: This is the developer's hands-on control surface — the way to inspect and
poke a running game without rebuilding it. It is the second thing built because it depends on
logging but nothing else depends on it.

**Independent Test**: Launch the game, press backtick, type `help`, confirm the command list
appears, run a listed command, confirm its output appears, press backtick and confirm gameplay
input resumes. Delivers value on its own: the running game becomes inspectable.

**Acceptance Scenarios**:

1. **Given** the game is running and the console is closed, **When** the developer presses the
   console toggle key, **Then** the console opens with an input field focused and does not insert
   the toggle character into that field.
2. **Given** the console is open, **When** the developer types `help` and submits it, **Then**
   every registered command is listed with a one-line description.
3. **Given** the console is open, **When** the developer submits `help` followed by a command name,
   **Then** that command's usage and arguments are shown.
4. **Given** the console is open, **When** the developer submits a name that is not a registered
   command, **Then** the console reports that the command is unknown, points at `help`, and the
   game keeps running.
5. **Given** the console is open, **When** the developer submits a command that fails, **Then** the
   console shows a failure message describing what went wrong, the failure is recorded in the
   session log, and the console remains usable.
6. **Given** the console is open, **When** the developer presses the movement or action keys used
   in gameplay, **Then** the game does not act on them; **When** the console is closed again,
   **Then** gameplay input works as before.
7. **Given** the developer has submitted commands earlier in this session, **When** they press the
   history keys in the input field, **Then** previously submitted commands are recalled for reuse.
8. **Given** the console has produced output, **When** the developer reads the session log after
   quitting, **Then** the commands submitted and their results are present in it.

---

### User Story 3 - Capture a screenshot as evidence, with or without a display (Priority: P3)

A developer wants proof of what the game currently looks like. From inside the game they open the
console and run `screenshot main`; the console reports the file it wrote and the image appears in
the project's `artifacts/` folder. The same capture can be triggered from the command line on a
machine with no display and no GPU, so an automated run — or an agent working in the container —
can produce the same evidence unattended.

**Why this priority**: Visual correctness cannot be confirmed any other way from inside the
container, and the project's quality gate requires screenshot evidence. It depends on the console
for the interactive path but is independently valuable through its headless path.

**Independent Test**: With no display available, invoke the capture from the command line and
confirm a non-empty PNG of the expected dimensions appears in `artifacts/` and the invocation
reports success. Separately, run `screenshot main` from the console and confirm the same file
appears. Delivers value on its own: rendering becomes verifiable without a human looking at a
window.

**Acceptance Scenarios**:

1. **Given** the console is open in a running game, **When** the developer submits
   `screenshot main`, **Then** an image of the current view is written to `artifacts/` under the
   given name and the console reports the path it wrote.
2. **Given** the `artifacts/` folder does not exist, **When** a capture is requested, **Then** the
   folder is created and the capture succeeds.
3. **Given** a capture is requested with no name, **Then** a default name is used and the resulting
   path is reported.
4. **Given** an image of the same name already exists, **When** a capture is requested, **Then** it
   is replaced and the developer is told it was replaced.
5. **Given** a machine with no display and no GPU, **When** the capture is invoked from the command
   line, **Then** it produces a non-empty image of the expected dimensions and reports success.
6. **Given** the capture cannot be completed, **When** it is invoked, **Then** it reports failure
   with the reason, records the reason in the session log, and leaves no empty or partial image
   file behind.
7. **Given** a fresh checkout with no real game scene yet, **When** a capture is requested, **Then**
   it succeeds against the placeholder scene, so the harness is provable before any gameplay exists.

---

### User Story 4 - Confirm the project is healthy with one command (Priority: P4)

Before reporting work as done, the developer runs a single verification command. It builds the
project, runs the tests, and captures a screenshot, printing a clear per-stage pass or fail summary
and the path to the screenshot. If any stage fails it stops there, says which stage failed, and
returns a failure status the developer or an automated caller can act on.

**Why this priority**: It composes the previous stories into the project's quality gate. It is last
because it has nothing to run until they exist.

**Independent Test**: Run the verification command on a healthy checkout and confirm it reports all
stages passing, returns success, and names a screenshot that exists. Then break a test deliberately
and confirm it stops at that stage, names it, and returns failure.

**Acceptance Scenarios**:

1. **Given** a healthy checkout, **When** the developer runs the verification command, **Then**
   build, tests, and screenshot capture each run in that order, each is reported as passed, the
   screenshot path is printed, and the overall result is success.
2. **Given** the build is broken, **When** verification runs, **Then** it stops after the build
   stage, reports the build as the failing stage, and returns failure without running later stages.
3. **Given** a test is failing, **When** verification runs, **Then** it reports the test stage as
   failed, surfaces enough detail to identify the failing test, and returns failure.
4. **Given** a machine with no display and no GPU, **When** verification runs, **Then** it completes
   all stages without human interaction.
5. **Given** verification has completed, **When** an automated caller inspects its result, **Then**
   the result is success only if every stage passed.

---

### User Story 5 - See performance while playing, and read the numbers afterwards (Priority: P5)

A developer suspects the game is running badly. They open the console and toggle a performance
overlay: frame time, frames per second, draw calls, and memory use appear on screen and update as
they play. They watch the numbers while moving around, then quit. In the session log they find
frame-time statistics for the run — average, 95th percentile, 99th percentile, and the worst single
frame — so they can tell whether the session was smooth throughout or merely smooth on average.

**Why this priority**: It is the only story here that is a diagnostic convenience rather than a
foundation the rest of the work rests on. The constitution does not require it, and nothing else in
this feature depends on it, so it is the safest story to drop or defer if the feature needs
trimming. It earns its place early because performance problems are cheapest to notice at the moment
they first appear, rather than months later with a large scene to bisect.

**Independent Test**: Launch the game, toggle the overlay from the console, confirm the four
measurements appear and move as the game runs, quit, and confirm the session log contains the four
statistics. Delivers value on its own: performance becomes observable both live and after the fact.

**Acceptance Scenarios**:

1. **Given** the game is running with the overlay off, **When** the developer runs the overlay
   toggle command, **Then** the overlay appears showing frame time, frames per second, draw calls,
   and both memory figures, and those values update as the game runs.
2. **Given** the overlay is visible, **When** the developer runs the toggle command again, **Then**
   the overlay disappears and nothing else about the running game changes.
3. **Given** the overlay is visible, **When** the scene behind it is bright, dark, or busy, **Then**
   the values stay legible.
4. **Given** a play session has ended normally — whether or not the overlay was ever opened —
   **When** the developer opens the session log, **Then** it contains that session's average frame
   time, 95th percentile, 99th percentile, and worst single frame, as one identifiable record
   rather than scattered prose.
5. **Given** the developer wants the numbers without the overlay on screen, **When** they run the
   statistics command in the console, **Then** the current statistics are printed to the console.
6. **Given** a session ran for only a handful of frames, **When** its statistics are written,
   **Then** they are still written and state how few samples they rest on, rather than being
   omitted or presented as though they were reliable.
7. **Given** the overlay is enabled, **When** the measured frame time is compared against the same
   scene with the overlay disabled, **Then** the difference is under 1 millisecond.
8. **Given** an automated run with no display, **When** metrics that depend on rendering are
   unavailable or meaningless, **Then** nothing crashes and the session log still records whatever
   statistics could be gathered.
9. **Given** one frame takes far longer than the frames around it, **When** the developer is
   watching the overlay, **Then** that stall appears in the interval's worst-frame figure instead of
   being smoothed away by the interval average.

---

### Edge Cases


- **Console toggle key unavailable**: on keyboard layouts where backtick is hard to reach or
  absent, the toggle must be reachable through a rebindable input binding rather than a hard-wired
  key, so the console is never unreachable.
- **Console opened before systems finish starting**: `help` must list whatever is registered at
  that moment and must not fail because a system has not registered yet.
- **Command name collision**: two systems registering the same command name must be surfaced as a
  logged error at registration rather than silently shadowing one another.
- **Long or rapid output**: a command producing many lines, or a system logging in a tight loop,
  must not freeze the game or make the console unreadable; console history is bounded.
- **Console used in a headless run**: a capture or command invoked where there is no console UI must
  still work through the non-interactive path.
- **Capture requested with nothing rendered yet**: a capture waits for its configured frame count
  first. If those frames cannot be produced at all, it must fail with a clear reason rather than
  writing a blank or zero-byte file.
- **Log destination unwritable**: if the logs folder cannot be created or written (permissions, full
  disk), the game must report this on the terminal and keep running rather than failing to start.
- **Two sessions at once**: two copies of the game running simultaneously must not write to the same
  session log file or corrupt one another's records.
- **Abrupt termination**: a session killed without a clean shutdown must still leave every warning
  and error it had reported readable on disk; at most the most recent unflushed batch of debug and
  information entries may be missing.
- **Invalid screenshot name**: a name containing path separators or otherwise unusable characters
  must be rejected with a clear message, and must not write outside `artifacts/`.
- **Overlay enabled before the first frame**: toggling the overlay before any frame has been drawn
  must show that no samples exist yet rather than dividing by zero or displaying a misleading value.
- **First-run frame spikes**: the first run after new assets pays a shader-compilation cost, which
  will dominate the worst-frame figure. The statistics must make that visible rather than quietly
  smoothing it away, so a developer is not misled into chasing a problem that only occurs once.
- **Percentiles from too few samples**: a session of fewer than 1000 frames must still produce a
  statistics record, marked low-confidence, rather than reporting a confident-looking figure drawn
  from a handful of frames.
- **Session killed mid-run**: a session terminated without a clean shutdown must still leave its
  most recent interim statistics record on disk, even though no final record was ever written.

## Requirements *(mandatory)*

### Functional Requirements

#### Logging

- **FR-001**: The game MUST write a log file for every run into a `logs` folder inside the
  per-user application data directory, with one file per session, named so sessions are
  distinguishable and orderable by start time.
- **FR-002**: Every log entry MUST record, at minimum: the time it occurred, its severity, the
  system that reported it, and a human-readable message.
- **FR-003**: The system MUST support at least four severities — debug, information, warning, and
  error — and MUST support a configurable minimum severity so noisy levels can be suppressed.
- **FR-004**: Any part of the game MUST be able to obtain a logger already labelled with that
  part's own system name, so every entry is attributable to its source without the author
  restating that name in each message.
- **FR-005**: Log entries MUST be readable on disk after the game exits. Warning and error entries
  MUST reach disk as they occur so they survive an abnormal termination; debug and information
  entries MAY be batched, and MUST be written on a short recurring interval and at shutdown.
- **FR-006**: The system MUST retain a bounded number of recent session logs and remove older ones
  automatically, so the logs folder does not grow without limit.
- **FR-007**: When the game runs without a display, log entries MUST also appear on the standard
  output stream so an automated caller can read them live.
- **FR-008**: Failures MUST NOT be discarded silently: an error that is handled MUST be logged with
  its cause, and an error that is not handled MUST be allowed to surface.

#### Developer console

- **FR-009**: The game MUST provide an in-game console that opens and closes on a single key press,
  bound by default to backtick and remappable, available from any scene without restarting.
- **FR-009a**: The console MUST be present in every build rather than omitted from some of them, so
  no system needs a different version of its command registration per kind of build. In a build made
  for distribution the console MUST stay unopenable unless explicitly enabled by a launch flag or
  setting, so a player cannot stumble into it.
- **FR-010**: The console MUST present a scrollable output history and a single-line input field,
  and MUST take keyboard focus while open so gameplay does not react to typing.
- **FR-011**: Opening the console MUST NOT insert the toggle key's character into the input field.
- **FR-012**: The console MUST provide a `help` command that lists every registered command with a
  one-line description, and `help <command>` that shows that command's usage and arguments.
- **FR-013**: Any game system MUST be able to register a command by supplying a name, a description,
  an argument description, and the behavior to run, without that system's registration requiring
  edits to unrelated systems.
- **FR-014**: Registering two commands under the same name MUST be reported as an error rather than
  silently replacing the earlier one.
- **FR-015**: Submitting an unrecognized command MUST produce a message naming the unrecognized
  input and pointing at `help`, and MUST NOT interrupt the running game.
- **FR-016**: A command that fails MUST report the failure and its reason in the console, record it
  in the session log, and leave the console usable for the next command.
- **FR-017**: The console MUST let the developer recall previously submitted commands from the
  current session.
- **FR-018**: Commands submitted and the results they report MUST be recorded in the session log.
- **FR-019**: Console output history MUST be bounded so a long-running session or a chatty command
  cannot exhaust memory.

#### Screenshot capture

- **FR-020**: The system MUST capture the currently rendered view and write it to the project's
  `artifacts/` folder as a PNG image.
- **FR-021**: A `screenshot` console command MUST be available, accepting an optional name that
  determines the file name and falling back to a default name when omitted.
- **FR-022**: The capture MUST report the full path of the file it wrote, both in the console and
  in the session log.
- **FR-023**: The `artifacts/` folder MUST be created automatically if it does not exist.
- **FR-024**: A capture whose target file already exists MUST replace it and say that it did.
- **FR-025**: Names that are not usable as a plain file name, or that would write outside
  `artifacts/`, MUST be rejected with a clear message and no file written.
- **FR-026**: Capture MUST be invocable from the command line without any human interaction and
  without a display or GPU, and MUST signal success or failure to its caller. Before capturing it
  MUST wait for a fixed, configurable number of fully rendered frames — a frame count, never a
  wall-clock delay — so the result does not depend on how fast or how loaded the machine is.
- **FR-027**: A failed capture MUST report the reason and MUST NOT leave an empty or partially
  written image behind.

#### Verification

- **FR-028**: The project MUST provide a single command that runs, in order: the build, an automated
  code-style check, the engine-free test suite, the engine-based test suite, and a screenshot
  capture.
- **FR-028a**: Verification MUST be structured so that further stages can be inserted as additional
  stages without reworking the command or its reporting. (The engine-based tier this requirement was
  originally written to accommodate is now a stage in its own right — see FR-028c — so this
  requirement now covers stages not yet foreseen.)
- **FR-028c**: The project MUST provide two distinct automated test tiers: a fast tier covering
  engine-free logic, and a slower tier that runs inside the engine and covers behavior that cannot be
  expressed without it. Both MUST be runnable independently and both MUST run as stages of the
  verification command.
- **FR-028d**: The engine-based tier MUST run unattended without a display, on the same software
  rendering path as the rest of verification, so it is runnable wherever the fast tier is.
- **FR-028e**: The console behavior that cannot be expressed engine-free — that opening the console
  does not deliver the opening keystroke into the console's own input, and that the console opens
  within a single displayed frame — MUST be covered by the engine-based tier rather than by a manual
  checklist.
- **FR-028b**: Code style MUST be enforced by a machine check reading a checked-in configuration
  file, and that configuration MUST be the definition of the project's style — not prose in a
  document and not a reviewer's judgement. The check MUST report violations without modifying source
  files, and MUST fail verification when any are found.
- **FR-029**: Verification MUST print a per-stage pass or fail summary and the path of the captured
  screenshot.
- **FR-030**: Verification MUST stop at the first failing stage, name that stage, and surface enough
  detail to identify the cause.
- **FR-031**: Verification MUST signal overall success only when every stage passed, in a form an
  automated caller can branch on.
- **FR-032**: Verification MUST run to completion unattended on a machine with no display and no
  GPU.

#### Capture target

- **FR-033**: The project MUST include a minimal placeholder main scene whose only purpose is to be
  something to photograph: a flat background and a visible label identifying the build. It MUST
  contain no gameplay.
- **FR-034**: The placeholder scene MUST be what the screenshot harness and the verification command
  capture by default, and MUST be replaceable by the first real game scene without changing either
  of them.

#### Golden reference images

- **FR-035**: The project MUST keep a committed reference image for each capture target, and MUST
  provide a way to compare a fresh capture against its reference and report how many pixels differ.
- **FR-036**: The comparison MUST pass or fail against a stated tolerance rather than demanding an
  exact match, MUST fail when the reference is missing rather than passing by default, and MUST run
  as a stage of the verification command.

#### Performance overlay and frame-time statistics

- **FR-037**: The game MUST be able to display a performance overlay showing, at minimum, current
  frame time, frames per second, draw calls, and memory (as specified in FR-047), updating as the
  game runs.
- **FR-038**: The overlay MUST be toggled by a dev console command and MUST be off by default, so an
  ordinary play session is never affected by it.
- **FR-039**: The overlay MUST refresh roughly 4 times per second (about every 250 ms), showing
  values averaged over each interval rather than raw per-frame numbers, which change too fast to
  read.
- **FR-039a**: Alongside each interval's average, the overlay MUST show the worst single frame
  within that interval, so a brief stall is visible rather than averaged away.
- **FR-039b**: Overlay values MUST stay legible over any scene content behind them.
- **FR-040**: Enabling the overlay MUST NOT meaningfully distort the measurements it reports: its
  own cost MUST stay under 1 millisecond of frame time.
- **FR-041**: The system MUST record frame-time statistics for the session — average, 95th
  percentile, 99th percentile, and worst single frame — into the session log.
- **FR-042**: Recorded statistics MUST be a single identifiable record that can be found by
  searching the log file, and MUST state the number of samples they were computed from.
- **FR-043**: A console command MUST print the current statistics on demand, without requiring the
  overlay to be visible.
- **FR-044**: Statistics computed from fewer than 1000 frame samples MUST still be written, but
  MUST be marked as low-confidence rather than presented as reliable. At 60 frames per second that
  is roughly 17 seconds of play — the point at which the 99th percentile rests on about 10 samples
  rather than on a single unlucky frame.
- **FR-045**: Frame sampling MUST run continuously from startup in every build, whether or not the
  overlay is visible, so every session produces statistics without the developer having to
  anticipate needing them.
- **FR-045a**: Because sampling is always on, it MUST cost no more than a fixed-size record per
  frame and MUST NOT grow without bound over a long session.
- **FR-046**: Statistics MUST be written to the session log at a recurring interval during the
  session and once more as a final record at shutdown, so a session that crashes or is killed still
  leaves usable statistics behind.
- **FR-046a**: Interim statistics records MUST be distinguishable from the final end-of-session
  record, so a reader can tell a mid-session snapshot from the summary of the whole run.
- **FR-046b**: Statistics records MUST reach disk as they are written rather than waiting in a
  batch. Without this, an abruptly killed session would discard the very records FR-046 exists to
  preserve (see FR-005 for the general flush policy).
- **FR-047**: The overlay and the logged statistics MUST report two separately labelled memory
  figures — total process memory and video memory — rather than one unlabelled "memory" number.
  The two catch different failures: overall allocation growth, and texture or mesh bloat on the
  graphics device.

### Key Entities

- **Log Entry**: one recorded event — time, severity, reporting system, message, and optional
  failure detail.
- **Session Log**: the ordered collection of log entries for a single run of the game, stored as one
  file identified by its start time.
- **Logger**: the per-system handle used to record entries, carrying the system's identity so
  entries are attributable.
- **Console Command**: a named developer action — name, one-line description, argument description,
  and the behavior it performs — that reports a result or a failure reason.
- **Command Registry**: the collection of registered commands that `help` enumerates and the console
  resolves submitted input against; names within it are unique.
- **Screenshot Artifact**: a captured PNG image of the rendered view, stored under `artifacts/` and
  identified by the name given at capture time.
- **Verification Run**: one execution of the verification command — an ordered set of stages, each
  with a pass or fail outcome, plus an overall outcome and the screenshot it produced.
- **Frame Sample**: one frame's measurement — how long the frame took, and when it was taken.
- **Frame-Time Statistics**: an aggregate over the samples collected — average, 95th percentile,
  99th percentile, worst single frame, the sample count the figures rest on, and whether that count
  clears the 1000-sample confidence threshold. Written both as interim snapshots during a session
  and as one final record at shutdown; the two are distinguishable.
- **Performance Overlay**: the on-screen display of current measurements — frame time, frames per
  second, draw calls, process memory and video memory — refreshed on a fixed interval, with a
  visible or hidden state toggled from the console. Its visibility does not affect whether sampling
  happens.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer who has not seen the project before can open the console, list the
  available commands, and produce a screenshot file in under 2 minutes, without reading source code
  or documentation beyond `help`.
- **SC-002**: 100% of completed play sessions leave a readable session log file covering startup
  through shutdown; a session killed abruptly retains 100% of the warnings and errors it had
  reported before the kill.
- **SC-003**: Screenshot capture invoked with no display available succeeds in 10 out of 10
  consecutive attempts, each producing a non-empty image at the expected dimensions showing the
  drawn scene rather than a blank frame — including on a heavily loaded machine.
- **SC-004**: The single verification command returns a definitive overall pass or fail with no
  manual steps, and completes in under 5 minutes on the development machine.
- **SC-005**: When verification fails, the stage at fault is identifiable from its output alone in
  100% of cases, without re-running individual stages to find out where it broke.
- **SC-006**: Adding a developer command for a new system requires changes only within that system;
  no existing system or shared list needs editing.
- **SC-007**: The console opens and closes within a single displayed frame of the key press, with no
  perceptible pause in the running game.
- **SC-008**: A one-hour play session's log file stays small enough to open and read in an ordinary
  text editor (under 50 MB) at the default severity threshold.
- **SC-009**: No developer-facing failure in these systems is silent: every failure path listed in
  the acceptance scenarios produces a message the developer can read in the console, the terminal,
  or the session log.
- **SC-010**: A change that alters what the placeholder scene looks like is caught by the
  verification command rather than reaching a commit unnoticed.
- **SC-011**: A developer can turn on the overlay and read current frame time, frames per second,
  draw calls, and memory within 5 seconds of opening the console, without restarting the game.
- **SC-012**: After a completed play session, a developer can locate that session's average, 95th
  percentile, 99th percentile, and worst frame time in the log by searching it once.
- **SC-013**: Turning the overlay on changes measured frame time by less than 1 millisecond, so the
  act of measuring does not distort what is being measured.
- **SC-014**: A style violation introduced into any checked-in C# source file is reported by the
  verification command, naming the file, the line, and the rule — with no source file modified by
  the check itself.
- **SC-015**: Both test tiers run from the single verification command on a machine with no display,
  and a deliberately broken test in either tier fails that command — so neither tier can rot unnoticed
  by being runnable only by hand.
- **SC-016**: Opening and closing the console, and the keystroke isolation that goes with it, are
  verified without a human performing the steps.

## Assumptions

These defaults were chosen where the feature description did not specify a detail. Each is a
reversible decision, recorded here so planning can challenge it.

- **Audience**: the only user is the solo developer and the automated agents working on their
  behalf. No end player ever sees the console, and no multi-user or permission model is needed.
- **`screenshot main` semantics**: the argument names the output file (yielding `artifacts/main.png`)
  and the capture is of whatever is currently on screen. It is not a request to load a scene called
  "main" before capturing.
- **The placeholder scene is disposable**: it exists only so the harness has something to capture
  (FR-033, FR-034) and is expected to be deleted outright once a real scene exists. Nothing may
  build on it, and its appearance is not worth debating.
- **Console availability**: no longer deferred — see Clarifications and FR-009a. The console ships
  everywhere and is gated at the point of opening rather than at the point it is built, which keeps
  one code path for every system that registers a command.
- **Log retention**: the ten most recent session logs are kept. The number is a configurable
  default, not a fixed rule.
- **Default severity threshold**: information and above in ordinary runs, with debug enabled by
  opt-in, so ordinary sessions stay readable.
- **Log format**: plain text, one entry per line, readable directly in a text editor without
  tooling. "Structured" means every entry carries the same named fields in a consistent order, not
  that the file is machine-parsed by a downstream system.
- **Screenshot format and location**: PNG files written directly into `artifacts/` at the repository
  root, flat, with no subfolders and no automatic timestamping — a repeat capture under the same
  name replaces the previous one so the latest evidence is always at a predictable path.
- **`artifacts/` is disposable**: it is not tracked in version control and may be deleted at any
  time; nothing may depend on its contents surviving.
- **Verification stages**: build, then the automated code-style check, then the engine-free test
  suite, then the engine-based test suite, then one screenshot. The style stage was added to match
  Constitution VI ("Standards Are Automated") and the engine tier to match Constitution II
  ("Test-First, Two Tiers"), both of which make their stage a required part of the gate. The stage
  order and the fail-fast behavior are the fixed part, the stage list is not.
- **Environment**: development happens without a GPU or real display, so every automated path must
  work under software rendering, and this feature must not assume a windowed session.
- **Performance overlay audience**: the overlay is developer-facing and shares the console's
  gating — off by default, and in a distributed build reachable only where the console is.
- **Percentiles are computed over the session's own samples**, not against any historical baseline;
  comparing sessions to each other is explicitly out of scope.
- **Draw calls and memory come from engine-provided counters**: the spec does not dictate how they
  are obtained, only that they are shown and are the figures a developer would expect.
- **Golden-image comparison is in scope** (changed after the spec was first written, to match
  Constitution IV, which requires golden reference screenshots). References are generated and
  compared in the development container only: the container rasterizes in software while the host's
  editor uses its real GPU driver, so the two do not produce identical pixels. That is why the
  comparison uses a tolerance rather than demanding an exact match.

## Out of Scope

- Remote or networked log collection, log shipping, or crash reporting to an external service.
- An approval or review workflow around golden reference images. Comparing a capture against a
  reference is in scope (FR-035, FR-036); a process for reviewing and signing off updates is not.
- Gameplay-specific console commands. This feature delivers the console and the mechanism for
  registering commands, plus `help` and `screenshot`; each gameplay system contributes its own
  commands as it is built.
- A graphical log viewer, in-game log overlay, or filtering UI. Logs are read as files.
- Video or animated capture, and capture of anything other than the rendered view.
- Continuous integration configuration. The verification command is what such a system would call;
  wiring it up is separate.
- A full profiler. Per-system or per-function timing breakdowns, flame graphs, allocation tracking,
  and trend history compared across past sessions all remain out. The live overlay and the
  end-of-session statistics in User Story 5 are in scope (FR-037 onward); attributing *where* the
  time goes is not.
