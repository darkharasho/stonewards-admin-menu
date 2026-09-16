#!/usr/bin/env bash
# Regenerates lib/refs: reference-only copies (public API signatures, no method bodies) of the
# game and BepInEx assemblies, so CI can build without the game installed.
# Needs the refasmer tool: dotnet tool install -g JetBrains.Refasmer.CliTool
# The installed apphost targets net6.0; on machines without a net6.0 runtime this script
# falls back to running the tool's dll directly with `dotnet ... --roll-forward Major`.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
game="${GAME_PATH:-/var/mnt/data/SteamLibrary/steamapps/common/Stonewards}/Stonewards_Data/Managed"
bepinex="${PROFILE_PATH:-$HOME/.config/r2modmanPlus-local/Stonewards/profiles/Default}/BepInEx/core"
out="$root/lib/refs"

# Keep in sync with the <Reference> items in src/AdminMenu/AdminMenu.csproj.
game_dlls=(Assembly-CSharp Mirror Unity.InputSystem UnityEngine UnityEngine.CoreModule
    UnityEngine.InputLegacyModule UnityEngine.PhysicsModule UnityEngine.TextRenderingModule
    UnityEngine.UIElementsModule)
bepinex_dlls=(BepInEx 0Harmony)

# Resolve a working refasmer invocation: prefer the PATH apphost, and fall back to running the
# tool's net6.0 dll directly (with roll-forward) when the apphost's target runtime isn't installed.
resolve_refasmer() {
    if command -v refasmer >/dev/null 2>&1 && refasmer --help >/dev/null 2>&1; then
        echo "refasmer"
        return
    fi
    local dll
    dll="$(find "$HOME/.dotnet/tools/.store/jetbrains.refasmer.clitool" \
        -path '*/tools/net6.0/any/RefasmerCliTool.dll' 2>/dev/null | sort -V | tail -1)"
    if [ -z "$dll" ]; then
        echo "refasmer not found. Install with: dotnet tool install -g JetBrains.Refasmer.CliTool" >&2
        exit 1
    fi
    echo "env DOTNET_ROLL_FORWARD=Major dotnet $dll"
}

read -r -a refasmer_cmd <<<"$(resolve_refasmer)"

rm -rf "$out"
mkdir -p "$out"
for name in "${game_dlls[@]}"; do "${refasmer_cmd[@]}" -q -c --omit-non-api-members=true -O "$out" "$game/$name.dll"; done
for name in "${bepinex_dlls[@]}"; do "${refasmer_cmd[@]}" -q -c --omit-non-api-members=true -O "$out" "$bepinex/$name.dll"; done
ls -l "$out"
