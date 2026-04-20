#!/usr/bin/env bash
#
# trigger-critic-on-sonnet.sh
#
# SubagentStop hook: when builder-sonnet or bug-fixer-sonnet completes, inject
# a system-level instruction into the parent agent's context telling it to
# spawn critic-opus for review before doing anything else.
#
# Loop prevention: for any other agent_type (critic-opus, writer-haiku,
# architect-opus, general-purpose, etc.) the hook exits silently so the
# review agent's own completion does not retrigger another review cycle.
#
# This script uses only POSIX tools (no jq) so it works on any bash shell,
# including minimal Git-Bash installs on Windows.
#
# Usage: registered in .claude/settings.json under hooks.SubagentStop.
# Input:  JSON on stdin
# Output: JSON on stdout OR empty (pass-through). Exit 0 always.

set -euo pipefail

# --- read hook input --------------------------------------------------------

input="$(cat)"

# --- extract agent_type without jq ------------------------------------------

agent_type="$(
  printf '%s' "$input" \
    | grep -oE '"agent_type"[[:space:]]*:[[:space:]]*"[^"]*"' \
    | sed -E 's/.*"agent_type"[[:space:]]*:[[:space:]]*"([^"]*)".*/\1/' \
    | head -n1 \
    || true
)"

# --- routing ----------------------------------------------------------------

case "${agent_type:-}" in
  builder-sonnet|bug-fixer-sonnet)
    cat <<'JSON'
{
  "hookSpecificOutput": {
    "hookEventName": "SubagentStop",
    "additionalContext": "AUTO-REVIEW HOOK FIRED.\n\nA Sonnet implementation subagent (builder-sonnet or bug-fixer-sonnet) just finished. Per the auto-review policy in .claude/AGENTS.md you MUST now spawn `critic-opus` via the Task tool to review the changes BEFORE doing anything else.\n\nHard rules for this turn:\n1. Do NOT write a summary to the user yet.\n2. Do NOT mark any task as completed yet.\n3. Do NOT move on to another task or file.\n4. Your very next action must be a Task tool call with subagent_type=\"critic-opus\".\n5. If critic-opus returns REQUEST CHANGES or REJECT, spawn bug-fixer-sonnet (or builder-sonnet) to fix the issues, then re-run critic-opus. Loop until APPROVE.\n6. Only after APPROVE may you report back to the user.\n\nThis instruction was injected by the SubagentStop hook at .claude/hooks/trigger-critic-on-sonnet.sh and is not optional."
  }
}
JSON
    exit 0
    ;;

  *)
    # Pass-through for every other agent type (prevents infinite review loop).
    exit 0
    ;;
esac
