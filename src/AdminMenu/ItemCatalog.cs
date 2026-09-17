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

            var resourceIds = ResourceIds(database.items);
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
                    IsResource = resourceIds.Contains(item.itemID),
                });
            }
            return entries;
        }

        /// <summary>
        /// The game has no "resource" flag, so this collects what it treats as one: whatever a dig profile can
        /// drop (<c>DigMatterProfileSO.resourceClusters</c>, private, and only loaded in a level), scrap items and
        /// what they refine into, and the rewards the exchange hands out. The item-side flags work in the hub
        /// too, where no dig profile is loaded.
        /// </summary>
        private static HashSet<string> ResourceIds(List<ItemDataSO> items)
        {
            var ids = new HashSet<string>();
            void Add(ItemDataSO item)
            {
                if (item != null && !string.IsNullOrEmpty(item.itemID))
                    ids.Add(item.itemID);
            }
            void AddRewards(ExchangeResourceList list)
            {
                if (list?.items == null) return;
                foreach (var reward in list.items)
                    Add(reward.item);
            }

            foreach (var item in items)
            {
                if (item == null) continue;
                if (item.isScrapItem)
                {
                    Add(item);
                    Add(item.itemProduced);
                }
                if (item.canBeExchanged)
                {
                    AddRewards(item.level1ExchangeRewards);
                    AddRewards(item.level2ExchangeRewards);
                }
            }

            foreach (var profile in Resources.FindObjectsOfTypeAll<DigMatterProfileSO>())
            {
                if (Traverse.Create(profile).Field("resourceClusters").GetValue() is System.Collections.IEnumerable clusters)
                    foreach (var cluster in clusters)
                        Add(Traverse.Create(cluster).Field<ItemDataSO>("resourceCluster").Value);
            }
            return ids;
        }

        /// <summary>The item's icon sprite, or null if the item hasn't been seen by <see cref="All"/> yet.</summary>
        public static Sprite IconFor(string id)
        {
            if (id == null || !ById.TryGetValue(id, out var item) || item == null)
                return null;
            return item.itemIcon;
        }

        /// <summary>
        /// Spawns <paramref name="count"/> of an item, split into stacks the game can hold: stackable items go
        /// out in <see cref="ItemEntry.MaxStack"/>-sized chunks and everything else one at a time.
        /// </summary>
        public static void Spawn(ItemEntry item, int count, bool toInventory)
        {
            if (item == null || count < 1)
                return;
            var stack = item.Stackable ? item.MaxStack : 1;
            for (var left = count; left > 0; left -= stack)
            {
                var chunk = Mathf.Min(left, stack);
                if (toInventory)
                    SpawnToInventory(item, chunk);
                else
                    SpawnToGround(item, chunk);
            }
            Plugin.Log.LogInfo($"Spawned {count}x {item.Id} {(toInventory ? "into the inventory" : "on the ground")}");
        }

        /// <summary>The chests the item database knows, commonest first. Empty until the hub or a level has loaded.</summary>
        public static List<ChestDataSO> Chests()
        {
            var manager = ItemManager.Instance;
            var database = manager == null ? null : Traverse.Create(manager).Field<ItemDatabaseSO>("itemDataBase").Value;
            if (database == null || database.carriables == null)
                return new List<ChestDataSO>();
            return database.carriables.OfType<ChestDataSO>()
                .Where(c => c != null && !string.IsNullOrEmpty(c.Id))
                .OrderBy(c => c.Rarity)
                .ToList();
        }

        /// <summary>
        /// Spawns a chest in front of you, the same way a player drops one. It can be carried to the chest NPC
        /// and opened like any other. The Command doesn't need authority, so this works from a client.
        /// </summary>
        public static void SpawnChest(ChestDataSO chest)
        {
            var player = PlayerActions.LocalPlayer();
            if (chest == null || player == null || ItemManager.Instance == null || !PlayerActions.ActionsAllowed)
                return;
            var position = player.transform.position + player.transform.forward * 2f + Vector3.up;
            ItemManager.Instance.CmdInstantiateCarriableObject(chest.Id, position);
            Plugin.Log.LogInfo($"Spawned chest {chest.Id}");
        }

        private static void SpawnToInventory(ItemEntry item, int count)
        {
            var player = PlayerActions.LocalPlayer();
            if (item == null || player == null || !PlayerActions.ActionsAllowed)
                return;
            player.CmdForcePickupItem(player, count, item.Id);
        }

        private static void SpawnToGround(ItemEntry item, int count)
        {
            var player = PlayerActions.LocalPlayer();
            if (item == null || player == null || ItemManager.Instance == null || !PlayerActions.ActionsAllowed)
                return;
            // A step in front of the player, at their feet, so the drop never lands inside a wall.
            var position = player.transform.position + player.transform.forward * 1.5f;
            ItemManager.Instance.CmdInstantiatePickableNewItem(item.Id, count, position);
        }
    }
}
