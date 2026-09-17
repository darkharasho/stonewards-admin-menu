using System;
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
            // Scrap (the "Wood" pieces dug out of barrels and crates) only exists as a pickup: the game turns it
            // into the item it produces when picked up, so a scrap item put straight into the inventory can't be
            // dropped. Its produced item is listed on its own. Several scrap assets also share an itemID, and the
            // game only ever resolves an ID to the first match, so later duplicates are skipped too.
            foreach (var item in database.items.Where(i => i != null && !string.IsNullOrEmpty(i.itemID) && !i.isScrapItem))
            {
                if (ById.ContainsKey(item.itemID))
                    continue;
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
        /// The chest's name with its rarity in front, e.g. "Rare Treasure chest", since chests of different rarities
        /// share a name. Uses the game's own rarity word, falling back to the enum when it isn't localized yet.
        /// </summary>
        public static string ChestLabel(ChestDataSO chest)
        {
            var name = chest.GetLocalizedName();
            if (string.IsNullOrEmpty(name))
                name = chest.Id;
            string rarity = null;
            try
            {
                rarity = ItemDataSO.GetRarityLocalizedString(chest.Rarity);
            }
            catch (Exception)
            {
                // Localization isn't loaded; use the enum name below.
            }
            if (string.IsNullOrEmpty(rarity) || rarity.StartsWith("RARITY_"))
                rarity = chest.Rarity.ToString().Substring(0, 1) + chest.Rarity.ToString().Substring(1).ToLowerInvariant();
            return name.IndexOf(rarity, StringComparison.OrdinalIgnoreCase) >= 0 ? name : $"{rarity} {name}";
        }

        /// <summary>The colour the game tags each rarity with (silver, green, cyan, magenta, orange), toned for the menu.</summary>
        public static Color RarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.UNCOMMON: return new Color(0.49f, 0.85f, 0.42f);
                case Rarity.RARE: return new Color(0.36f, 0.85f, 0.92f);
                case Rarity.EPIC: return new Color(0.87f, 0.45f, 0.93f);
                case Rarity.LEGENDARY: return new Color(1f, 0.65f, 0.25f);
                default: return new Color(0.8f, 0.8f, 0.82f);
            }
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
