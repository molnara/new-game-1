---
name: pr-summary
description: Generate a PR description for the current spec-kit feature branch
---

Generate a pull request description for the current feature branch and write it
to artifacts/pr.md (create the directory if needed). Do not commit it.

Sources, in priority order:
1. specs/<current-branch>/spec.md — the intent and user stories
2. specs/<current-branch>/plan.md and research.md — decisions and trade-offs,
   including the Complexity Tracking table if any entries exist
3. specs/<current-branch>/tasks.md — what was actually delivered
4. `git log master..HEAD --oneline` and `git diff master...HEAD --stat`

Format (GitHub markdown, no headers larger than ###):

### Summary
2-4 sentences: what this delivers and why, in the spec's terms. No implementation detail.

### What's included
Bullets grouped by user story, each stating the delivered capability, not the task.

### Decisions worth knowing
Bullets for choices a reviewer might question: anything from Complexity Tracking,
constitution exemptions, deviations from the original spec noted during converge.
Omit the section if empty.

### Verification
How it was verified: test counts added (Core / Godot), whether verify.sh passes,
which screenshots were reviewed. State facts, not assurances.

### Not included / follow-ups
Anything descoped or deferred, with the reason.

Rules: under 400 words. Plain statements, no adjectives like "robust" or "comprehensive".
Don't list files changed. Don't restate the constitution. If tasks.md and the code
disagree, trust the code and say so.