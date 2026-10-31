#!/usr/bin/env bash
# Lists repositories and tags on the NAS registry (same registry as the robot images).
#   nas_reg=192.168.1.203:5000 ./scripts/pc/registry_tags.sh [repository]
set -euo pipefail
NAS_REG="${nas_reg:-192.168.1.203:5000}"
if [[ $# -ge 1 ]]; then
    curl -fsS "http://$NAS_REG/v2/$1/tags/list"; echo
    exit 0
fi
for repo in $(curl -fsS "http://$NAS_REG/v2/_catalog" | sed -e 's/.*\[//' -e 's/\].*//' -e 's/"//g' -e 's/,/ /g'); do
    printf '%-32s ' "$repo"
    curl -fsS "http://$NAS_REG/v2/$repo/tags/list" | sed -e 's/.*"tags":\[//' -e 's/\].*//' -e 's/"//g'
    echo
done
