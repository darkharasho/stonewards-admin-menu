using System;
using HarmonyLib;

namespace AdminMenu.Patches
{
    /// <summary>
    /// Infinite arrows, ballista bolts and throwables for the local player. The inventory lives on the host,
    /// so everything here works by changing which commands this client sends, which needs no mod on the host.
    /// </summary>
    [HarmonyPatch]
    internal static class InfiniteAmmoPatches
    {
        private static readonly AccessTools.FieldRef<InventorySystem, FirstPersonController> InventoryOwner =
            AccessTools.FieldRefAccess<InventorySystem, FirstPersonController>("player");
        private static readonly AccessTools.FieldRef<InventorySystem, int> SelectedSlot =
            AccessTools.FieldRefAccess<InventorySystem, int>("slotSelected");
        private static readonly Func<SiegeWeapon, bool> HasAuthorityOnTurret =
            AccessTools.MethodDelegate<Func<SiegeWeapon, bool>>(AccessTools.PropertyGetter(typeof(SiegeWeapon), "HasAuthorityOnTurret"));

        // Set only while a local bow shot or ballista shot runs, so building and repairs, which remove items
        // through the same command on the host, still cost materials.
        private static bool _localShot;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BowTool), "ShootArrow")]
        private static void BeforeArrow(BowTool __instance)
        {
            _localShot = Cheats.InfiniteAmmoActive && __instance.Player != null && __instance.Player.isLocalPlayer;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(BowTool), "ShootArrow")]
        private static void AfterArrow() => _localShot = false;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Ballista), "OnFire")]
        private static void BeforeBolt(Ballista __instance)
        {
            _localShot = Cheats.InfiniteAmmoActive && HasAuthorityOnTurret(__instance);
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Ballista), "OnFire")]
        private static void AfterBolt() => _localShot = false;

        /// <summary>The bow and ballista only fire while you hold at least one, so skipping the removal is enough.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventorySystem), nameof(InventorySystem.CmdRemoveItemQuantity))]
        private static bool SkipAmmoRemoval() => !_localShot;

        /// <summary>
        /// The host removes a throwable and spawns it in one step, so it can't be skipped from a client. Give
        /// one back instead. Commands reach the host in order, so the refill goes first when the stack has
        /// room: a last bomb then goes 1 → 2 → 1 and stays equipped. A full stack gets it after the throw,
        /// so the extra doesn't spill into another slot.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventorySystem), nameof(InventorySystem.ThrowCurrentEquipment))]
        private static void RefillBeforeThrow(InventorySystem __instance, out string __state)
        {
            __state = null;
            if (!TryGetThrown(__instance, out var owner, out var entry))
                return;
            var data = ItemManager.Instance != null ? ItemManager.Instance.GetItemDataSoByID(entry.itemID) : null;
            if (data != null && entry.count >= data.maxStackSize)
            {
                __state = entry.itemID;
                return;
            }
            owner.CmdForcePickupItem(owner, 1, entry.itemID);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventorySystem), nameof(InventorySystem.ThrowCurrentEquipment))]
        private static void RefillAfterThrow(InventorySystem __instance, string __state)
        {
            var owner = InventoryOwner(__instance);
            if (__state != null && owner != null)
                owner.CmdForcePickupItem(owner, 1, __state);
        }

        private static bool TryGetThrown(InventorySystem inventory, out FirstPersonController owner, out InventorySystem.InventoryEntry entry)
        {
            entry = default;
            owner = InventoryOwner(inventory);
            if (!Cheats.InfiniteAmmoActive || owner == null || !owner.isLocalPlayer)
                return false;
            var slot = SelectedSlot(inventory);
            if (slot < 0 || slot >= inventory.InventoryEntries.Count)
                return false;
            entry = inventory.InventoryEntries[slot];
            return !entry.IsEmpty;
        }
    }
}
