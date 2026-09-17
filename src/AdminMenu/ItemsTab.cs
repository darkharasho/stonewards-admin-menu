using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Items tab: a searchable, spawnable item catalog, each row with its own amount. The Resources tab reuses
    /// it with a filter. The result list is rebuilt from scratch on every
    /// keystroke, so each row is styled as it's created rather than by re-running <see cref="Theme.StyleControls"/>
    /// over the whole scroll view (see <see cref="PlayersTab"/> for the same pattern).
    /// </summary>
    internal static class ItemsTab
    {
        private const int MaxResults = 200;
        private const int MaxAmount = 999;

        public static VisualElement Build(out Action refresh) => Build(null, out refresh);

        /// <param name="include">Optional filter applied before the search; null shows every item.</param>
        public static VisualElement Build(Func<ItemEntry, bool> include, out Action refresh)
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

            var toInventory = new Toggle("Spawn into inventory") { value = true };
            toolbar.Add(toInventory);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1f;
            root.Add(scrollView);

            void RenderResults()
            {
                scrollView.Clear();

                if (!PlayerActions.ActionsAllowed)
                {
                    var note = new Label("Host-only actions is on and you are not the host.");
                    note.style.color = Theme.Muted;
                    note.style.marginBottom = 12f;
                    scrollView.Add(note);
                }

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
                    var row = BuildRow(item, toInventory);
                    // Rows are new elements, so styling them once here cannot stack hover callbacks.
                    Theme.StyleControls(row);
                    scrollView.Add(row);
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
                all.AddRange(include == null ? ItemCatalog.All() : ItemCatalog.All().Where(include));
                RenderResults();
            }

            refresh = Refresh;
            return root;
        }

        private static VisualElement BuildRow(ItemEntry item, Toggle toInventory)
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
            name.style.flexGrow = 1f;
            row.Add(name);

            var rarity = new Label(item.Rarity);
            rarity.style.color = Theme.Muted;
            rarity.style.width = 100f;
            row.Add(rarity);

            var times = new Label("x");
            times.style.color = Theme.Muted;
            times.style.marginRight = 4f;
            row.Add(times);

            var amount = new IntegerField { value = 1 };
            amount.style.width = 60f;
            amount.style.marginRight = 12f;
            row.Add(amount);

            var spawn = new Button(() =>
            {
                var count = Mathf.Clamp(amount.value, 1, MaxAmount);
                if (amount.value != count)
                    amount.value = count;
                ItemCatalog.Spawn(item, count, toInventory.value);
            }) { text = "Spawn" };
            Theme.StyleButton(spawn);
            spawn.SetEnabled(PlayerActions.ActionsAllowed);
            row.Add(spawn);

            return row;
        }
    }
}
