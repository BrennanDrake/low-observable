#!/usr/bin/env bash
# Packages build/webgl into an nginx image and pushes it to the NAS registry, tagged the same way as
# the robot images in turtlebot-rfid-slam: :<VERSION>, :git-<sha>, :latest, with OCI labels.
#   nas_reg=192.168.1.203:5000 ./scripts/pc/build_web_image.sh
# Requires a committed HEAD and an existing WebGL build (./scripts/pc/build_unity.sh webgl).
set -euo pipefail
cd "$(dirname "$0")/../.."

NAS_REG="${nas_reg:-192.168.1.203:5000}"
IMAGE="${IMAGE:-low-observable-web}"
APP_VERSION=$(cat VERSION)
GIT_SHA=$(git rev-parse --short HEAD)

if [[ ! -f build/webgl/index.html ]]; then
    echo "No WebGL build at build/webgl. Run ./scripts/pc/build_unity.sh webgl first." >&2
    exit 1
fi
if ! git diff --quiet HEAD -- core unity; then
    echo "Warning: uncommitted changes in core/ or unity/; the image label will not match the build." >&2
fi

echo "[web-image] $NAS_REG/$IMAGE:$APP_VERSION (git $GIT_SHA)"
# Use the default builder (docker driver) so the daemon's insecure-registries list applies, as in the robot repo.
docker build -f docker/web/Dockerfile \
    --label "org.opencontainers.image.version=$APP_VERSION" \
    --label "org.opencontainers.image.revision=$GIT_SHA" \
    --label "org.opencontainers.image.title=low-observable-web" \
    -t "$NAS_REG/$IMAGE:$APP_VERSION" \
    -t "$NAS_REG/$IMAGE:git-$GIT_SHA" \
    -t "$NAS_REG/$IMAGE:latest" \
    .
for tag in "$APP_VERSION" "git-$GIT_SHA" latest; do
    docker push "$NAS_REG/$IMAGE:$tag"
done
echo "[web-image] pushed. On the NAS: ./scripts/nas/run_web.sh"
