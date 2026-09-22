# Manual verification checklist — AdminMenu 0.1.10

Reading another player's real stats, and the new quick offer, pickaxe dig size and pickaxe reach cheats. `StatSnapshots` is covered by
unit tests (`dotnet test tests/AdminMenu.Tests`); the message path, the panel and the forge patch
need the game and — for the stat sections — a second player. Work through this before pushing a
release tag — Thunderstore never allows replacing a published version.

New in the config file: `Cheats.QuickOffer`, default `false`; `Cheats.PickaxeDigSize`, default `0.35`;
`Cheats.PickaxeReach`, default `3`.

## A client admin reads a real value

1. Host with a second player who also has the mod, and elevate them (F1 → Players → **Elevate**).
2. Have the host's player take a few level-up rings — anything that isn't max health, digging speed
   or melee/magic attack speed, since those four are synced by the game and were always right.
   Movement speed multiplier and crit chance are easy to read.
3. On the *elevated client*, open F1 → Players → **Stats** on the host's row.
4. The note reads "Waiting for … own values" for at most a second or two, then goes away, and the
   fields show the upgraded numbers — not the base ones. **This is the change**; in 0.1.9 they stayed
   at base values indefinitely.
5. Have the host take another ring of the same kind while the panel stays open. The field follows it
   within about a second.

## The host reads a client's real value

1. With the same pair, have the *client* take a few rings.
2. On the host, open Stats on the client's row. The upgraded numbers show, same as above.

## Setting still works, and agrees with what's shown

1. From the client admin, type a value into a stat on the host's row and press **Set**.
2. The field settles on the value you set, on both machines, within a second.
3. Press **Reset**. It returns to the host's real upgraded value, not to base.

## A player without the mod

1. Have a third player join without the mod installed (or have the second player remove it).
2. Open Stats on their row. The note says they don't have the admin menu and that these are the stats
   this peer holds for them; no "waiting" note is left hanging, and the panel doesn't sit blank.

## Nothing leaks to a player who isn't an admin

1. Revoke the second player's admin (F1 → Admins → **Revoke**, or their row's Revoke).
2. Their F1 does nothing, and the host's BepInEx log shows no stat queries being served for them.
   A query from an untrusted connection logs "Ignored a stat query from untrusted connection …".

## Between levels

1. Finish a level with a Stats panel having been open.
2. On the next level, open Stats on the same player. Values are re-read fresh — no numbers carried
   over from the previous level, and no stale value briefly showing before the first reply.

## Quick offer

1. F1 → **Cheats** → turn **Quick offer** on. Carry a stack of something the forge takes (stone,
   wooden planks, iron, gold, emerald or diamond) — more than a couple, so the difference is obvious.
2. Walk to the forge and press primary **once**, without holding.
3. The whole stack goes in at once: the slot empties, and the forge's board for that resource jumps
   by the full stack rather than climbing by one. Without the cheat the same press offers one item.
4. Check the numbers agree — the board should go up by exactly what left your inventory. A mismatch
   in either direction is the bug this is written to catch, since the host credits the count it is
   sent but only removes what the slot really holds.
5. Hold the button down with an empty hand or an empty slot. Nothing happens and the log stays quiet.
6. Turn the cheat off and offer again. One item per tick, as vanilla.

Worth watching: the host plays one place animation per item, so a large stack briefly shows a flurry
of them. That is host-side code a client-only mod can't batch — note it if it looks bad enough to
matter, but it isn't a failure.

## Quick offer doesn't touch anything else

1. With quick offer still on, use the same primary action on the other things that take items: a
   storage area / chest, the wall repair NPC, the tool exchange NPC, and a catapult projectile stack.
2. Each still takes one item per press, unchanged. The patch is scoped to the forge only.

## Quick offer against a plain host

1. Join a host who does **not** run this mod, with the cheat on.
2. Offer a stack. It still goes in at once — the command is the vanilla one and needs no authority.

## Pickaxe dig size

1. F1 → **Cheats**. The **Pickaxe dig size** row has a slider rather than a toggle, reading `0.35`.
2. Dig a block at `0.35` and note the hole. This is vanilla — the slider's floor is the cheat's off
   position, so nothing should look different from an unmodded game here.
3. Drag the slider to `1.50`. The readout follows it, and reopening the menu shows the value you left
   it on rather than snapping back.
4. Dig again. The hole is roughly a bomb's crater, not a pickaxe's — several times the radius.
5. Throw an actual bomb next to it. The two craters are about the same size; that's the number this
   is matched to.
6. Check the resources. You get **one block's worth** per swing at any slider position — the bigger
   hole does not pay more. Getting nothing, or getting a bomb's haul, both mean something is wrong.
7. Try a middle value such as `0.8`. The hole sits between the two, so the slider isn't behaving as
   an on/off switch.
8. Put the slider back to `0.35` and dig. Vanilla again.

Worth watching: a big dig is a lot of terrain for the game to rebuild in one frame, so a noticeable
hitch per swing at `1.50` is expected rather than a failure — especially with **Super speed pickaxe**
on as well. Note it if it's bad enough to be unplayable.

## Pickaxe dig size doesn't touch anything else

1. With the slider at `1.50`, throw a bomb. Its crater is unchanged — the patch only acts on digs
   that carry a player, and bombs carry none.
2. Watch another player dig while your slider is up. Their holes stay normal size; the cheat only
   raises the size your own client sends.
3. Load a fresh level. The caves generate as usual — level generation digs with no player either.

## Pickaxe dig size against a plain host

1. Join a host who does **not** run this mod, with the slider at `1.50`.
2. Dig. The crater is still bomb-sized, and the host's terrain shows it for everyone — the command is
   the vanilla one and needs no authority.

## Pickaxe reach

1. F1 → **Cheats**. The **Pickaxe reach** row is a slider reading `3.00`.
2. At `3.00`, stand back from a wall until your swings stop connecting, and note roughly where that is.
   This is vanilla — the slider's floor is the cheat's off position.
3. Drag the slider to `20.00`. The readout follows, and reopening the menu shows where you left it.
4. Swing from where you were just missing. The hit lands, and the hole appears on the far wall rather than
   in front of you. Back up further and keep swinging — it should keep connecting out to about 20 metres.
5. Past roughly 20 metres the swing misses again. That's the slider's ceiling, not a bug.
6. Try a middle value such as `8.00`. The reach sits between the two, so the slider isn't an on/off switch.
7. Swing at a **prop or destructible** (a rock, a crate) from well beyond arm's length. It takes damage and
   breaks. This is expected: the game measures the swing at a prop and the dig at terrain with the same
   raycast, so lengthening one lengthens both.
8. Put the slider back to `3.00`. Distant swings miss again, as vanilla.

## Pickaxe reach doesn't touch anything else

1. With the reach at `20.00`, check your *other* interactions at range: picking items up, opening a chest,
   talking to an NPC, offering at the forge. All still need you to be up close — the patch only touches the
   digging tool's own raycast.
2. Swing with a weapon that isn't a pickaxe (a sword, a bow). Its range is unchanged.
3. Watch another player dig while your slider is up. Their reach is normal; the cheat only lengthens the
   measurement your own client makes.

## Pickaxe reach against a plain host

1. Join a host who does **not** run this mod, with the slider at `20.00`.
2. Dig a far wall and break a distant prop. Both land, and everyone sees the result — neither the dig nor
   the prop hit checks how far away you were.

## Both pickaxe sliders together

1. Set dig size to `1.50` and reach to `20.00`, and dig a distant wall. You get a bomb-sized crater at
   range, and still one block's worth of resources per swing.
2. Add **Super speed pickaxe** and hold the swing. Watch for frame hitching — three cheats stacked is the
   worst case for it. Note how bad it gets; it isn't a failure, but it's the combination most likely to
   be unpleasant.
