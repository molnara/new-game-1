#!/usr/bin/env bash
# Regenerates a capture target's committed golden reference from a fresh capture.
# Usage: scripts/update-golden.sh [name]
# Must NEVER be run as a verify.sh stage: a gate that regenerates its own
# expectation cannot fail. Run in the container only (research R2).
# See specs/001-dev-foundations/contracts/cli-scripts.md
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

name="${1:-main}"
golden_path="${repo_root}/tests/golden/${name}.png"

cd "${repo_root}"

captured_path="$("${script_dir}/screenshot.sh" "${name}")"

if [[ ! -s "${captured_path}" ]]; then
    echo "update-golden.sh: capture failed, leaving existing golden untouched" >&2
    exit 1
fi

replaced="false"
if [[ -e "${golden_path}" ]]; then
    replaced="true"
fi

mkdir -p "$(dirname "${golden_path}")"
cp "${captured_path}" "${golden_path}"

echo "update-golden.sh: wrote ${golden_path} (replaced existing: ${replaced})"
