# Project notes for Claude Code

## Environment
- You are in a Podman container. Godot 4.7.2 (.NET) is on PATH as `godot` with .NET SDK 10 (net10.0 target). Host runs the same versions.
- No GPU, no real display. Rendering only works through `xvfb-run` (software).

## Commands
- Build: `dotnet build`
- Re-import after adding assets: `godot --headless --import`
- Run headless smoke check: `godot --headless --quit-after 60 2>&1 | grep -i "script error\|parse error" && echo FAIL || echo OK`