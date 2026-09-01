# Feature Specification: Developer Foundations

**Feature Branch**: `001-dev-foundations`

**Created**: 2026-09-01

**Status**: Draft

**Input**: User description: "Developer foundations: the game has structured file logging, an in-game dev console toggled with backtick that lists and runs commands, a screenshot harness for headless verification, and a verify script that runs build, tests, and a screenshot. A developer can open the console, type help, see commands, run \"screenshot main\", and find the PNG in artifacts/. Logs from a play session are readable in the user data logs folder after quitting."

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
   session log afterwards, **Then** the entries written before the failure are present and the
   failure itself is recorded with its details.
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
- **Capture requested with nothing rendered yet**: requesting a screenshot before the first frame is
  drawn must fail with a clear reason rather than writing a blank or zero-byte file.
- **Log destination unwritable**: if the logs folder cannot be created or written (permissions, full
  disk), the game must report this on the terminal and keep running rather than failing to start.
- **Two sessions at once**: two copies of the game running simultaneously must not write to the same
  session log file or corrupt one another's records.
- **Abrupt termination**: a session killed without a clean shutdown must still leave the entries
  written up to that point readable on disk.
- **Invalid screenshot name**: a name containing path separators or otherwise unusable characters
  must be rejected with a clear message, and must not write outside `artifacts/`.

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
- **FR-005**: Log entries MUST be readable on disk after the game exits, including entries written
  before an abnormal termination.
- **FR-006**: The system MUST retain a bounded number of recent session logs and remove older ones
  automatically, so the logs folder does not grow without limit.
- **FR-007**: When the game runs without a display, log entries MUST also appear on the standard
  output stream so an automated caller can read them live.
- **FR-008**: Failures MUST NOT be discarded silently: an error that is handled MUST be logged with
  its cause, and an error that is not handled MUST be allowed to surface.

#### Developer console

- **FR-009**: The game MUST provide an in-game console that opens and closes on a single key press,
  bound by default to backtick and remappable, available from any scene without restarting.
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
  without a display or GPU, and MUST signal success or failure to its caller.
- **FR-027**: A failed capture MUST report the reason and MUST NOT leave an empty or partially
  written image behind.

#### Verification

- **FR-028**: The project MUST provide a single command that runs, in order: the build, the test
  suites, and a screenshot capture.
- **FR-029**: Verification MUST print a per-stage pass or fail summary and the path of the captured
  screenshot.
- **FR-030**: Verification MUST stop at the first failing stage, name that stage, and surface enough
  detail to identify the cause.
- **FR-031**: Verification MUST signal overall success only when every stage passed, in a form an
  automated caller can branch on.
- **FR-032**: Verification MUST run to completion unattended on a machine with no display and no
  GPU.

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

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer who has not seen the project before can open the console, list the
  available commands, and produce a screenshot file in under 2 minutes, without reading source code
  or documentation beyond `help`.
- **SC-002**: 100% of completed play sessions leave a readable session log file covering startup
  through shutdown; sessions terminated abruptly leave the entries written up to that point.
- **SC-003**: Screenshot capture invoked with no display available succeeds in 10 out of 10
  consecutive attempts, each producing a non-empty image at the expected dimensions.
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

## Assumptions

These defaults were chosen where the feature description did not specify a detail. Each is a
reversible decision, recorded here so planning can challenge it.

- **Audience**: the only user is the solo developer and the automated agents working on their
  behalf. No end player ever sees the console, and no multi-user or permission model is needed.
- **`screenshot main` semantics**: the argument names the output file (yielding `artifacts/main.png`)
  and the capture is of whatever is currently on screen. It is not a request to load a scene called
  "main" before capturing.
- **A capture target exists**: because the project currently has no scene to photograph, a minimal
  placeholder main scene is in scope as the thing the screenshot harness and verification command
  capture. It carries no gameplay.
- **Console availability**: the console is present in development builds. Whether it ships in a
  distributed release build is deferred; nothing in this feature depends on the answer.
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
- **Verification stages**: build, then all test suites, then one screenshot. Adding stages later is
  expected; the order and fail-fast behavior are the fixed part.
- **Environment**: development happens without a GPU or real display, so every automated path must
  work under software rendering, and this feature must not assume a windowed session.
- **Golden-image comparison is not part of this feature**: the harness captures evidence; comparing
  a capture against a stored reference image is separate work that builds on it.

## Out of Scope

- Remote or networked log collection, log shipping, or crash reporting to an external service.
- Automatic comparison of screenshots against golden reference images, and any diffing or approval
  workflow around them.
- Gameplay-specific console commands. This feature delivers the console and the mechanism for
  registering commands, plus `help` and `screenshot`; each gameplay system contributes its own
  commands as it is built.
- A graphical log viewer, in-game log overlay, or filtering UI. Logs are read as files.
- Video or animated capture, and capture of anything other than the rendered view.
- Continuous integration configuration. The verification command is what such a system would call;
  wiring it up is separate.
- Performance profiling, frame-time overlays, or metrics collection.
