using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Players tab: lists every connected player with revive, heal, kill and teleport actions. Rows are
    /// rebuilt only when the set of players changes; otherwise a scheduled tick updates health and button
    /// states in place, so the list stays live while the menu is open without rebuilding buttons out from
    /// under a click. Rebuilt rows are styled as they're created rather than by re-running
    /// <see cref="Theme.StyleControls"/> over the whole scroll view.
    /// </summary>
    internal static class PlayersTab
    {
        private static readonly Color DeadColor = new Color(0.85f, 0.35f, 0.35f);
        private const long TickMilliseconds = 250;

        public static VisualElement Build(out Action refresh)
        {
            var root = new VisualElement();

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.justifyContent = Justify.FlexEnd;
            toolbar.style.marginBottom = 12f;
            root.Add(toolbar);

            var bringAll = new Button(() =>
            {
                var self = PlayerActions.LocalPlayer();
                foreach (var player in PlayerActions.Players())
                    if (player != self)
                        PlayerActions.Bring(player);
            }) { text = "Bring everyone" };
            Theme.StyleButton(bringAll);
            toolbar.Add(bringAll);

            var campfire = new Button(PlayerActions.TeleportToCampfire) { text = "Go to campfire" };
            Theme.StyleButton(campfire);
            campfire.style.marginLeft = 8f;
            toolbar.Add(campfire);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1f;
            root.Add(scrollView);

            var shown = new List<FirstPersonController>();
            var updaters = new List<Action>();

            void Rebuild(List<FirstPersonController> players)
            {
                scrollView.Clear();
                shown.Clear();
                updaters.Clear();

                if (!PlayerActions.ActionsAllowed)
                {
                    var note = new Label("Only the host and trusted admins can use these.");
                    note.style.color = Theme.Muted;
                    note.style.marginBottom = 12f;
                    scrollView.Add(note);
                }

                foreach (var player in players)
                {
                    shown.Add(player);
                    try
                    {
                        scrollView.Add(BuildRow(player, out var update));
                        updaters.Add(update);
                    }
                    catch (Exception e)
                    {
                        // One player's row failing to build (e.g. stats not yet synced) shouldn't blank the
                        // rest of the list.
                        Plugin.Log.LogWarning($"Failed to build player row for {player?.playerName}: {e}");
                    }
                }
            }

            void Tick(bool forceRebuild = false)
            {
                var players = PlayerActions.Players();
                var changed = forceRebuild || players.Count != shown.Count;
                for (var i = 0; !changed && i < players.Count; i++)
                    changed = players[i] != shown[i];
                if (changed)
                    Rebuild(players);

                bringAll.SetEnabled(PlayerActions.CanBring && PlayerActions.ActionsAllowed && players.Count > 1);
                var hasCampfire = PlayerActions.Campfire() != null;
                campfire.SetEnabled(Plugin.Enabled.Value && hasCampfire && PlayerActions.LocalPlayer() != null);
                campfire.tooltip = hasCampfire ? null : "There's no campfire here.";
                foreach (var update in updaters)
                    update();
            }

            void Refresh()
            {
                // Rebuild on show so the host-only note reflects the current config.
                Tick(forceRebuild: true);
            }

            // The scheduler only runs while the element is attached to a panel, and the check below skips
            // work while the menu or this tab is hidden.
            root.schedule.Execute(() =>
            {
                if (AdminMenuController.IsOpen && root.resolvedStyle.display == DisplayStyle.Flex)
                    Tick();
            }).Every(TickMilliseconds);

            refresh = Refresh;
            return root;
        }

        private static VisualElement BuildRow(FirstPersonController player, out Action update)
        {
            var container = new VisualElement { name = "PlayerRow" };
            container.style.marginBottom = 10f;
            container.style.paddingBottom = 10f;
            container.style.borderBottomWidth = 1f;
            container.style.borderBottomColor = Theme.Line;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            container.Add(row);

            var isSelf = player == PlayerActions.LocalPlayer();

            var name = new Label(isSelf ? $"{player.playerName} (you)" : player.playerName);
            name.style.color = Theme.Text;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.width = 200f;
            name.style.flexShrink = 1f;
            row.Add(name);

            var health = new Label();
            health.style.width = 100f;
            health.style.flexGrow = 1f;
            row.Add(health);

            var revive = AddButton(row, "Revive", () => PlayerActions.Revive(player));
            var heal = AddButton(row, "Heal", () => PlayerActions.Heal(player));
            var kill = AddButton(row, "Kill", () => PlayerActions.Kill(player));
            var goTo = AddButton(row, "Go to", () => PlayerActions.TeleportTo(player));
            var bring = AddButton(row, "Bring", () => PlayerActions.Bring(player));
            // Host-only, and only for players running the mod: elevating someone with no menu to unlock
            // would do nothing. Hidden entirely off the host rather than shown disabled, since a client can
            // never grant and a permanently dead button just raises questions.
            var elevate = AddButton(row, "Elevate", () => Access.SetGranted(player, !Access.IsGranted(player)));

            var statsPanel = BuildStatsPanel(player, out var updateStats);
            statsPanel.style.display = DisplayStyle.None;
            container.Add(statsPanel);
            Button statsToggle = null;
            statsToggle = AddButton(row, "Stats", () =>
            {
                var show = statsPanel.style.display == DisplayStyle.None;
                statsPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                statsToggle.text = show ? "Hide stats" : "Stats";
                if (show)
                    updateStats();
            });
            statsToggle.style.marginRight = 0f;

            update = () =>
            {
                var stats = player != null ? player.PlayerStats : null;
                if (stats == null)
                    return;
                if (stats.isDead)
                {
                    health.text = "DEAD";
                    health.style.color = DeadColor;
                }
                else
                {
                    health.text = $"{Mathf.CeilToInt(stats.GetCurrentHealth())} / {Mathf.CeilToInt(PlayerActions.MaxHealthOf(stats))}";
                    health.style.color = Theme.Muted;
                }

                var allowed = PlayerActions.ActionsAllowed;
                revive.SetEnabled(allowed && stats.isDead);
                heal.SetEnabled(allowed);
                kill.SetEnabled(allowed && !stats.isDead);
                goTo.SetEnabled(Plugin.Enabled.Value && !isSelf);
                bring.SetEnabled(allowed && PlayerActions.CanBring && !isSelf);
                bring.tooltip = PlayerActions.CanBring ? "" : !allowed
                    ? "Only the host and trusted admins can move other players."
                    : "The host needs the admin menu installed for you to move other players.";
                var canGrant = Access.CanGrant && !isSelf;
                elevate.style.display = canGrant ? DisplayStyle.Flex : DisplayStyle.None;
                if (canGrant)
                {
                    var granted = Access.IsGranted(player);
                    var hasMod = Access.HasMod(player);
                    elevate.text = granted ? "Revoke" : "Elevate";
                    elevate.SetEnabled(Plugin.Enabled.Value && hasMod);
                    elevate.tooltip = !hasMod
                        ? "This player needs the admin menu installed before you can give them admin."
                        : granted
                            ? "Take back admin for this player. Lasts for this session only."
                            : "Give this player the admin menu for this session. Not saved to your config.";
                }

                statsToggle.SetEnabled(allowed);
                if (statsPanel.style.display == DisplayStyle.Flex)
                    updateStats();
            };
            update();

            return container;
        }

        /// <summary>
        /// One field per ring stat, showing the player's current value in the units the game shows (percent
        /// for percentage stats). Set makes the stat equal the field; Reset undoes it. A field refreshes from
        /// the live value until you edit it.
        /// </summary>
        private static VisualElement BuildStatsPanel(FirstPersonController player, out Action update)
        {
            var panel = new VisualElement();
            panel.style.marginTop = 10f;

            var note = new Label();
            note.style.color = Theme.Muted;
            note.style.marginBottom = 8f;
            note.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(note);

            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            panel.Add(grid);

            var cells = new List<Action<bool>>();
            var names = new List<string>();
            foreach (var statName in StatEditor.RingStats)
            {
                var name = statName;
                names.Add(name);
                CharacterStat Stat() => StatEditor.Get(player != null ? player.PlayerStats : null, name);

                var cell = new VisualElement();
                cell.style.flexDirection = FlexDirection.Row;
                cell.style.alignItems = Align.Center;
                cell.style.width = new Length(50f, LengthUnit.Percent);
                cell.style.paddingRight = 12f;
                cell.style.marginBottom = 6f;
                grid.Add(cell);

                var label = new Label(StatEditor.Label(name));
                label.style.color = Theme.Text;
                label.style.flexGrow = 1f;
                label.style.flexShrink = 1f;
                cell.Add(label);

                var edited = false;
                var field = new FloatField { value = StatEditor.DisplayValue(Stat()) };
                field.style.width = 80f;
                field.style.marginRight = 6f;
                field.RegisterValueChangedCallback(_ => edited = true);
                cell.Add(field);

                var unit = new Label();
                unit.style.color = Theme.Muted;
                unit.style.width = 16f;
                unit.style.marginRight = 6f;
                cell.Add(unit);

                var set = new Button(() =>
                {
                    StatEditor.Set(player, name, field.value);
                    edited = false;
                }) { text = "Set" };
                Theme.StyleButton(set);
                set.style.marginRight = 6f;
                cell.Add(set);

                var reset = new Button(() =>
                {
                    StatEditor.Set(player, name, float.NaN);
                    edited = false;
                }) { text = "Reset" };
                Theme.StyleButton(reset);
                cell.Add(reset);

                cells.Add(enabled =>
                {
                    var stat = Stat();
                    if (!edited && field.focusController?.focusedElement != field)
                        field.SetValueWithoutNotify((float)Math.Round(StatEditor.DisplayValue(stat), 2));
                    unit.text = stat == null ? ""
                        : stat._formatType == FormatType.Percentage ? "%"
                        : stat._formatType == FormatType.PercentageBySecond ? "%/s"
                        : stat._formatType == FormatType.Multiplier ? "x"
                        : stat._formatType == FormatType.Cooldown ? "s" : "";
                    label.style.color = StatEditor.IsOverridden(stat) ? Theme.Gold : Theme.Text;
                    field.SetEnabled(enabled);
                    set.SetEnabled(enabled);
                    reset.SetEnabled(enabled && StatEditor.IsOverridden(stat));
                });
            }

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.FlexEnd;
            footer.style.marginTop = 4f;
            panel.Add(footer);
            var resetAll = new Button(() =>
            {
                foreach (var name in names)
                    if (StatEditor.IsOverridden(StatEditor.Get(player.PlayerStats, name)))
                        StatEditor.Set(player, name, float.NaN);
            }) { text = "Reset all" };
            Theme.StyleButton(resetAll);
            footer.Add(resetAll);

            Theme.StyleControls(panel);

            update = () =>
            {
                var blocked = StatEditor.BlockedReason(player);
                var enabled = blocked == null && PlayerActions.ActionsAllowed;
                note.text = blocked ?? StatEditor.PartialReason(player)
                    ?? "Type a value and press Set. Changed stats are gold; Reset puts one back.";
                resetAll.SetEnabled(enabled);
                foreach (var cell in cells)
                    cell(enabled);
            };
            return panel;
        }

        private static Button AddButton(VisualElement row, string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            Theme.StyleButton(button);
            button.style.marginRight = 8f;
            row.Add(button);
            return button;
        }
    }
}
