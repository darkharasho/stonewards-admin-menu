using System;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Cheats tab: a column of rows, one per cheat — a toggle for the cheats that are simply on or off, a
    /// slider for the ones with a degree to them. All controls are built once here; <c>refresh</c> only ever
    /// updates their values, so nothing needs restyling on show — see <see cref="AdminMenuPanel.AddTab"/>
    /// for why restyling more than once would leak hover callbacks.
    /// </summary>
    internal static class CheatsTab
    {
        public static VisualElement Build(out Action refresh)
        {
            var root = new VisualElement();

            var superPickaxeToggle = BuildRow(root, "Super pickaxe", Plugin.SuperPickaxe);
            var superSpeedPickaxeToggle = BuildRow(root, "Super speed pickaxe", Plugin.SuperSpeedPickaxe);
            var quickOfferToggle = BuildRow(root, "Quick offer", Plugin.QuickOffer);
            var digSizeSlider = BuildSliderRow(root, "Pickaxe dig size", Plugin.PickaxeDigSize);
            var reachSlider = BuildSliderRow(root, "Pickaxe reach", Plugin.PickaxeReach);
            var infiniteStaminaToggle = BuildRow(root, "Infinite stamina", Plugin.InfiniteStamina);
            var infiniteAmmoToggle = BuildRow(root, "Infinite arrows & bombs", Plugin.InfiniteAmmo);
            var godModeToggle = BuildRow(root, "God mode", Plugin.GodMode);

            void Refresh()
            {
                superPickaxeToggle.value = Plugin.SuperPickaxe.Value;
                superSpeedPickaxeToggle.value = Plugin.SuperSpeedPickaxe.Value;
                quickOfferToggle.value = Plugin.QuickOffer.Value;
                digSizeSlider.value = Plugin.PickaxeDigSize.Value;
                reachSlider.value = Plugin.PickaxeReach.Value;
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
            var row = BuildRowShell(root, title, entry.Description.Description);

            var toggle = new Toggle { value = entry.Value };
            toggle.RegisterValueChangedCallback(evt => entry.Value = evt.newValue);
            row.Add(toggle);

            return toggle;
        }

        /// <summary>
        /// A cheat measured on a scale rather than switched on and off: the same row, with a slider over the
        /// entry's <see cref="AcceptableValueRange{T}"/> and a readout of the current value. The bounds come
        /// from the config entry so the slider can't offer a value the entry would clamp away.
        /// </summary>
        private static Slider BuildSliderRow(VisualElement root, string title, ConfigEntry<float> entry)
        {
            var range = (AcceptableValueRange<float>)entry.Description.AcceptableValues;
            var row = BuildRowShell(root, title, entry.Description.Description);

            var value = new Label();
            value.style.color = Theme.Muted;
            value.style.minWidth = 34f;
            value.style.unityTextAlign = TextAnchor.MiddleRight;
            value.style.marginRight = 6f;
            row.Add(value);

            var slider = new Slider(range.MinValue, range.MaxValue) { value = entry.Value };
            slider.style.width = 120f;
            // Formatted invariantly to match the .cfg, which BepInEx also writes invariantly.
            slider.RegisterValueChangedCallback(evt =>
            {
                entry.Value = evt.newValue;
                value.text = evt.newValue.ToString("0.00", CultureInfo.InvariantCulture);
            });
            value.text = entry.Value.ToString("0.00", CultureInfo.InvariantCulture);
            row.Add(slider);

            return slider;
        }

        /// <summary>The shared part of a cheat row: bold name and muted description, with room left for the
        /// control that measures it.</summary>
        private static VisualElement BuildRowShell(VisualElement root, string title, string description)
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

            var summary = new Label(description);
            summary.style.color = Theme.Muted;
            summary.style.whiteSpace = WhiteSpace.Normal;
            text.Add(summary);

            root.Add(row);

            return row;
        }
    }
}
