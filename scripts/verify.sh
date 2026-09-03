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
    # --no-incremental and -warnaserror are both load-bearing (issue #3). Up-to-date projects are
    # not recompiled, so a warm build re-emits none of their diagnostics: the same tree that
    # cold-builds with 14 warnings reports "0 Warning(s)" on the second run. Forcing a full
    # recompile is what makes the count real; -warnaserror is what makes it blocking.
    # Deliberately here and not as TreatWarningsAsErrors in Directory.Build.props: the gate and CI
    # stay strict while a developer's inner-loop `dotnet build` stays fast and non-blocking.
    dotnet build NewGame1.sln --no-incremental -warnaserror -v minimal
}

stage_style() {
    # FR-028f: the style stage must fail if its configuration enforces no rule. Against the
    # repository's original four-line .editorconfig, `dotnet format` passes almost unconditionally
    # (research R13) — a green stage that checked nothing. Confirm at least one diagnostic is
    # actually enforced at warning or above before trusting a clean run.
    local enforced_rules
    enforced_rules="$(grep -cE '^\s*dotnet_diagnostic\.[A-Za-z0-9]+\.severity\s*=\s*(warning|error)\b' "${repo_root}/.editorconfig")"
    if (( enforced_rules == 0 )); then
        echo "verify.sh: .editorconfig enforces no diagnostic at warning or above — style stage would pass vacuously" >&2
        return 1
    fi

    # issue #3: the build stage's -warnaserror cannot see a diagnostic set to `severity = none` —
    # that suppresses at the analyzer, before MSBuild has a warning to promote, so it is invisible
    # to the ratchet by construction. "No warning is ignored" and "no warning is hidden" are two
    # failure modes needing two controls; this is the second. Silencing a rule stays allowed, but
    # only with a justification carrying an `Expiry:` condition in the comment block directly above
    # it, so the decision gets re-audited rather than inherited forever. To re-audit one: flip the
    # line to `= warning`, rebuild, and read what comes back.
    local unjustified
    unjustified="$(awk '
        /^[[:space:]]*#/ {
            if (!in_comment) { in_comment = 1; expiry = 0 }
            if (tolower($0) ~ /expiry:/) { expiry = 1 }
            next
        }
        /^[[:space:]]*dotnet_diagnostic\.[A-Za-z0-9]+\.severity[[:space:]]*=[[:space:]]*none([[:space:]]|$)/ {
            if (!in_comment || !expiry) { print "  .editorconfig:" FNR ": " $0 }
            next
        }
        { in_comment = 0; expiry = 0 }
    ' "${repo_root}/.editorconfig")"
    if [[ -n "${unjustified}" ]]; then
        echo "verify.sh: .editorconfig silences a diagnostic with no justification carrying an 'Expiry:' condition:" >&2
        echo "${unjustified}" >&2
        return 1
    fi

    # Bare `dotnet format` (no subcommand) so whitespace, style and analyzers all run
    # (research R13). --verify-no-changes: a gate must never rewrite the tree it judges.
    # Exit code is trustworthy here: 0 clean, non-zero when changes would be made.
    dotnet format NewGame1.sln --verify-no-changes --no-restore
}

stage_core_tests() {
    local output
    output="$(dotnet test tests/Core.Tests/NewGame1.Core.Tests.csproj 2>&1)"
    local exit_code=$?
    echo "${output}"

    if (( exit_code != 0 )); then
        return "${exit_code}"
    fi

    # FR-028f: mirror the Godot test stage's anti-vacuity check below — a run that discovers
    # and executes zero tests must not pass silently just because dotnet test's exit code is 0.
    local passed
    passed="$(grep -oP 'Passed:\s*\K[0-9]+' <<< "${output}" | tail -1)"
    if [[ -z "${passed}" ]] || (( passed == 0 )); then
        echo "verify.sh: Core test stage reported no passed tests (Passed: ${passed:-none found})" >&2
        return 1
    fi
    return 0
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
