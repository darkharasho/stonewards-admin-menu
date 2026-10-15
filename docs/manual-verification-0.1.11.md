# Manual verification checklist — AdminMenu 0.1.11

The quick offer fix. `OfferBudget` and the version check are covered by unit tests
(`dotnet test tests/AdminMenu.Tests`); the forge patches need the game. Work through this before
pushing a release tag — Thunderstore never allows replacing a published version.

## Host offers a big stack

1. Host solo with **Quick offer** on. Spawn a large stack of diamonds (F1 → Items), e.g. 5,000.
2. Note the forge level, then press primary on the forge once.
3. The whole stack leaves the inventory and the forge levels up, opening a draft.
4. Pick an upgrade. The next draft opens straight away, and keeps doing so until the bank no longer
   covers a level. **This is the change**; in 0.1.10 one press credited 20 diamonds.
5. The level reached should match `GetTotalChargeUpToLevel` for that many diamonds — roughly, the
   charge text under the forge after the last draft is what's left over.

## Client offers to a modded host

1. Host with 0.1.11 and a second player with 0.1.11 who's been elevated, Quick offer on for them.
2. The client offers a large stack: same as above, everyone gets the drafts in turn.

## Client offers to a vanilla or older host

1. Join a host without the mod (or 0.1.10). Quick offer on, hold primary on the forge with a stack
   of 500 stone.
2. The stack drains at about 25 a second, and the forge's charge rises by every item: compare the
   charge before and after against the stone's value × 500.

## Nothing else changed

1. Quick offer off: offering still goes one item per tick, with the fly-in animation.
2. Storage areas, the wall repair NPC and the catapult stack still take one item at a time.
