#!/usr/bin/env bash
# Startet den ADR-Manager. Legt beim ersten Aufruf ein venv an und
# installiert die Requirements, danach wird die GUI gestartet.
# Alle Argumente werden durchgereicht, z.B.:
#   ./start.sh --new "Titel der Entscheidung"
set -euo pipefail
cd "$(dirname "$0")"

VENV_DIR="venv"

if [ ! -d "$VENV_DIR" ]; then
    echo "Erstelle virtuelle Umgebung in $VENV_DIR ..."
    python3 -m venv "$VENV_DIR"
fi

# shellcheck disable=SC1091
source "$VENV_DIR/bin/activate"

pip install --quiet --upgrade pip
pip install --quiet -r requirements.txt

python adr_manager.py "$@"
