#!/bin/bash
# planning-with-files: Stop hook for Codex
# Reused from the Cursor integration; Codex adapts followup_message separately.

PLAN_FILE="task_plan.md"

if [ ! -f "$PLAN_FILE" ]; then
    exit 0
fi

TOTAL=$(grep -c "### Phase" "$PLAN_FILE" || true)
COMPLETE=$(grep -cF "**Status:** complete" "$PLAN_FILE" || true)
IN_PROGRESS=$(grep -cF "**Status:** in_progress" "$PLAN_FILE" || true)
PENDING=$(grep -cF "**Status:** pending" "$PLAN_FILE" || true)

if [ "$COMPLETE" -eq 0 ] && [ "$IN_PROGRESS" -eq 0 ] && [ "$PENDING" -eq 0 ]; then
    COMPLETE=$(grep -c "\[complete\]" "$PLAN_FILE" || true)
    IN_PROGRESS=$(grep -c "\[in_progress\]" "$PLAN_FILE" || true)
    PENDING=$(grep -c "\[pending\]" "$PLAN_FILE" || true)
fi

: "${TOTAL:=0}"
: "${COMPLETE:=0}"
: "${IN_PROGRESS:=0}"
: "${PENDING:=0}"

if [ "$COMPLETE" -eq "$TOTAL" ] && [ "$TOTAL" -gt 0 ]; then
    echo "{\"followup_message\": \"[planning-with-files] ALL PHASES COMPLETE ($COMPLETE/$TOTAL). If the user has additional work, add new phases to task_plan.md before starting.\"}"
    exit 0
fi

echo "{\"followup_message\": \"[planning-with-files] Task incomplete ($COMPLETE/$TOTAL phases done). Update progress.md, then read task_plan.md and continue working on the remaining phases.\"}"
exit 0
