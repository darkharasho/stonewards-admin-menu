using HarmonyLib;
using Mirror;

namespace AdminMenu.Patches
{
    /// <summary>
    /// Host side of <see cref="QuickOfferPatch"/>: credits an offer of more than one item in full.
    ///
    /// The game's <see cref="Forge.TryPlaceItem"/> sends one <c>RpcPlayPlaceAnimation</c> per item, and the
    /// charge is only added when that animation finishes on the host's own client — from a pool of 20 visuals
    /// per resource. An item that finds the pool empty skips its animation and with it the credit, while the
    /// command still removes the whole count from the inventory. Vanilla never notices because it offers one
    /// item per tick; a stack offered in one go lost everything past the first 20, which is how two stacks of
    /// 30,000 diamonds bought two levels. Large offers are credited here directly instead, with no animation.
    ///
    /// This runs for any player's offer, not just an admin's: it fixes how a count is credited rather than
    /// granting anything, and the command is clamped to what the slot really holds first, so a client sending
    /// an inflated count gets no more than it gives.
    /// </summary>
    [HarmonyPatch]
    internal static class ForgeBulkCreditPatch
    {
        private static readonly AccessTools.FieldRef<NetworkHelper, bool> UpgradePhaseActive =
            AccessTools.FieldRefAccess<NetworkHelper, bool>("isRogueUpgradePhaseActive");

        // Set when a bulk offer banks more charge than one level needs. RogueLikeUpgradeManager.AddResources
        // levels up at most once per call and keeps the rest; vanilla spends that surplus only on the next
        // offer, so the levels a big stack paid for are drawn one at a time as each draft closes instead.
        private static bool _chainLevels;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FirstPersonController),
            "UserCode_CmdTryPlaceItemInItemInteractable__FirstPersonController__String__Int32__Int32__NetworkIdentity__Vector3__Quaternion")]
        private static bool ClampToSlot(FirstPersonController _Owner, string _ItemID, int _SlotSelected, ref int _Count, NetworkIdentity _NetID)
        {
            if (_Count <= 1 || _Owner == null || _NetID == null || !_NetID.TryGetComponent<Forge>(out _))
                return true;
            var inventory = _Owner.InventorySystem;
            if (inventory == null || _SlotSelected < 0 || _SlotSelected >= inventory.InventoryEntries.Count)
                return false;
            var entry = inventory.InventoryEntries[_SlotSelected];
            if (entry.itemID != _ItemID)
                return false;
            if (_Count > entry.count)
                _Count = entry.count;
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Forge), nameof(Forge.TryPlaceItem))]
        private static bool CreditDirectly(Forge __instance, FirstPersonController _Owner, string _ItemID, int _Count, ref bool __result)
        {
            if (!NetworkServer.active || _Count <= 1 || _Owner == null)
                return true;
            var upgrades = RogueLikeUpgradeManager.Instance;
            if (upgrades == null)
                return true;

            __result = __instance.IsPlaceable(_ItemID);
            if (!__result)
                return false;
            upgrades.AddResources(_ItemID, _Count);
            _chainLevels = true;
            _Owner.TargetRPCOnResourcesToForge(_ItemID, _Count);
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NetworkHelper), nameof(NetworkHelper.CheckRogueUpgradeCompletion))]
        private static void DrawNextLevel(NetworkHelper __instance)
        {
            if (!_chainLevels || !NetworkServer.active || UpgradePhaseActive(__instance))
                return;
            var upgrades = RogueLikeUpgradeManager.Instance;
            if (upgrades == null)
                return;
            // OnXPModifierChanged is the game's own "level up if the bank allows" step: it spends one level's
            // charge and opens the next draft, or does nothing once the bank runs short.
            var level = upgrades.GetCurrentLevel();
            upgrades.OnXPModifierChanged();
            if (upgrades.GetCurrentLevel() == level)
                _chainLevels = false;
        }
    }
}
