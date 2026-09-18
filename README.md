# stonewards-admin-menu

A BepInEx plugin for Stonewards that adds an in-game admin/cheat menu, following the same conventions as the author's existing Stonewards mods. The menu is toggled with a hotkey (F1 by default) and presents a polished, aesthetically pleasing UI rather than a bare debug panel. It exposes common admin actions such as resurrecting or killing players, spawning items, god mode, infinite stamina, and a super pickaxe.

## Features

- **Players tab** — revive, heal, or kill any connected player, teleport to them, or (as host) bring them to you. Health and alive/dead status update live. **Stats** opens a per-player editor for every stat level-up rings raise, showing each current value and letting you set it. **Go to campfire** takes you to the level's campfire. As host, **Elevate** gives another player the admin menu for the session.
- **Items tab** — search and browse the item catalog and spawn any item, with an amount next to each one.
- **Resources tab** — add meta currency to your save, spawn any kind of treasure chest in front of you, and spawn dug-up resources and scrap.
- **Cheats tab** — toggle god mode, infinite stamina, infinite arrows & bombs, a super pickaxe (25x dig strength) and a super speed pickaxe (3x digging speed).
- A themed, tabbed panel matching the game's own menus, not a raw debug list.

## Opening the menu

Press **F1** to open and close the admin menu. Escape also closes it by default (configurable, see below).

### Rebinding

Settings are stored in `BepInEx/config/com.darkharasho.stonewards.adminmenu.cfg` and can be edited by hand or, if [darkharasho-ModSettings](https://github.com/darkharasho/stonewards-mod-settings) is installed, from the in-game **Mods** settings panel:

| Section | Key | Default | Description |
| --- | --- | --- | --- |
| `General` | `Enabled` | `true` | Allow the admin menu to be opened. |
| `General` | `TrustedAdmins` | empty | Steam IDs, comma-separated, that may use the admin menu in games you host, besides you. Everyone else only gets it as host. The mod author is trusted as well and is not affected by this setting — see [Mod author access](#mod-author-access). |
| `Controls` | `ToggleKey` | `F1` | Key that opens and closes the admin menu. |
| `Controls` | `CloseOnEscape` | `true` | Also close the admin menu with Escape, instead of opening the pause menu. |
| `Cheats` | `GodMode` | `false` | Keep your health topped up and block incoming damage. |
| `Cheats` | `InfiniteStamina` | `false` | Never run out of stamina when sprinting, climbing or blocking. |
| `Cheats` | `InfiniteAmmo` | `false` | Arrows, ballista bolts, bombs and other throwables are never used up. You need at least one to start with. |
| `Cheats` | `SuperPickaxe` | `false` | Dig with 25x strength, including the heavy dig bonus. |
| `Cheats` | `SuperSpeedPickaxe` | `false` | Dig 3x faster. |

## Mod author access

**The mod author (darkharasho, Steam ID `76561197987892075`) is always a trusted admin in any game hosted with this mod installed, and this cannot be turned off in the config.** It is not a default value you can clear: the ID is compiled into the plugin and added to the trusted list on every install, on top of whatever `TrustedAdmins` contains. In practice that means the author can open the admin menu in your session and use everything in it — reviving, healing and killing players, teleporting, spawning items and editing stats.

It works this way because a config default kept getting lost by accident: adding your own friends to `TrustedAdmins` replaces the value and drops the author with it.

If you don't want that, you have two options, and the plugin logs this grant on startup so it's never a surprise:

- **Don't install the mod**, or
- **Build it from source without it.** This mod is open source and will stay that way. Remove the `OwnerSteamId` constant in [`src/AdminMenu/Plugin.cs`](src/AdminMenu/Plugin.cs) (set it to `""`), rebuild, and the grant is gone — nothing else depends on it.

Hosts can also grant admin to anyone else themselves: see **Elevate** below.

## Multiplayer notes

The admin menu is host-only. The exception is players whose Steam IDs are listed in `TrustedAdmins`, which by default holds the mod author (darkharasho). When the host runs this mod, the host's list decides and is shared with the lobby, and a trusted client's host-only actions (Bring, stat changes) are carried out by the host. When the host doesn't run it, the joining player's own list decides, and only the actions the game already accepts from clients work.

As host you can also grant admin from the menu itself: each player's row has an **Elevate** button that gives them the admin menu for the rest of the session, and **Revoke** takes it back. The grant is held in memory only — it is never written to your config, and it is dropped when you leave the lobby, so it lasts exactly as long as the game you made it in. It is offered only for players who have this mod installed, since there would otherwise be no menu to unlock; the button is hidden entirely when you aren't the host, because only the host's list decides. An elevated player's own menu unlocks within a couple of seconds, once they read the updated lobby data.

Positions are owned by each player's own client, so **Go to** works for anyone but **Bring** needs the host, either you or a host running this mod.

Stat changes have to reach both the host and the player they're for, since the host decides health, defense and damage while each player's own game decides movement, digging and attack speed. The admin menu sends them itself, so for full effect both of those need the admin menu installed. As a client you can only edit stats when the host has it.

Damage in Stonewards is decided on the server, and the game clamps health and marks you dead in the same call — so god mode only really works where it can veto the hit before that happens. **As host, it blocks hits outright.** As a trusted client on a host running this mod, your game asks the host to hold god mode for you and the host blocks the hits on your behalf, which works the same way.

**On a host that doesn't run this mod, god mode is a fallback that heals you back up after each hit**, because nothing on your machine can veto damage the host has already applied. That survives ordinary chip damage, but a burst big enough to cover your whole health bar between heals — a volley of strong projectiles, say — still kills you, and nothing installed only on your side can prevent it.

## Development

Requires the .NET SDK, Stonewards, and an r2modman profile with BepInExPack 5.4.2305 installed.

- `dotnet build src/AdminMenu/AdminMenu.csproj` builds the plugin. `src/AdminMenu/AdminMenu.csproj` targets `netstandard2.1` and resolves its `<Reference>` items through `$(BepInExCorePath)` and `$(ManagedPath)`, which default to this machine's game install (`GamePath`) and r2modman profile (`ProfilePath`) — override them with `-p:GamePath=... -p:ProfilePath=...`, or with a gitignored `Local.props` importing your own `GamePath`/`ProfilePath` properties. After a build with the game found, the DLL is copied into the profile's `BepInEx/plugins/darkharasho-AdminMenu`.
- When `$(ManagedPath)/Assembly-CSharp.dll` doesn't exist (no game installed, e.g. in CI), or with `-p:UseRefs=true`, the build falls back to the reference-only assemblies checked into `lib/refs` instead of the real game/BepInEx DLLs.
- `lib/refs` is generated by `scripts/update-refs.sh`, which needs the `JetBrains.Refasmer.CliTool` (`dotnet tool install -g JetBrains.Refasmer.CliTool`). Rerun it after a game update that changes APIs the mod uses, or when adding a `<Reference>`.
- `dotnet test tests/AdminMenu.Tests` runs the unit tests; the test project never references game or BepInEx assemblies.
- `scripts/package.sh` builds Release and writes a Thunderstore zip to `dist/`. It fails if the versions in `thunderstore/manifest.json`, `Plugin.cs` and the `.csproj` differ, or if `CHANGELOG.md` has no entry for that version.

### Releases

Pushing a `v*` tag runs `.github/workflows/release.yml`. It checks the tag matches `thunderstore/manifest.json`, runs `scripts/package.sh`, creates a GitHub release with the zip and that version's changelog entry, and publishes the zip to Thunderstore with `scripts/thunderstore-publish.sh` (using the `THUNDERSTORE_TOKEN` repo secret; versions already on Thunderstore are skipped). Bump the version in the manifest, `Plugin.cs`, the `.csproj` and `CHANGELOG.md` first, then `git tag vX.Y.Z && git push origin vX.Y.Z`.
