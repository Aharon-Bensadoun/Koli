#!/usr/bin/env bash
# Koli LIGHT motion video -> MP4  (macOS / Linux)
# Usage:  ./render.sh                full render
#         ./render.sh --only-capture
set -euo pipefail
cd "$(dirname "$0")"

if [ ! -d node_modules ]; then
  echo "[render] Installing puppeteer (first run only)..."
  npm install
fi

node render.js "$@"
echo "[render] Output: ../out/koli-promo-light.mp4"
