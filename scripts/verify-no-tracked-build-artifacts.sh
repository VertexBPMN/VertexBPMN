#!/usr/bin/env bash
set -euo pipefail

tracked_artifacts="$(git ls-files | grep -E '(^|/)([Bb]in|[Oo]bj)(/|\\)' || true)"

if [[ -n "$tracked_artifacts" ]]; then
  echo "Tracked .NET build artifacts are not allowed:"
  echo "$tracked_artifacts"
  exit 1
fi

echo "No tracked .NET bin/obj artifacts found."
