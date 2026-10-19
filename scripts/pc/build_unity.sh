#!/usr/bin/env bash
# Builds the Unity project in batch mode (no editor window).
#   ./scripts/pc/build_unity.sh webgl     -> build/webgl/
#   ./scripts/pc/build_unity.sh linux     -> build/linux/
#   ./scripts/pc/build_unity.sh windows   -> build/windows/
# Set UNITY_PATH to the editor binary if it is not at the Unity Hub default for the version in
# unity/LowObservable/ProjectSettings/ProjectVersion.txt.
set -euo pipefail
cd "$(dirname "$0")/../.."

TARGET="${1:-webgl}"
PROJECT="unity/LowObservable"
case "$TARGET" in
    webgl)   METHOD="LowObservable.EditorTools.BuildScript.WebGL" ;;
    linux)   METHOD="LowObservable.EditorTools.BuildScript.Linux" ;;
    windows) METHOD="LowObservable.EditorTools.BuildScript.Windows" ;;
    *) echo "usage: $0 webgl|linux|windows" >&2; exit 2 ;;
esac

if [[ -z "${UNITY_PATH:-}" ]]; then
    if [[ -f "$PROJECT/ProjectSettings/ProjectVersion.txt" ]]; then
        VERSION=$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt")
        UNITY_PATH="$HOME/Unity/Hub/Editor/$VERSION/Editor/Unity"
    fi
fi
if [[ -z "${UNITY_PATH:-}" || ! -x "$UNITY_PATH" ]]; then
    echo "Unity editor not found. Open unity/LowObservable once in Unity Hub, or set UNITY_PATH." >&2
    exit 1
fi

GIT_SHA=$(git rev-parse --short HEAD)
APP_VERSION=$(cat VERSION)
mkdir -p build
echo "[unity] $TARGET build of v$APP_VERSION (git $GIT_SHA) with $UNITY_PATH"
"$UNITY_PATH" -batchmode -nographics -quit \
    -projectPath "$PROJECT" \
    -executeMethod "$METHOD" \
    -buildVersion "$APP_VERSION" -buildSha "$GIT_SHA" \
    -logFile "build/unity-$TARGET.log"
echo "[unity] done: build/$TARGET (log: build/unity-$TARGET.log)"
