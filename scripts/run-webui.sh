#!/usr/bin/env bash
# =============================================================================
# NexusCheatFramework — WebUI Launch Script (Bash)
# =============================================================================
# Usage:
#   ./scripts/run-webui.sh <console-ip> [web-port]
#
# Arguments:
#   console-ip   (required) IP address of the PS4/PS5 with NexusFramework loaded
#   web-port     (optional) WebUI listen port (default: 9080)
#
# Prerequisites:
#   - .NET SDK 8.0+ installed
#   - NexusFramework payload loaded on the console
#   - Console and host PC on the same network
#
# Examples:
#   ./scripts/run-webui.sh 192.168.1.100
#   ./scripts/run-webui.sh 192.168.1.100 8080
# =============================================================================
set -euo pipefail

# --- Argument parsing ---
if [ $# -lt 1 ]; then
    echo "ERROR: Missing required argument <console-ip>"
    echo ""
    echo "Usage: $0 <console-ip> [web-port]"
    echo ""
    echo "  console-ip   IP address of the PS4/PS5 with NexusFramework loaded"
    echo "  web-port     WebUI listen port (default: 9080)"
    echo ""
    echo "Example:"
    echo "  $0 192.168.1.100"
    echo "  $0 192.168.1.100 8080"
    exit 1
fi

CONSOLE_IP="$1"
WEB_PORT="${2:-9080}"

# --- Validate IP format (basic) ---
if ! echo "$CONSOLE_IP" | grep -qE '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$'; then
    echo "ERROR: '$CONSOLE_IP' does not look like a valid IPv4 address"
    exit 1
fi

# --- Validate port ---
if ! echo "$WEB_PORT" | grep -qE '^[0-9]+$' || [ "$WEB_PORT" -lt 1 ] || [ "$WEB_PORT" -gt 65535 ]; then
    echo "ERROR: Port must be a number between 1 and 65535, got '$WEB_PORT'"
    exit 1
fi

# --- Check dotnet ---
if ! command -v dotnet &>/dev/null; then
    echo "ERROR: .NET SDK not found. Please install .NET SDK 8.0+ from https://dotnet.microsoft.com/download"
    exit 1
fi

# --- Determine project directory ---
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
WEB_PROJECT="$PROJECT_DIR/src/NexusCheatFramework.Web"

if [ ! -f "$WEB_PROJECT/NexusCheatFramework.Web.csproj" ]; then
    echo "ERROR: WebUI project not found at $WEB_PROJECT"
    echo "Make sure you are running this script from the repository root."
    exit 1
fi

# --- Print info ---
echo "============================================"
echo " NexusCheatFramework WebUI"
echo "============================================"
echo " Console IP:  $CONSOLE_IP"
echo " WebUI Port:  $WEB_PORT"
echo " WebUI URL:   http://localhost:$WEB_PORT"
echo ""
echo " IMPORTANT: Make sure the NexusFramework payload"
echo " is already running on the console ($CONSOLE_IP)."
echo ""
echo " Open the WebUI in your browser at:"
echo "   http://localhost:$WEB_PORT"
echo ""
echo " On the PS4/PS5 browser, open:"
echo "   http://<YOUR-PC-IP>:$WEB_PORT"
echo "============================================"
echo ""

# --- Launch WebUI ---
cd "$PROJECT_DIR"
exec dotnet run --project "$WEB_PROJECT" -- --ip "$CONSOLE_IP" --port "$WEB_PORT"
