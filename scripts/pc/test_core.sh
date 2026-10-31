#!/usr/bin/env bash
# Runs the engine-free core's unit tests. Works anywhere .NET 8 is installed (dev box, NAS, CI, cloud).
set -euo pipefail
cd "$(dirname "$0")/../.."
dotnet test dotnet/LowObservable.Core.Tests/LowObservable.Core.Tests.csproj -nologo "$@"
