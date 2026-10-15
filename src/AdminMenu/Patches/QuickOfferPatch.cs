using HarmonyLib;
using Mirror;
using UnityEngine;

namespace AdminMenu.Patches
{
    /// <summary>
    /// Offers the whole stack to the forge in one press.
    ///
    /// Holding primary at the forge runs <c>PlayerInteraction.PrimaryActionLoop</c>, which sends
    /// <see cref="FirstPersonController.CmdTryPlaceItemInItemInteractable"/> with a count of 1 once per
    /// <c>primaryActionInterval</c> — one item per tick, so a full stack takes as many seconds as it has
    /// items. The command already takes an arbitrary count and is <c>requiresAuthority = false</c>, so the
    /// local player can raise the count it sends. Patched on the command rather than the loop because the host
    /// runs <c>UserCode</c> straight from this method instead of serialising, so one prefix covers both.
    ///
    /// What a raised count is worth depends on the host. The game's forge credits each item only when its
    /// fly-in animation finishes, from a pool of 20 visuals per resource, so a vanilla host silently drops
    /// every item past the 20th of a burst — they leave the inventory and are never credited. A host running
    /// this mod credits large offers directly (<see cref="ForgeBulkCreditPatch"/>), so the whole stack goes in
    /// one send; against any other host the count is held to what the pool can absorb (<see cref="OfferBudget"/>).
    /// </summary>
    [HarmonyPatch(typeof(FirstPersonController), nameof(FirstPersonController.CmdTryPlaceItemInItemInteractable))]
    internal static class QuickOfferPatch
    {
        /// <summary>The first release whose host credits a large offer in full.</summary>
        internal const string BulkCreditVersion = "0.1.11";

        // 15 of the pool's 20 leaves room for other players offering at the same time. The window is the
        // animation's 0.4 s plus margin for the commands bunching up on their way to the host.
        private static readonly OfferBudget Budget = new OfferBudget(capacity: 15, window: 0.6f);

        private static bool Prefix(FirstPersonController _Owner, int _SlotSelected, ref int _Count, NetworkIdentity _NetID)
        {
            // Only our own offers, and only to the forge: the same command feeds storage areas, the wall
            // repair NPC, the tool exchange and the catapult stack, none of which are asked to take a stack.
            if (!Cheats.QuickOfferActive || _Owner == null || !_Owner.isLocalPlayer || _NetID == null)
                return true;
            if (!_NetID.TryGetComponent<Forge>(out _))
                return true;

            var inventory = _Owner.InventorySystem;
            if (inventory == null || _SlotSelected < 0 || _SlotSelected >= inventory.InventoryEntries.Count)
                return true;

            // The count has to be what the slot really holds. The forge credits the count it is sent while the
            // inventory clamps the removal to the stack, so an inflated count would be free resources rather
            // than a faster offer. Raising it only upward leaves the vanilla single item in place once the stack
            // is gone, which matters because the loop keeps ticking for as long as the button is held and our
            // copy of the SyncList lags the removal by a round trip; the host's own empty-slot check is what
            // makes that repeat harmless.
            var held = inventory.InventoryEntries[_SlotSelected].count;
            var wanted = held > _Count ? held : _Count;

            if (NetworkServer.active || Access.HostVersionAtLeast(BulkCreditVersion))
            {
                _Count = wanted;
                return true;
            }

            // Skipping the send outright when the pool is spoken for, rather than sending vanilla's single
            // item, is what keeps the in-flight count honest; the loop retries next tick.
            var granted = Budget.Take(Time.unscaledTime, wanted);
            if (granted <= 0)
                return false;
            _Count = granted;
            return true;
        }
    }
}
