#!/usr/bin/env bash
# SessionStart hook for Claude Code cloud sessions (wired in .claude/settings.json). Installs the
# .NET 8 SDK from the Ubuntu archive if it is missing, so `make test` works in a fresh container.
# Does nothing on local machines.
set -euo pipefail
if [[ "${CLAUDE_CODE_REMOTE:-}" != "true" ]]; then
    exit 0
fi
if ! command -v dotnet >/dev/null 2>&1; then
    (apt-get update -qq && apt-get install -y -qq dotnet-sdk-8.0) >/dev/null 2>&1 || {
        echo "Could not install dotnet-sdk-8.0; core tests will not run in this session." >&2
        exit 0
    }
fi
cd "$(dirname "$0")/../.."
dotnet restore LowObservable.sln >/dev/null 2>&1 || true
