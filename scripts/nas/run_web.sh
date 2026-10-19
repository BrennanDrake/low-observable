#!/usr/bin/env bash
# Runs the latest playtest build on the NAS (or any Docker host on the LAN).
#   nas_reg=192.168.1.203:5000 PORT=8088 TAG=latest ./scripts/nas/run_web.sh
# Playtesters then open http://<nas-ip>:<PORT>/ in a browser.
set -euo pipefail
NAS_REG="${nas_reg:-192.168.1.203:5000}"
IMAGE="${IMAGE:-low-observable-web}"
TAG="${TAG:-latest}"
PORT="${PORT:-8088}"
NAME="${NAME:-low-observable-web}"

docker pull "$NAS_REG/$IMAGE:$TAG"
docker rm -f "$NAME" >/dev/null 2>&1 || true
docker run -d --name "$NAME" --restart unless-stopped -p "$PORT:80" "$NAS_REG/$IMAGE:$TAG"
docker inspect --format '{{ index .Config.Labels "org.opencontainers.image.version" }} (git {{ index .Config.Labels "org.opencontainers.image.revision" }})' "$NAME"
echo "Serving on port $PORT"
