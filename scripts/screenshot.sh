#!/usr/bin/env bash
# Captures a screenshot of the placeholder main scene without a display.
# Usage: scripts/screenshot.sh [name]
# See specs/001-dev-foundations/contracts/cli-scripts.md
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

name="${1:-main}"
output_path="${repo_root}/artifacts/${name}.png"

cd "${repo_root}"

# Godot's exit code is not trustworthy (research R4); success is asserted positively
# below by checking the output file. Capture output only to surface it on failure.
godot_output="$(timeout 120 xvfb-run -a godot \
    --rendering-method forward_plus --rendering-driver vulkan --audio-driver Dummy \
    -- --screenshot "${name}" 2>&1)" || true

if [[ ! -s "${output_path}" ]]; then
    echo "screenshot.sh: failed to write a non-empty PNG at ${output_path}" >&2
    echo "${godot_output}" >&2
    exit 1
fi

echo "${output_path}"
