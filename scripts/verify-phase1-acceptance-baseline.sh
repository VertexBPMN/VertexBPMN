#!/usr/bin/env bash
set -euo pipefail

configuration="${PHASE1_CONFIGURATION:-Release}"
project="tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj"
dotnet_command="${DOTNET_COMMAND:-dotnet}"

contracts=(
  "P1_AC_01_Deploy_Start_Service_UserTask_Complete_End|The calculateScore service-task handler was not executed by the public runtime path."
  "P1_AC_02A_TimerCatch_Waits_Until_Due_Then_Resumes_Exactly_Once|Timer catch wait token was not persisted by the public runtime path."
  "P1_AC_02B_BoundaryTimer_Interrupts_Waiting_Task_And_Resumes_Exactly_Once|Boundary timer precondition failed: the attached user task was not persisted."
  "P1_AC_03A_Message_Waits_Correlates_Only_Matching_Subscription_And_Resumes_Once|Message subscription wait token was not persisted by the public runtime path."
  "P1_AC_03B_Signal_Waits_Broadcasts_To_Matching_Subscriptions_And_Resumes_Once|Signal subscription wait tokens were not persisted for both process instances."
  "P1_AC_04_Host_Restart_Preserves_Wait_State_And_Resumes_Without_Duplication|Restart precondition failed: the durable user-task wait state was not persisted."
  "P1_AC_05_Parallel_Join_Waits_For_Both_Branches_And_Instances_Are_Isolated|Parallel split did not persist two isolated user-task branches per process instance."
)

for contract in "${contracts[@]}"; do
  method="${contract%%|*}"
  expected_diagnostic="${contract#*|}"

  set +e
  output=$("$dotnet_command" test "$project" \
    --configuration "$configuration" \
    --no-build \
    --no-restore \
    --verbosity minimal \
    --filter-method "*$method*" 2>&1)
  exit_code=$?
  set -e

  if [[ $exit_code -ne 2 ]]; then
    printf '%s\n' "$output"
    echo "$method must be an explicit red contract with test-failure exit code 2; actual exit code: $exit_code"
    exit 1
  fi

  if ! grep -Fq "$expected_diagnostic" <<<"$output"; then
    printf '%s\n' "$output"
    echo "$method failed for an unexpected reason; expected diagnostic: $expected_diagnostic"
    exit 1
  fi

  echo "$method: expected red runtime gap confirmed"
done

echo "All Phase 1 BPMN acceptance contracts are present and red for their documented runtime gaps."
