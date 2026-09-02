# Contract: Developer Console Command Surface

**Feature**: 001-dev-foundations | **Plan**: [../plan.md](../plan.md)

The console is a user-facing interface for the developer. This is its contract: what is typed, what
comes back, and what is guaranteed regardless of which command runs.

---

## Invocation

```
<command> [args...]
```

Tokens are whitespace-separated. Double quotes group a token containing spaces. An unterminated
quote is a parse error and runs nothing.

## Universal guarantees

Every command, without exception:

1. Returns a result — success with a message, or failure with a reason. It never terminates the game
   and never leaves the console unusable (FR-016).
2. Has its invocation and its result recorded in the session log (FR-018).
3. Is listed by `help` with a one-line summary (FR-012).
4. Reports failure in a form that names what went wrong, not merely that something did.

A handler that throws is caught at the registry boundary, converted to a failure result, and logged
with its exception detail. Exceptions never cross into the UI layer, and are never silently
discarded (constitution III).

---

## `help`

| | |
|---|---|
| **Usage** | `help` or `help <command>` |
| **Summary** | List available commands, or explain one. |

**`help`** — lists every registered command, ordered by name, one per line as `name — summary`.
Reflects what is registered at the moment it runs; a system that has not started yet is simply
absent rather than causing an error.

**`help <command>`** — prints that command's summary and usage.

**Failure**: `help <unknown>` fails with a message naming the unknown command and pointing back at
bare `help`.

---

## `screenshot`

| | |
|---|---|
| **Usage** | `screenshot [name]` |
| **Summary** | Capture the current view to `artifacts/<name>.png`. |

**Behavior**: captures the current viewport and writes a PNG into `artifacts/`, creating the folder
if absent (FR-023). Omitting `name` uses the default name (FR-021). An existing file of the same name
is replaced, and the result message says so (FR-024).

**Success message**: reports the full path written (FR-022).

**Failure conditions**, each with a distinct message and no file left behind (FR-025, FR-027):

| Condition | Result |
|---|---|
| Name contains a path separator or `..` | Rejected before any capture is attempted. |
| Name contains characters illegal in a file name | Rejected, naming the offending input. |
| Viewport texture unavailable | Fails with the reason (this is the `--headless` case — see research R1). |
| Write fails (permissions, disk full) | Fails with the underlying reason; no partial file remains. |

---

## Unknown commands

Submitting an unregistered name produces a failure naming the input and suggesting `help`
(FR-015). The game continues running normally.

---

## Extension contract

Any system may add commands (FR-013). Registering requires supplying name, summary, usage, and
handler. Registration is the whole integration — no shared list is edited, no existing system is
touched (SC-006).

Registering a name that is already taken raises a duplicate error at registration time rather than
silently shadowing the earlier command (FR-014). This surfaces as a logged error during startup.
