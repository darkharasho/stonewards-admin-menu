# Changelog

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
