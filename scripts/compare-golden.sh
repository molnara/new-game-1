#!/usr/bin/env bash
# Compares a capture against its committed reference.
# Usage: scripts/compare-golden.sh <candidate.png> <golden.png> [threshold]
# See specs/001-dev-foundations/contracts/cli-scripts.md
set -euo pipefail

candidate="${1:?usage: compare-golden.sh <candidate.png> <golden.png> [threshold]}"
golden="${2:?usage: compare-golden.sh <candidate.png> <golden.png> [threshold]}"
threshold="${3:-0}"

if [[ ! -s "${candidate}" ]]; then
    echo "compare-golden.sh: candidate image missing or empty: ${candidate}" >&2
    exit 1
fi

if [[ ! -s "${golden}" ]]; then
    echo "compare-golden.sh: no golden reference at ${golden}" >&2
    echo "compare-golden.sh: generate one with scripts/update-golden.sh" >&2
    exit 1
fi

# `compare -metric AE` writes the differing pixel count to stderr and exits non-zero
# when the images differ (research R2, R3), so capture stderr and do not let set -e
# abort on that expected exit.
compare_output="$(compare -metric AE "${candidate}" "${golden}" null: 2>&1 1>/dev/null)" || true

if ! [[ "${compare_output}" =~ ^[0-9]+(\.[0-9]+)?$ ]]; then
    echo "compare-golden.sh: comparison failed: ${compare_output}" >&2
    exit 1
fi

diff_pixels="${compare_output%%.*}"

echo "compare-golden.sh: ${diff_pixels} differing pixels (threshold: ${threshold})"

if (( diff_pixels > threshold )); then
    echo "compare-golden.sh: ${diff_pixels} pixels differ, exceeding threshold ${threshold}" >&2
    exit 1
fi

exit 0
