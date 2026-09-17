#!/usr/bin/env bash
# Regenerates lib/refs: reference-only copies (public API signatures, no method bodies) of the
# game and BepInEx assemblies, so CI can build without the game installed.
# Needs the refasmer tool: dotnet tool install -g JetBrains.Refasmer.CliTool
# The installed apphost targets net6.0; on machines without a net6.0 runtime this script
# falls back to running the tool's dll directly with `DOTNET_ROLL_FORWARD=Major dotnet ...`.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
game="${GAME_PATH:-/var/mnt/data/SteamLibrary/steamapps/common/Stonewards}/Stonewards_Data/Managed"
bepinex="${PROFILE_PATH:-$HOME/.config/r2modmanPlus-local/Stonewards/profiles/Default}/BepInEx/core"
out="$root/lib/refs"

# Keep in sync with the <Reference> items in src/AdminMenu/AdminMenu.csproj.
game_dlls=(Assembly-CSharp Mirror com.rlabrecque.steamworks.net Unity.InputSystem UnityEngine UnityEngine.AnimationModule UnityEngine.CoreModule
    UnityEngine.InputLegacyModule UnityEngine.PhysicsModule UnityEngine.TextRenderingModule
    UnityEngine.UIElementsModule)
bepinex_dlls=(BepInEx 0Harmony)

# Resolve a working refasmer invocation into the refasmer_cmd array: prefer the PATH apphost,
# and fall back to running the tool's net6.0 dll directly (with roll-forward) when the apphost's
# target runtime isn't installed. Assigns the global array directly rather than going through
# `$(...)` + word-splitting, so a failure here actually exits the script (not just a subshell)
# and a tool path containing spaces still works.
resolve_refasmer() {
    if command -v refasmer >/dev/null 2>&1 && refasmer --help >/dev/null 2>&1; then
        refasmer_cmd=(refasmer)
        return
    fi
    local dll
    dll="$(find "$HOME/.dotnet/tools/.store/jetbrains.refasmer.clitool" \
        -path '*/tools/net6.0/any/RefasmerCliTool.dll' 2>/dev/null | sort -V | tail -1)"
    if [ -z "$dll" ]; then
        echo "refasmer not found. Install with: dotnet tool install -g JetBrains.Refasmer.CliTool" >&2
        exit 1
    fi
    refasmer_cmd=(env DOTNET_ROLL_FORWARD=Major dotnet "$dll")
}

refasmer_cmd=()
resolve_refasmer

rm -rf "$out"
mkdir -p "$out"
for name in "${game_dlls[@]}"; do "${refasmer_cmd[@]}" -q -c --omit-non-api-members=true -O "$out" "$game/$name.dll"; done
for name in "${bepinex_dlls[@]}"; do "${refasmer_cmd[@]}" -q -c --omit-non-api-members=true -O "$out" "$bepinex/$name.dll"; done
ls -l "$out"
