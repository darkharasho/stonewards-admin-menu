using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Resources tab: the meta currency on top, then the item spawner narrowed to resources (see
    /// <see cref="ItemEntry.IsResource"/>).
    /// </summary>
    internal static class ResourcesTab
    {
        public static VisualElement Build(out Action refresh)
        {
            var root = new VisualElement();

            var currencyRow = new VisualElement { name = "CurrencyRow" };
            currencyRow.style.flexDirection = FlexDirection.Row;
            currencyRow.style.alignItems = Align.Center;
            currencyRow.style.marginBottom = 16f;
            currencyRow.style.paddingBottom = 12f;
            currencyRow.style.borderBottomWidth = 2f;
            currencyRow.style.borderBottomColor = Theme.Line;
            root.Add(currencyRow);

            var title = new Label("Currency");
            title.style.color = Theme.Gold;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginRight = 16f;
            currencyRow.Add(title);

            var balance = new Label();
            balance.style.color = Theme.Muted;
            balance.style.flexGrow = 1f;
            currencyRow.Add(balance);

            var times = new Label("x");
            times.style.color = Theme.Muted;
            times.style.marginRight = 4f;
            currencyRow.Add(times);

            var amount = new IntegerField { value = 1 };
            amount.style.width = 80f;
            amount.style.marginRight = 12f;
            currencyRow.Add(amount);

            Button add = null;
            add = new Button(() =>
            {
                var manager = ResourceManager.Instance;
                if (manager == null || !PlayerActions.ActionsAllowed || amount.value < 1)
                    return;
                // Meta currency lives in the local save, so this only ever changes your own balance.
                manager.AddMetaCurrency(amount.value);
                Plugin.Log.LogInfo($"Added {amount.value} currency");
                UpdateBalance();
            }) { text = "Add" };
            Theme.StyleButton(add);
            currencyRow.Add(add);

            var chestRow = new VisualElement { name = "ChestRow" };
            chestRow.style.flexDirection = FlexDirection.Row;
            chestRow.style.flexWrap = Wrap.Wrap;
            chestRow.style.alignItems = Align.Center;
            chestRow.style.marginBottom = 16f;
            chestRow.style.paddingBottom = 12f;
            chestRow.style.borderBottomWidth = 2f;
            chestRow.style.borderBottomColor = Theme.Line;
            root.Add(chestRow);

            // The chest list comes from the item database, which only exists once the hub or a level has
            // loaded, so the buttons are rebuilt on every refresh rather than once here.
            void RebuildChests()
            {
                chestRow.Clear();
                var chestTitle = new Label("Chests");
                chestTitle.style.color = Theme.Gold;
                chestTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                chestTitle.style.marginRight = 16f;
                chestRow.Add(chestTitle);

                var chests = ItemCatalog.Chests();
                if (chests.Count == 0)
                {
                    var none = new Label("Not available here");
                    none.style.color = Theme.Muted;
                    chestRow.Add(none);
                    return;
                }

                foreach (var chest in chests)
                {
                    var data = chest;
                    var button = new Button(() => ItemCatalog.SpawnChest(data))
                    {
                        text = ItemCatalog.ChestLabel(data),
                        tooltip = "Spawns in front of you; carry it to the chest NPC to open it.",
                    };
                    Theme.StyleButton(button);
                    button.style.color = ItemCatalog.RarityColor(data.Rarity);
                    button.style.marginRight = 8f;
                    button.style.marginBottom = 4f;
                    button.SetEnabled(PlayerActions.ActionsAllowed && PlayerActions.LocalPlayer() != null);
                    chestRow.Add(button);
                }
            }

            var items = ItemsTab.Build(item => item.IsResource, out var refreshItems);
            items.style.flexGrow = 1f;
            root.Add(items);

            void UpdateBalance()
            {
                var manager = ResourceManager.Instance;
                balance.text = manager != null ? $"{manager.GetCurrentMetaCurrency()} in your save" : "Not available here";
                add.SetEnabled(manager != null && PlayerActions.ActionsAllowed);
            }

            refresh = () =>
            {
                UpdateBalance();
                RebuildChests();
                refreshItems();
            };
            return root;
        }
    }
}
