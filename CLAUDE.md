# NewGame1 — notes for Claude Code

Architecture and process rules live in .specify/memory/constitution.md. This file is
environment facts only.

## Environment
- Podman container, Debian-based. No GPU, no display. The developer runs the Godot
  editor on the host against this same folder; ask before editing .tscn files.
- Godot 4.7.2 (.NET) on PATH as `godot`. Headless runs must use `--headless`.
- .NET SDK 10, projects target net10.0. `TargetFramework` must stay explicitly in
  NewGame1.csproj — the Godot editor inserts net8.0 if it's absent.
- Rendering only works via Xvfb + software GL:
  `xvfb-run -a godot --rendering-method gl_compatibility ...`
  First run after new assets is slow (shader compile + import). Not pixel-identical to GPU.
- ImageMagick is version 6: use `compare`/`convert` directly (no `magick` command).
  `compare -metric AE` prints to stderr; exit 1 = different, 2 = error.
- Godot import cache: new/moved asset files need `godot --headless --import` before
  headless runs can load them. Missing-resource errors on files that exist = stale cache.
- Logs: ~/.local/share/godot/app_userdata/new game 1/logs/ (game-*.log and godot.log).
- python3 is available for throwaway one-off commands only. Project scripts live in
  scripts/ as bash, or in C# under src/. Nothing checked in depends on Python.

## Workflow
- Feature work happens on spec-kit branches; master merges by squash PR (host side).
- During /speckit-implement, commit after each completed task phase with a
  conventional message. Include tasks.md checkbox updates. Never commit failing tests.
- Do not modify container/ without asking.
- Before reporting a task done, run scripts/verify.sh (once it exists) and read any
  screenshot it produces.