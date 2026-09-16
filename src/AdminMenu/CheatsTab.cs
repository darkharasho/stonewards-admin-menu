using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Cheats tab: a column of toggles, one per cheat, plus a strength slider for the super pickaxe. All
    /// controls are built once here; <c>refresh</c> only ever updates values on the elements built at that
    /// time (toggle values, the slider's display, live labels), so nothing needs restyling on show — see
    /// <see cref="AdminMenuPanel.AddTab"/> for why restyling more than once would leak hover callbacks.
    /// </summary>
    internal static class CheatsTab
    {
        public static VisualElement Build(out Action refresh)
        {
            var root = new VisualElement();

            var pickaxeMultiplierRow = default(VisualElement);
            var multiplierValueLabel = default(Label);
            var multiplierSlider = default(Slider);

            var superPickaxeToggle = BuildRow(root, "Super pickaxe", Plugin.SuperPickaxe,
                onExtra: row =>
                {
                    pickaxeMultiplierRow = new VisualElement { name = "SuperPickaxeMultiplierRow" };
                    pickaxeMultiplierRow.style.flexDirection = FlexDirection.Row;
                    pickaxeMultiplierRow.style.alignItems = Align.Center;
                    pickaxeMultiplierRow.style.marginLeft = 24f;
                    pickaxeMultiplierRow.style.marginBottom = 18f;

                    var label = new Label("Strength");
                    label.style.color = Theme.Muted;
                    label.style.width = 100f;
                    pickaxeMultiplierRow.Add(label);

                    multiplierSlider = new Slider(1f, 25f) { value = Plugin.SuperPickaxeMultiplier.Value };
                    multiplierSlider.style.flexGrow = 1f;
                    multiplierSlider.style.marginRight = 12f;
                    pickaxeMultiplierRow.Add(multiplierSlider);

                    multiplierValueLabel = new Label();
                    multiplierValueLabel.style.color = Theme.Text;
                    multiplierValueLabel.style.width = 50f;
                    pickaxeMultiplierRow.Add(multiplierValueLabel);

                    multiplierSlider.RegisterValueChangedCallback(evt =>
                    {
                        Plugin.SuperPickaxeMultiplier.Value = evt.newValue;
                        multiplierValueLabel.text = $"x{evt.newValue:0.0}";
                    });

                    root.Add(pickaxeMultiplierRow);
                });
            BuildRow(root, "Infinite stamina", Plugin.InfiniteStamina);
            BuildRow(root, "God mode", Plugin.GodMode);

            void Refresh()
            {
                superPickaxeToggle.value = Plugin.SuperPickaxe.Value;
                if (multiplierSlider != null)
                {
                    multiplierSlider.value = Plugin.SuperPickaxeMultiplier.Value;
                    multiplierValueLabel.text = $"x{Plugin.SuperPickaxeMultiplier.Value:0.0}";
                }
                if (pickaxeMultiplierRow != null)
                    pickaxeMultiplierRow.style.display = Plugin.SuperPickaxe.Value ? DisplayStyle.Flex : DisplayStyle.None;
            }

            refresh = Refresh;
            return root;
        }

        /// <summary>
        /// One cheat row: bold name, a muted description pulled from the config entry, and a toggle bound
        /// to it. <paramref name="onExtra"/> lets the super pickaxe row append its strength slider directly
        /// below itself, in build order, without a second pass over the tree.
        /// </summary>
        private static Toggle BuildRow(VisualElement root, string title, ConfigEntry<bool> entry, Action<VisualElement> onExtra = null)
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
            onExtra?.Invoke(row);

            return toggle;
        }
    }
}
