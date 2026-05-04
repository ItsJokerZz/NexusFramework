#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

echo "==> dotnet --info"
dotnet --info | head -n 5 || { echo "ERROR: .NET SDK not found"; exit 1; }

echo "==> restore"
dotnet restore NexusCheatFramework.sln

echo "==> build"
dotnet build   NexusCheatFramework.sln -c Release --nologo

echo "==> test"
dotnet test    NexusCheatFramework.sln -c Release --nologo --no-build
