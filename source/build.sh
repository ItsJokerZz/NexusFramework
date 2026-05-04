#!/bin/sh

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

cd "$SCRIPT_DIR/console"

MAKE_OPT="$1" 

if [ -n "$MAKE_OPT" ]; then
    make "$MAKE_OPT"
else
    make
fi

PS4_ELF="output/nexus-ps4.elf"
PS5_ELF="output/nexus-ps5.elf"
DEST="../libraries/C#/payloads"

[ -f "$PS4_ELF" ] && cp "$PS4_ELF" "$DEST/"
[ -f "$PS5_ELF" ] && cp "$PS5_ELF" "$DEST/"

cd ..
