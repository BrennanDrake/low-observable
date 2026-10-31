#!/usr/bin/env bash
# Headless simulator: autopilot episodes without Unity. All arguments pass through, e.g.
#   ./scripts/pc/sim.sh --policy ghost --seeds 50
#   ./scripts/pc/sim.sh --policy all --seeds 5 --out experiments/runs
set -euo pipefail
cd "$(dirname "$0")/../.."
dotnet run -c Release --project dotnet/LowObservable.Sim -- "$@"
