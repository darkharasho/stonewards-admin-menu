using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Items tab: a searchable, spawnable item catalog. The result list is rebuilt from scratch on every
    /// keystroke, so each row is styled as it's created rather than by re-running <see cref="Theme.StyleControls"/>
    /// over the whole scroll view (see <see cref="PlayersTab"/> for the same pattern).
    /// </summary>
    internal static class ItemsTab
    {
        private const int MaxResults = 200;

        public static VisualElement Build(out Action refresh)
        {
            var all = new List<ItemEntry>();

            var root = new VisualElement();

            var toolbar = new VisualElement { name = "ItemsToolbar" };
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.marginBottom = 12f;
            root.Add(toolbar);

            var searchContainer = new VisualElement();
            searchContainer.style.flexGrow = 1f;
            searchContainer.style.marginRight = 12f;
            toolbar.Add(searchContainer);

            var search = new TextField { value = string.Empty };
            search.style.flexGrow = 1f;
            searchContainer.Add(search);

            var placeholder = new Label("Search by name or item ID…");
            placeholder.style.color = Theme.Muted;
            placeholder.style.position = Position.Absolute;
            placeholder.style.left = 8f;
            placeholder.style.top = 0f;
            placeholder.style.bottom = 0f;
            placeholder.style.unityTextAlign = TextAnchor.MiddleLeft;
            placeholder.pickingMode = PickingMode.Ignore;
            searchContainer.Add(placeholder);

            var count = new IntegerField { value = 1 };
            count.style.width = 70f;
            count.style.marginRight = 12f;
            toolbar.Add(count);

            var toInventory = new Toggle("Spawn into inventory") { value = true };
            toolbar.Add(toInventory);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1f;
            root.Add(scrollView);

            int ClampedCount()
            {
                var value = count.value;
                if (value < 1) value = 1;
                if (value > 999) value = 999;
                if (count.value != value)
                    count.value = value;
                return value;
            }

            void RenderResults()
            {
                scrollView.Clear();
                var results = ItemSearch.Filter(all, search.value);
                var shown = 0;
                foreach (var item in results)
                {
                    if (shown >= MaxResults)
                    {
                        var truncated = new Label("… more results; refine the search");
                        truncated.style.color = Theme.Muted;
                        scrollView.Add(truncated);
                        break;
                    }
                    scrollView.Add(BuildRow(item, ClampedCount, toInventory));
                    shown++;
                }
            }

            search.RegisterValueChangedCallback(evt =>
            {
                placeholder.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
                RenderResults();
            });

            void Refresh()
            {
                all.Clear();
                all.AddRange(ItemCatalog.All());
                RenderResults();
            }

            refresh = Refresh;
            return root;
        }

        private static VisualElement BuildRow(ItemEntry item, Func<int> clampedCount, Toggle toInventory)
        {
            var row = new VisualElement { name = "ItemRow" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 10f;
            row.style.paddingBottom = 10f;
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = Theme.Line;

            var icon = new VisualElement { name = "ItemIcon" };
            icon.style.width = 32f;
            icon.style.height = 32f;
            icon.style.marginRight = 12f;
            icon.style.flexShrink = 0f;
            var sprite = ItemCatalog.IconFor(item.Id);
            if (sprite != null)
                icon.style.backgroundImage = new StyleBackground(sprite);
            row.Add(icon);

            var name = new Label(item.Name);
            name.style.color = Theme.Text;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.width = 220f;
            row.Add(name);

            var rarity = new Label(item.Rarity);
            rarity.style.color = Theme.Muted;
            rarity.style.width = 100f;
            row.Add(rarity);

            var spawn = new Button(() =>
            {
                var count = clampedCount();
                if (!item.Stackable)
                    count = Mathf.Min(count, item.MaxStack);
                if (toInventory.value)
                    ItemCatalog.SpawnToInventory(item, count);
                else
                    ItemCatalog.SpawnToGround(item, count);
            }) { text = "Spawn" };
            Theme.StyleButton(spawn);
            row.Add(spawn);

            return row;
        }
    }
}
