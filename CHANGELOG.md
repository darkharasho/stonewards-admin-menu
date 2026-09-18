# Changelog

## 0.1.7

- The mod author is now always a trusted admin on every install, rather than only being the default value of `TrustedAdmins`. Holding the grant in the config meant it was lost by accident whenever a host added their own friends to the list, since that replaces the value. It is compiled in instead and can't be removed through the config; the README's new **Mod author access** section says so plainly, the config description repeats it, and the plugin logs it on startup. The mod is open source, so building it without the `OwnerSteamId` constant removes the grant.
- New: as host, **Elevate** on a player's row in the Players tab gives them the admin menu for the rest of the session, and **Revoke** takes it back. Grants are kept in memory only — never written to your config, and dropped when you leave the lobby. Offered only for players who have the mod installed, and only to the host, since it's the host's list that decides who is trusted. The elevated player's menu unlocks within a couple of seconds.
- `TrustedAdmins` now defaults to empty, since the author no longer needs to be listed in it. Existing entries keep working.
- Fix: the host rewrote its admin list into the Steam lobby on every check, because it compared the published string against one built from a set whose enumeration order isn't stable. The list is sorted now, so it's written only when it actually changes.

## 0.1.6

- Fix: teleporting to a player, or bringing one to you, no longer drops anyone through the map when the spot is tight. Both moves aimed at a fixed point 1.5 m from the anchor without checking anything was there; landing inside the level pushes a player's capsule out in whatever direction the overlap resolves, which is often straight down. They now sweep around the anchor for a spot a player actually fits in, and fall back to standing on the anchor rather than out of the world. The campfire teleport's fallback spot was picked the same blind way and is fixed too.

## 0.1.5

- Fix: reviving yourself from the menu while dead no longer leaves you unable to move. Closing the menu handed input back to the game's dead state because that's where the menu was opened from; it now picks the state that matches whether you're actually alive, so killing yourself from the menu behaves too.

## 0.1.4

- Fix: the item list no longer shows the game's scrap wood pieces (the many "Wood" entries). Spawned into the inventory they couldn't be dropped; the wood they turn into is still listed.

## 0.1.3

- Chest buttons on the Resources tab now show their rarity in the name (e.g. "Rare Treasure chest") and are coloured by rarity, so chests that share a name can be told apart.

## 0.1.2

- Fix: the super speed pickaxe, and digging, melee attack and magic speeds set in the stats editor, no longer drop back to normal after switching items; the speed is put back whenever it's lost.
- Fix: stats set in the stats editor now survive a class change instead of silently resetting. They still reset when a new level starts.

## 0.1.1

- Fix: reviving a player whose body someone is carrying now drops the body first, instead of leaving it stuck invisible in the carrier's hands.
- The Bring button's tooltip now says why it's unavailable, and the log notes whether the host runs the admin menu.

## 0.1.0

First release.

- Admin menu on F1 (Escape also closes it), themed to match the game's menus.
- Players tab: revive, heal or kill any player, Go to and Bring (host) teleports, Bring everyone, Go to campfire, and live health/death updates.
- Players tab: a Stats editor per player that shows and sets any stat level-up rings raise.
- Items tab: search and spawn any item, with an amount per item; large amounts are split into full stacks.
- Resources tab: add meta currency, spawn treasure chests, and spawn resources and scrap.
- Cheats tab: god mode, infinite stamina, infinite arrows & bombs, super pickaxe (25x dig strength) and super speed pickaxe (3x digging speed).
- Host-only: the menu and its cheats work for the host, plus the Steam IDs in `TrustedAdmins` (defaults to the mod author). When the host runs the mod, trusted clients' Bring and stat changes go through the host.
- Fix: as host, a player killed near you and revived out of your view no longer comes back into view as a sliding ragdoll.
