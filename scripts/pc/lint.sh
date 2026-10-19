#!/usr/bin/env bash
# Quality gate run before pushing and in CI: C# formatting/analyzers on the core, ruff on tools/,
# shellcheck on scripts (if installed). Exits non-zero on the first failing check.
set -euo pipefail
cd "$(dirname "$0")/../.."

echo "[lint] dotnet build (warnings are errors)"
dotnet build dotnet/LowObservable.Core/LowObservable.Core.csproj -nologo -v quiet

echo "[lint] dotnet format --verify-no-changes"
dotnet format LowObservable.sln --verify-no-changes --verbosity quiet

if command -v ruff >/dev/null 2>&1; then
    echo "[lint] ruff"
    ruff check tools
else
    echo "[lint] ruff not installed; skipping (pip install -r requirements-dev.txt)"
fi

if command -v shellcheck >/dev/null 2>&1; then
    echo "[lint] shellcheck"
    shellcheck scripts/pc/*.sh scripts/nas/*.sh scripts/cloud/*.sh
else
    echo "[lint] shellcheck not installed; skipping"
fi
echo "[lint] ok"
