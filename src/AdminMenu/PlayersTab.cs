using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Players tab: lists every connected player with revive, heal and kill actions. The list is rebuilt
    /// from scratch on every refresh, so buttons never go stale, and each rebuilt row is styled as it's
    /// created rather than by re-running <see cref="Theme.StyleControls"/> over the whole scroll view.
    /// </summary>
    internal static class PlayersTab
    {
        private static readonly Color DeadColor = new Color(0.85f, 0.35f, 0.35f);

        public static VisualElement Build(out Action refresh)
        {
            var scrollView = new ScrollView();

            void Refresh()
            {
                scrollView.Clear();

                if (!PlayerActions.ActionsAllowed)
                {
                    var note = new Label("Host-only actions is on and you are not the host.");
                    note.style.color = Theme.Muted;
                    note.style.marginBottom = 12f;
                    scrollView.Add(note);
                }

                foreach (var player in PlayerActions.Players())
                {
                    try
                    {
                        scrollView.Add(BuildRow(player, Refresh));
                    }
                    catch (Exception e)
                    {
                        // One player's row failing to build (e.g. stats not yet synced) shouldn't blank the
                        // rest of the list.
                        Plugin.Log.LogWarning($"Failed to build player row for {player?.playerName}: {e}");
                    }
                }
            }

            refresh = Refresh;
            return scrollView;
        }

        private static VisualElement BuildRow(FirstPersonController player, Action refresh)
        {
            var row = new VisualElement { name = "PlayerRow" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 10f;
            row.style.paddingBottom = 10f;
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = Theme.Line;

            var stats = player.PlayerStats;

            var name = new Label(player.playerName);
            name.style.color = Theme.Text;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.width = 200f;
            row.Add(name);

            var health = new Label();
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
            health.style.width = 100f;
            row.Add(health);

            var allowed = PlayerActions.ActionsAllowed;

            var revive = new Button(() =>
            {
                PlayerActions.Revive(player);
                refresh();
            }) { text = "Revive" };
            Theme.StyleButton(revive);
            revive.style.marginRight = 8f;
            revive.SetEnabled(allowed && stats.isDead);
            row.Add(revive);

            var heal = new Button(() =>
            {
                PlayerActions.Heal(player);
                refresh();
            }) { text = "Heal" };
            Theme.StyleButton(heal);
            heal.style.marginRight = 8f;
            heal.SetEnabled(allowed);
            row.Add(heal);

            var kill = new Button(() =>
            {
                PlayerActions.Kill(player);
                refresh();
            }) { text = "Kill" };
            Theme.StyleButton(kill);
            kill.SetEnabled(allowed && !stats.isDead);
            row.Add(kill);

            return row;
        }
    }
}
