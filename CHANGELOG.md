# Changelog

## 0.1.11

- Fix: **Quick offer** no longer loses most of what you offer. The forge credits an item only when its fly-in animation finishes, and it has just 20 of those visuals per resource; every item past the 20th of a burst found the pool empty, skipped its animation and was never credited — but had already left your inventory. Offering a 30,000-diamond stack in one press credited 20 diamonds and destroyed the rest, which is how two stacks bought two levels.
- A host running this mod now credits a multi-item offer directly, without the animation, so the whole stack counts. It clamps the count to what the offering slot really holds first, so nobody can claim more than they give.
- The levels a big offer pays for now arrive one after another: the game levels up at most once per offer and banks the rest, so when a draft closes the host opens the next one while the bank still covers a level.
- Against a host without this mod (or with 0.1.10 or older), quick offer now sends no more than that forge can animate — about 25 items a second instead of the vanilla 10. Slower than one press, but nothing is lost.
- Diamonds lost to the old behaviour can't be recovered; the mod never saw them credited.

## 0.1.10

- New cheat: **Pickaxe reach**, a slider from the pickaxe's normal 3 metres out to 20. The game measures a swing with one raycast that it fixes at 3, so this re-measures it at the distance you choose. That one raycast serves both halves of a swing, which means the reach applies to hitting props and destructibles as well as to digging terrain — there is no separate number to lengthen only one of them. Nothing downstream needed changing: the dig itself already traces 100 metres, and neither endpoint checks how far away you were.
- New cheat: **Pickaxe dig size**, a slider from the pickaxe's normal 0.35 up to 1.5 — the radius a bomb clears. The pickaxe and the bomb already dig through the same call and differ only in the size they ask for, so this sets the size your digs send. Only the crater grows: resources still come from the one grid point your swing landed on, because that is all the pickaxe's dig path ever spawns from. Large sizes are a lot of terrain to rebuild per swing, so expect a frame cost, especially alongside the super speed pickaxe.
- New cheat: **Quick offer**. Offering at the forge normally places one item per tick of the hold, so emptying a stack of stone means standing there for as many seconds as you have items. With this on, one press sends the whole stack. The count is read from the stack you're actually holding, so the forge is credited exactly what leaves your inventory — no more, no less.
- Fix: the Stats panel now shows another player's real stat values instead of their base ones. The game syncs only four stat values over the network, so every peer works the rest out from the upgrades it knows about and holds nothing but base numbers for somebody else's player. An admin could set another player's stats correctly — the value sent is a target, and each peer sizes its own modifier to hit it — but the numbers they were reading before they set anything were wrong, which made the panel close to useless for anyone but the host's own player. The menu now asks the player's own client for its values while their Stats panel is open, roughly once a second, and shows those.
- The host asks too: its copy is no better than a client's for somebody else's player, so opening another player's stats as host was showing base values as well.
- A player who doesn't have the admin menu has no client to ask, so their panel still shows the stats this peer holds for them and says so. The same note appears for a second or two before the first reply arrives.
- Only the connection that owns a player may report that player's stats, and the host relays a reply only to admins who asked for it.
- The gold "changed" highlight still reflects only the stats you changed from this peer, so a stat the host set won't show gold on a client.

## 0.1.9

- Admins granted with **Elevate** are now saved, so somebody you gave the menu to still has it the next time you host. Previously a grant was held in memory and dropped when you left the lobby, which meant re-elevating the same people every session; hand-editing Steam IDs into `TrustedAdmins` was the only way to make it stick.
- New **Admins tab** listing everyone who can use the menu in your games, in the three groups it comes from: granted from the menu, set in `TrustedAdmins`, and the mod author. Each granted admin has a **Revoke** button that works whether or not they're online — the reason the tab exists, since Elevate and Revoke on a player's row can only reach players who are in your lobby right now.
- `TrustedAdmins` is unchanged and stays the list to edit by hand, for granting admin to somebody who has never been in your lobby. Granted admins are saved separately in `SavedAdmins`, which the menu maintains.
- Elevate has no confirmation step, so a misclick now persists until you revoke it. The Admins tab is where you'd see and undo that.

## 0.1.8

- Fix: god mode no longer lets a client be killed by a heavy burst of damage, such as a volley of strong projectiles. The game clamps health and sets the death flag in the same server-side call, so a client noticing its health drop and healing back afterwards was always too late — the host had already marked it dead. Anything that covered the whole health bar before the next heal was lethal. A client now tells the host when its god mode is on and the host vetoes the damage itself, before the death check sees it, exactly as it already did for its own player. Trusted admins only, and the host stops protecting a player within a couple of seconds of their admin being revoked.
- Against a host that doesn't run this mod, nothing can veto damage on your behalf, so god mode there is still the heal-up fallback and a big enough burst still kills. The README and the config description now say so instead of implying it always holds.

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
