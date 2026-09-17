using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Cheats tab: a column of toggles, one per cheat. All controls are built once here; <c>refresh</c> only
    /// ever updates toggle values, so nothing needs restyling on show — see <see cref="AdminMenuPanel.AddTab"/>
    /// for why restyling more than once would leak hover callbacks.
    /// </summary>
    internal static class CheatsTab
    {
        public static VisualElement Build(out Action refresh)
        {
            var root = new VisualElement();

            var superPickaxeToggle = BuildRow(root, "Super pickaxe", Plugin.SuperPickaxe);
            var superSpeedPickaxeToggle = BuildRow(root, "Super speed pickaxe", Plugin.SuperSpeedPickaxe);
            var infiniteStaminaToggle = BuildRow(root, "Infinite stamina", Plugin.InfiniteStamina);
            var infiniteAmmoToggle = BuildRow(root, "Infinite arrows & bombs", Plugin.InfiniteAmmo);
            var godModeToggle = BuildRow(root, "God mode", Plugin.GodMode);

            void Refresh()
            {
                superPickaxeToggle.value = Plugin.SuperPickaxe.Value;
                superSpeedPickaxeToggle.value = Plugin.SuperSpeedPickaxe.Value;
                infiniteStaminaToggle.value = Plugin.InfiniteStamina.Value;
                infiniteAmmoToggle.value = Plugin.InfiniteAmmo.Value;
                godModeToggle.value = Plugin.GodMode.Value;
            }

            refresh = Refresh;
            return root;
        }

        /// <summary>
        /// One cheat row: bold name, a muted description pulled from the config entry, and a toggle bound
        /// to it.
        /// </summary>
        private static Toggle BuildRow(VisualElement root, string title, ConfigEntry<bool> entry)
        {
            var row = new VisualElement { name = "CheatRow" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 10f;
            row.style.paddingBottom = 10f;
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = Theme.Line;

            var text = new VisualElement();
            text.style.flexGrow = 1f;
            row.Add(text);

            var name = new Label(title);
            name.style.color = Theme.Text;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            text.Add(name);

            var description = new Label(entry.Description.Description);
            description.style.color = Theme.Muted;
            description.style.whiteSpace = WhiteSpace.Normal;
            text.Add(description);

            var toggle = new Toggle { value = entry.Value };
            toggle.RegisterValueChangedCallback(evt => entry.Value = evt.newValue);
            row.Add(toggle);

            root.Add(row);

            return toggle;
        }
    }
}
