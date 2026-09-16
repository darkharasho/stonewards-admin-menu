# Manual verification checklist — AdminMenu 0.1.0

No one was able to launch Stonewards during implementation, so **no runtime verification exists**.
Every task's in-game checks were collected here instead. Work through this before pushing a release tag —
Thunderstore never allows replacing a published version.


Collected from every task report. Nothing below could be verified by an agent: none of them can launch Stonewards.


## Task 1


Step 5 from the brief cannot be performed by the agent (no way to launch Stonewards). To confirm
the plugin loads in-game:

1. Launch Stonewards through r2modman and let it reach the main menu.
2. Run:
   ```
   grep AdminMenu ~/.config/r2modmanPlus-local/Stonewards/profiles/Default/BepInEx/LogOutput.log
   ```
3. Expected output: `[Info   :  AdminMenu] AdminMenu 0.1.0 loaded`


## Task 2


I cannot launch Stonewards from this environment. Please verify in-game:

1. Launch the game with the mod deployed (the build already copied `AdminMenu.dll` into
   `~/.config/r2modmanPlus-local/Stonewards/profiles/Default/BepInEx/plugins/darkharasho-AdminMenu`).
2. During normal gameplay, press F1 — `LogOutput.log` should show `Admin menu opened`.
3. Press F1 again — should show `Admin menu closed`.
4. Open the pause menu (or any other non-gameplay/inventory/dead state) and press F1 — no new log line
   should appear (the toggle should be gated off).
5. Die in-game and press F1 while dead — the toggle should still work (per
   `OpensWhileDeadSoYouCanReviveYourself`), producing `Admin menu opened` in the log.
6. If BepInEx.ConfigurationManager or the ModSettings mod is installed, confirm the four new settings show
   up under "Enable admin menu", "Open / close admin menu", "Close with Escape", and "Host-only actions"
   with the expected default values and ordering.


## Task 3


I cannot launch Stonewards, so Step 5 is untested. Please check, in a loaded level (not just the hub):

1. **Open.** Press F1 during gameplay. The overlay should cover the screen with a 70%-black scrim, the
   window centred at roughly 72% × 78% of the screen with a light 3px border, and "ADMIN MENU" top-left.
2. **Cursor and camera.** The mouse cursor must be visible and free, and the camera must stop following
   mouse movement. Move the mouse in circles and confirm the view does not turn.
3. **Player input off.** With the menu open, press W/A/S/D, left-click and right-click. The character
   should not move, attack, or interact.
4. **Close button.** Click "Close". The menu should disappear, the cursor re-lock, and camera plus
   movement return immediately. Repeat open/close several times.
5. **Escape.** Press F1, then Escape. The menu should close and the game's **pause menu must not open**.
   Then set `CloseOnEscape = false` in the config, reload, and confirm Escape no longer closes the menu.
6. **F1 closes too.** Press F1 while open — it should close via the same path as the Close button.
7. **Empty shell.** With no tabs yet, the rail is empty and the content area blank. That is expected for
   this task.
8. **Scene change while open.** Open the menu, then let a scene change happen (die and respawn, or
   finish a level). Confirm you are not left without input; the mod deliberately does *not* restore the
   remembered state in that case.
9. **Open from the inventory and while dead.** Both are states `MenuGate` allows. Closing should return
   you to the inventory / dead state respectively, not to plain gameplay.
10. **Re-attach after a scene load.** Load into a new level and press F1 again — the overlay must still
    appear (this exercises the `Attach()` staleness check) and must draw *over* the HUD, not under it.
11. **Console.** Check the BepInEx log for `No HUD document yet; the admin menu cannot open here.` —
    expected only if you press F1 mid-load, never during normal gameplay.


## Task 4


Since I cannot launch Stonewards, please run through the brief's Step 4 by hand:

1. **Single player:** Start a game, press F1. Confirm your own name and current/max health show correctly in the Players tab.
2. Click **Kill** on yourself — confirm the game's death screen appears and the row now shows "DEAD" with Revive enabled and Kill disabled the next time you open F1 (the Dead input state should still allow opening the menu).
3. With the death screen up, reopen F1 and click **Revive** — confirm you get back up at full health, and the row's Kill button re-enables while Revive disables again.
4. Click **Heal** while alive but damaged (e.g. after taking fall damage) — confirm your health bar/row goes to full without any other side effects.
5. **Host + second client:** Start as host with `RequireHost` off (default), have a second client join. Open F1 on the host and confirm both players are listed, sorted by name, and that Kill/Revive/Heal work correctly against the *other* player (not just yourself) from the host's menu.
6. **Host-only toggle:** In the BepInEx config, set `RequireHost` to `true`. On the non-host client, open F1 and confirm the "Host-only actions is on and you are not the host." note appears above the list and all three buttons for every row are disabled. On the host, confirm the note is absent and buttons still work.
7. Reopen the tab (switch away and back, or close/reopen F1) after any action and confirm the row states (health numbers, DEAD label, enabled/disabled buttons) are current — this exercises `onShown` calling `refresh`.


## Task 5


I cannot launch Stonewards, so please run these checks in-game:

1. Open the admin menu (F1) and select the **Items** tab. Confirm the search box, count field (default 1),
   the "Spawn into inventory" toggle (default on), and a scrollable list of every item in the database
   appear.
2. Type part of an item name (e.g. a substring you know is unique) into the search box. Confirm the list
   filters live on every keystroke, that an exact name match sorts above partial matches, and that pasting
   a raw item ID (e.g. copied from a log) also finds the item.
3. Confirm each row shows the item's icon (when the item has one), its display name, and its rarity.
4. With "Spawn into inventory" on, set the count to 5, click **Spawn** on an item. Confirm 5 of that item
   land in your inventory (or stack correctly if the item is stackable).
5. Toggle "Spawn into inventory" off, click **Spawn** on the same item. Confirm the item(s) appear on the
   ground just in front of you, and that they are pickable.
6. Try a non-stackable item with `MaxStack == 1`: set count to something higher than 1, spawn it, and
   confirm only 1 is produced (the clamp to `MaxStack` for non-stackable items).
7. Close and reopen the admin menu, or switch to the Players tab and back to Items. Confirm the list
   refreshes (via `onShown`) rather than showing stale data from a previous session/scene.
8. If possible, open the Items tab before a level/hub has finished loading (e.g. right after spawning in)
   to confirm the "Item database not available yet" warning path degrades gracefully (empty list, no
   exception) rather than throwing.

---


## Task 6


The following cannot be checked without launching Stonewards, and should be verified in game before release:

1. **Infinite stamina**: turn it on, sprint continuously, and confirm the stamina bar never drains (climbing and blocking too, since they drain the field directly rather than through `ConsumeStamina`).
2. **God mode**: turn it on, take damage from an enemy, and confirm health returns to full — as the host, damage should be blocked outright (`CanTakeDamage` postfix); as a plain client, health should visibly snap back up via the periodic heal in `Cheats.Update`.
3. **Super pickaxe on vs off**: mine the same block type with the cheat off, then on, and compare time-to-break — it should be noticeably faster at the default x5 multiplier (and scale further/less with the strength slider).
4. **Toggling super pickaxe off restores original speed**: turn the cheat off after having it on, and confirm dig speed returns to the *original* unmodified rate rather than staying elevated or compounding on a later re-toggle — this exercises `RemoveAllModifiersFromSource` actually clearing the modifier rather than stacking a new one on top of a stale one.
5. **Cheats tab UI**: open the menu, confirm the three toggles reflect the persisted `.cfg` values on open, that the strength slider only appears while Super pickaxe is on, that dragging it updates the live `x#.#` label and takes effect immediately (no dedicated "apply" step), and that switching tabs away and back doesn't duplicate rows or break hover highlighting on the toggles/slider (this is the failure mode Ruling B is guarding against).


## Task 7


- Install the built zip (`dist/darkharasho-AdminMenu-0.1.0.zip`) through r2modman/Thunderstore Mod Manager as a local package and confirm BepInEx loads `AdminMenu` without errors in `BepInEx/LogOutput.log`.
- In-game: confirm F1 opens/closes the themed tabbed panel (Players / Items / Cheats), and that closing with Escape and the pause menu both behave as expected per `CloseOnEscape`.
- Confirm the config file appears at `BepInEx/config/com.darkharasho.stonewards.adminmenu.cfg` with the sections/keys documented in the README, and that changing `ToggleKey` there (or via darkharasho-ModSettings if installed) actually rebinds the hotkey.
- Multiplayer: verify the god-mode behavioral difference described in the README — as host, damage should be blocked outright; as a plain client, health should visibly dip and heal back rather than never dropping.
- Visually confirm `thunderstore/icon.png` looks acceptable at Thunderstore's listing thumbnail size, since it was newly designed rather than reused.
- When ready to publish: bump nothing (still 0.1.0), review/adjust Thunderstore categories, then `git tag v0.1.0 && git push origin v0.1.0` to trigger `.github/workflows/release.yml`. The first Thunderstore upload will need `--categories` passed to `scripts/thunderstore-publish.sh` (or picked on the Thunderstore web UI first) since there's no existing listing to read categories from yet — the current release.yml invocation does not pass `--categories`, so the very first tag push's Thunderstore publish step may need a manual one-off run or a temporary edit to add `--categories <slugs>` before pushing the tag, per the brief's Step 7 note.
- Confirm `THUNDERSTORE_TOKEN` is genuinely set correctly on `darkharasho/stonewards-admin-menu` (I did not check this per Ruling A) before relying on the release workflow's Thunderstore publish step.

## Final review — added after the whole-branch review

- **God mode as host:** take a hit from an enemy. You should take no damage, but you WILL still see the hit
  FX and damage feedback — the game fires `RPCOnHit`/`TargetRpcOnDamageReceived` before it applies the health
  change, and the clamp sits in the health change. Cosmetic; confirm it is not mistaken for the cheat failing.
- **God mode does NOT veto scripted death** (`ServerRestoreDeath` writes the health SyncVar directly and is
  death-by-script rather than damage). If a scripted sequence kills you with god mode on, that is by design.
- **God mode as a plain client:** you will see a brief dip and restore rather than no damage at all — the
  client cannot veto damage the host already applied, so it heals back within 0.25s.
- **Disable check:** set `Enabled = false` in the config with Super pickaxe on, and confirm dig speed returns
  to normal without restarting the game (the modifier is removed rather than stranded).
- **Item spawner:** spawn a stackable item with a count above its real max stack and confirm the count is
  clamped to the stack limit; spawn a non-stackable with a count above 1 and confirm you get exactly one.

---

## Known cosmetic issues, deliberately left in 0.1.0

These were reviewed and judged not worth fixing; they are recorded so they aren't mistaken for new bugs.

- A tab's refresh runs once at panel construction and again on first open (harmless double refresh).
- A failed menu open logs a warning each time; mashing F1 during a scene load can repeat it.
- On a plain client, a revive/kill/heal button shows pre-round-trip state until the tab is reselected.
- Refreshing a list resets its scroll position to the top.
- Item rarity renders as the raw enum name rather than the game's localized string.
- A malformed item database with a duplicate item ID would show two rows sharing one icon.
