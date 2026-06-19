#!/usr/bin/env bash
set -euo pipefail

export AETHERIS_RUN_LEGACY_TESTS=1
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec "$SCRIPT_DIR/test-all.sh" "$@"
