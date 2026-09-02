## Dev container

All commands run from `container/` on the host unless noted.

### Daily
| Task | Command |
|---|---|
| Start (or resume) the container | `podman compose up -d` |
| Shell into it | `podman compose exec gamedev bash` |
| Run Claude Code directly | `podman compose exec gamedev claude` |
| Resume last Claude session | `podman compose exec gamedev claude --continue` |
| Stop (keeps container + volumes) | `podman compose stop` |
| Remove container (keeps volumes) | `podman compose down` |

### After changing the Containerfile
    podman compose up -d --build --force-recreate

`--build` rebuilds the image; `--force-recreate` replaces the running container with
one from the new image (podman-compose won't do this on its own). Any Claude session
inside is killed — exit it first, then `claude --continue` afterward.

### After changing only compose.yaml (env, mounts, devices)
    podman compose up -d --force-recreate

### Rebuild from scratch (broken cache, base image changed)
    podman compose build --no-cache && podman compose up -d --force-recreate

### Verify what's running
    podman ps --format '{{.Names}}  {{.Image}}  {{.CreatedAt}}'
    podman images | grep newgame1
    podman compose exec gamedev id            # uid=1000(dev) gid=1000(dev)
    podman compose exec gamedev env | grep GIT_

If the container's CreatedAt is older than the image, you're on a stale container —
run the force-recreate command.

### Volumes (persistent state)
| Volume | Holds |
|---|---|
| `newgame1_claude-state` | Claude Code login + settings |
| `newgame1_nuget-cache` | NuGet packages |
| `newgame1_godot-data` | Export templates, `user://` data, game logs |
| `newgame1_godot-cache` | Shader/import cache |

    podman volume ls                          # list
    podman volume inspect newgame1_godot-data # host path (for reading logs)
    podman volume rm newgame1_godot-cache     # reset one (container must be down)

Volumes survive `down`, rebuilds, and image changes. Only `podman compose down -v`
deletes them — avoid unless you intend to log in to Claude again.
