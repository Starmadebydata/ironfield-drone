#!/usr/bin/env bash
# Regenerate every blockout model into Assets/Art/**.
# Usage: tools/build_assets.sh   (run from the repo root or anywhere)
set -euo pipefail

BLENDER="${BLENDER:-/opt/homebrew/bin/blender}"
if [ ! -x "$BLENDER" ]; then
  BLENDER="/Applications/Blender.app/Contents/MacOS/Blender"
fi
if [ ! -x "$BLENDER" ]; then
  echo "Blender not found. Set BLENDER=/path/to/blender" >&2
  exit 1
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPTS=(drone.py tank.py ifv.py truck.py ruins_kit.py)

for s in "${SCRIPTS[@]}"; do
  echo "=== $s ==="
  "$BLENDER" --background --factory-startup \
    --python "$ROOT/tools/blender/$s" \
    --python-exit-code 1
done

echo
echo "Done. FBX files:"
find "$ROOT/Assets/Art" -name '*.fbx' -print
