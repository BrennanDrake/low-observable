#!/usr/bin/env bash
# Escape campaign checks without Unity:
#   1. every level is reachable (sensors off, a walking bot must escape), and
#   2. a balance table: escape rate per level for the walker, sneaker and runner bots.
# Extra arguments go to the balance run, e.g. ./scripts/pc/stage_check.sh --seeds 20
set -euo pipefail
cd "$(dirname "$0")/../.."
dotnet run -c Release --project dotnet/LowObservable.Sim -- stage --check
dotnet run -c Release --project dotnet/LowObservable.Sim -- stage --policy all --seeds 6 "$@"
