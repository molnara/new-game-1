#!/usr/bin/env bash
# One-command verification gate (constitution "Development Workflow & Quality Gates").
# Usage: scripts/verify.sh
# Runs stages in order, stopping at the first failure (FR-028, FR-030).
# See specs/001-dev-foundations/contracts/cli-scripts.md
set -uo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"
cd "${repo_root}"

screenshot_path=""

# Runs a stage function, printing PASS/FAIL and, on failure, the stage's output verbatim
# before stopping the gate. Adding a stage is one more call to this, in order (FR-028a).
run_stage() {
    local name="$1"
    local fn="$2"
    local logfile
    logfile="$(mktemp)"

    "${fn}" >"${logfile}" 2>&1
    local status=$?

    if (( status == 0 )); then
        echo "PASS: ${name}"
        rm -f "${logfile}"
    else
        echo "FAIL: ${name}"
        cat "${logfile}"
        rm -f "${logfile}"
        exit 1
    fi
}

stage_build() {
    dotnet build NewGame1.sln
}

stage_style() {
    # Bare `dotnet format` (no subcommand) so whitespace, style and analyzers all run
    # (research R13). --verify-no-changes: a gate must never rewrite the tree it judges.
    # Exit code is trustworthy here: 0 clean, non-zero when changes would be made.
    dotnet format NewGame1.sln --verify-no-changes --no-restore
}

stage_core_tests() {
    dotnet test tests/Core.Tests/NewGame1.Core.Tests.csproj
}

stage_godot_tests() {
    local output
    output="$(timeout 120 xvfb-run -a godot \
        --rendering-method forward_plus --rendering-driver vulkan --audio-driver Dummy \
        res://scenes/Main.tscn -- --run-tests --quit-on-finish 2>&1)"
    local exit_code=$?
    echo "${output}"

    if (( exit_code != 0 )); then
        return "${exit_code}"
    fi

    # research R14: a run executing zero tests exits 0 and prints "Passed: 0 | Failed: 0 |
    # Skipped: 0". The exit code alone cannot catch that, so the passed count must also be
    # asserted greater than zero (FR-028f).
    local passed
    passed="$(grep -oP 'Passed:\s*\K[0-9]+' <<< "${output}" | tail -1)"
    if [[ -z "${passed}" ]] || (( passed == 0 )); then
        echo "verify.sh: Godot test stage reported no passed tests (Passed: ${passed:-none found})" >&2
        return 1
    fi
    return 0
}

stage_screenshot() {
    screenshot_path="$("${script_dir}/screenshot.sh" main)"
    local status=$?
    if (( status != 0 )); then
        return "${status}"
    fi
    echo "${screenshot_path}"
}

stage_golden_compare() {
    "${script_dir}/compare-golden.sh" "${screenshot_path}" "${repo_root}/tests/golden/main.png"
}

run_stage "Build" stage_build
run_stage "Code style" stage_style
run_stage "Core tests" stage_core_tests
run_stage "Godot tests" stage_godot_tests
run_stage "Screenshot" stage_screenshot
run_stage "Golden compare" stage_golden_compare

echo "screenshot: ${screenshot_path}"
exit 0
