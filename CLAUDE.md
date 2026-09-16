## Project Context

A BepInEx plugin for Stonewards that adds an in-game admin/cheat menu, following the same conventions as the author's existing Stonewards mods. The menu is toggled with a hotkey (F1 by default) and presents a polished, aesthetically pleasing UI rather than a bare debug panel. It exposes common admin actions such as resurrecting or killing players, spawning items, god mode, infinite stamina, and a super pickaxe.

## Goals

- Toggle the admin menu with a configurable hotkey, defaulting to F1
- Ship a visually polished, well-organized menu UI (not a raw debug list)
- Player actions: resurrect and kill players
- Item spawning with a searchable/browsable item list
- Cheat toggles: no death / god mode, infinite stamina, super pickaxe
- Match the structure, naming, and conventions of the author's existing Stonewards mods
- Persist settings and the hotkey binding via the BepInEx config file

## Out of scope

- Not a standalone launcher or external trainer app
- Not a general modding framework or API for other mods

## Suggested stack

- **BepInEx** — Matches the loader used by the author's other Stonewards mods
- **C# / .NET (Unity-targeted class library)** — Standard for BepInEx plugins against a Unity game
- **HarmonyX** — Bundled with BepInEx; needed to patch game methods for god mode, stamina, and pickaxe behavior
