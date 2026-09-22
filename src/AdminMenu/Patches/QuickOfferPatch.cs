using HarmonyLib;
using Mirror;

namespace AdminMenu.Patches
{
    /// <summary>
    /// Offers the whole stack to the forge in one press.
    ///
    /// Holding primary at the forge runs <c>PlayerInteraction.PrimaryActionLoop</c>, which sends
    /// <see cref="FirstPersonController.CmdTryPlaceItemInItemInteractable"/> with a count of 1 once per
    /// <c>primaryActionInterval</c> — one item per tick, so a full stack takes as many seconds as it has
    /// items. The command already takes an arbitrary count and is <c>requiresAuthority = false</c>, so
    /// raising the count the local player sends is enough and the host needs no mod, the same trick as
    /// <see cref="DigSpeedPatch"/>. Patched on the command rather than the loop because the host runs
    /// <c>UserCode</c> straight from this method instead of serialising, so one prefix covers both.
    /// </summary>
    [HarmonyPatch(typeof(FirstPersonController), nameof(FirstPersonController.CmdTryPlaceItemInItemInteractable))]
    internal static class QuickOfferPatch
    {
        private static void Prefix(FirstPersonController _Owner, int _SlotSelected, ref int _Count, NetworkIdentity _NetID)
        {
            // Only our own offers, and only to the forge: the same command feeds storage areas, the wall
            // repair NPC, the tool exchange and the catapult stack, none of which are asked to take a stack.
            if (!Cheats.QuickOfferActive || _Owner == null || !_Owner.isLocalPlayer || _NetID == null)
                return;
            if (!_NetID.TryGetComponent<Forge>(out _))
                return;

            var inventory = _Owner.InventorySystem;
            if (inventory == null || _SlotSelected < 0 || _SlotSelected >= inventory.InventoryEntries.Count)
                return;

            // The count has to be what the slot really holds. Forge.TryPlaceItem credits the count it is sent
            // while InventorySystem.TryRemove clamps the removal to the stack, so an inflated count would be
            // free resources rather than a faster offer. Raising it only upward leaves the vanilla single item
            // in place once the stack is gone, which matters because the loop keeps ticking for as long as the
            // button is held and our copy of the SyncList lags the removal by a round trip; the host's own
            // empty-slot check is what makes that repeat harmless.
            var held = inventory.InventoryEntries[_SlotSelected].count;
            if (held > _Count)
                _Count = held;
        }
    }
}
