#!/usr/bin/env bash
set -euo pipefail

# Render every PlantUML diagram in docs/uml to PNG using the official image.
# Run from anywhere; the script always works from the repository root.
cd "$(dirname "$0")/.."

docker run --rm -v "$PWD/docs/uml:/data" plantuml/plantuml -tpng /data/*.puml
