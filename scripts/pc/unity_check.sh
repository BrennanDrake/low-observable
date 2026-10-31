#!/usr/bin/env bash
# Type-checks the Unity package without the Unity editor: compiles the runtime scripts against Unity's
# reference assemblies (both input backends) and the editor scripts against UnityEditor.dll. Needs
# nuget.org the first time. Catches compile errors, not runtime behaviour; still open the editor before
# shipping. See dotnet/LowObservable.Unity.TypeCheck/*.csproj for what is and isn't covered.
set -euo pipefail
cd "$(dirname "$0")/../.."

echo "[unity-check] runtime, legacy Input Manager"
dotnet build dotnet/LowObservable.Unity.TypeCheck/LowObservable.Unity.TypeCheck.csproj -nologo -v quiet
echo "[unity-check] runtime, Input System (stubbed)"
dotnet build dotnet/LowObservable.Unity.TypeCheck/LowObservable.Unity.TypeCheck.csproj -nologo -v quiet -p:InputSystem=true
echo "[unity-check] editor scripts"
dotnet build dotnet/LowObservable.Unity.EditorCheck/LowObservable.Unity.EditorCheck.csproj -nologo -v quiet
echo "[unity-check] ok"
