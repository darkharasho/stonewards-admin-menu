using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>
    /// Reads the game's item database and spawns from it. The database lives on the networked
    /// <see cref="ItemManager"/>, so nothing here works before a level or the hub has loaded.
    /// </summary>
    internal static class ItemCatalog
    {
        private static readonly Dictionary<string, ItemDataSO> ById = new Dictionary<string, ItemDataSO>();

        public static List<ItemEntry> All()
        {
            ById.Clear();
            var manager = ItemManager.Instance;
            var database = manager == null ? null : Traverse.Create(manager).Field<ItemDatabaseSO>("itemDataBase").Value;
            if (database == null || database.items == null)
            {
                Plugin.Log.LogWarning("Item database not available yet; load into the hub or a level first");
                return new List<ItemEntry>();
            }

            var entries = new List<ItemEntry>();
            foreach (var item in database.items.Where(i => i != null && !string.IsNullOrEmpty(i.itemID)))
            {
                ById[item.itemID] = item;
                // A LocalizationSettings.StringDatabase lookup; resolve it once per item, not twice.
                var localizedName = item.GetLocalizedName();
                entries.Add(new ItemEntry
                {
                    Id = item.itemID,
                    Name = string.IsNullOrEmpty(localizedName) ? item.itemID : localizedName,
                    Rarity = item.Rarity.ToString(),
                    Stackable = item.isStackable,
                    MaxStack = Mathf.Max(1, item.maxStackSize),
                });
            }
            return entries;
        }

        /// <summary>The item's icon sprite, or null if the item hasn't been seen by <see cref="All"/> yet.</summary>
        public static Sprite IconFor(string id)
        {
            if (id == null || !ById.TryGetValue(id, out var item) || item == null)
                return null;
            return item.itemIcon;
        }

        public static void SpawnToInventory(ItemEntry item, int count)
        {
            var player = PlayerActions.LocalPlayer();
            if (item == null || player == null || !PlayerActions.ActionsAllowed)
                return;
            player.CmdForcePickupItem(player, count, item.Id);
            Plugin.Log.LogInfo($"Spawned {count}x {item.Id} into the inventory");
        }

        public static void SpawnToGround(ItemEntry item, int count)
        {
            var player = PlayerActions.LocalPlayer();
            if (item == null || player == null || ItemManager.Instance == null || !PlayerActions.ActionsAllowed)
                return;
            // A step in front of the player, at their feet, so the drop never lands inside a wall.
            var position = player.transform.position + player.transform.forward * 1.5f;
            ItemManager.Instance.CmdInstantiatePickableNewItem(item.Id, count, position);
            Plugin.Log.LogInfo($"Spawned {count}x {item.Id} on the ground");
        }
    }
}
