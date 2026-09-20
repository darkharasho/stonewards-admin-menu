# Manual verification checklist — AdminMenu 0.1.9

Saved admin grants and the Admins tab. `AdminRoster` and `TrustList` are covered by unit tests
(`dotnet test tests/AdminMenu.Tests`); everything below needs the game and, for most of it, a second
player. Work through this before pushing a release tag — Thunderstore never allows replacing a
published version.

The config file is `BepInEx/config/com.darkharasho.stonewards.adminmenu.cfg`; the new value is
`SavedAdmins` under `[General]`.

## Grants persist

1. Host a game with a second player who also has the mod. Open F1 → Players, press **Elevate** on their row.
2. The button reads **Revoke**. Within a couple of seconds F1 opens the menu on their machine.
3. Check `SavedAdmins` in the config now holds `<their id>=<their name>`.
4. Both quit to the main menu and back, host again, rejoin. **Their menu still works, with no Elevate needed** —
   this is the change; in 0.1.8 the grant was gone at this point.

## Revoke reaches an offline player

1. With the same grant in place, have the other player leave the game entirely.
2. Open F1 → **Admins**. They are listed under "Granted from the menu", by name and Steam ID, with a **Revoke** button.
3. Press **Revoke**. The row disappears and `SavedAdmins` is empty again.
4. Have them rejoin: F1 does nothing for them.

## Admins tab contents

1. As host with `TrustedAdmins` set to some ID, open F1 → Admins. Three sections appear: granted from the menu,
   from the config (the `TrustedAdmins` ID, no Revoke button), and the mod author (`76561197987892075`).
2. As a client, the tab shows the note that only the host's list decides, and no Revoke buttons.
3. Grant somebody from the Players tab with the Admins tab already open behind it — the tab picks the change up
   within about half a second without reopening the menu.

## Name refresh and edge cases

1. Have a granted player change their Steam name and rejoin. The Admins tab shows the new name; the ID is unchanged.
2. Hand-edit `SavedAdmins` to a bare ID with no name (`SavedAdmins = 76561190000000000`) and restart. The tab shows
   "(unknown player)" with that ID and still grants them admin.
3. Hand-edit it to rubbish (`SavedAdmins = not-an-id,111=Ferris`). The plugin loads, the junk is dropped, Ferris remains.
4. Elevate a player, then have them uninstall the mod and rejoin. **Revoke is still available on their row** — only
   granting is gated on them having the mod.
