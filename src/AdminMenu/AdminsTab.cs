using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Admins tab: who can use the menu in the games you host, in the three groups it comes from -- granted
    /// from the Players tab, configured by hand, and the mod author.
    ///
    /// It exists so a grant can be taken back from somebody who isn't in the lobby. Elevate and Revoke live on
    /// a player's row, which only reaches players who are online right now; without this list, un-inviting
    /// anybody else would mean editing the config file, which is what the menu is here to replace.
    ///
    /// The whole list is rebuilt on show and whenever the stored value changes, rather than on a timer: it
    /// only changes when somebody uses this tab or the Players tab.
    /// </summary>
    internal static class AdminsTab
    {
        private const int TickMilliseconds = 500;

        public static VisualElement Build(out Action refresh)
        {
            var root = new VisualElement();

            var note = new Label();
            note.style.color = Theme.Muted;
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginBottom = 12f;
            root.Add(note);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1f;
            root.Add(scrollView);

            var shown = (string)null;
            var shownHost = (bool?)null;

            void Rebuild()
            {
                scrollView.Clear();

                note.text = Access.CanGrant
                    ? "Everyone who can use the admin menu in your games. Elevate players from the Players tab; "
                        + "revoke here, whether or not they're online."
                    : "Only the host's list decides who can use the admin menu, so there's nothing to change here. "
                        + "This is the list your own games would use.";

                var granted = Access.Roster.Entries.ToList();
                AddSection(scrollView, "Granted from the menu", granted.Count > 0
                    ? null
                    : "Nobody yet. Use Elevate on a player's row in the Players tab.");
                foreach (var entry in granted)
                {
                    var id = entry.Id;
                    AddRow(scrollView, entry.Name, id, Access.CanGrant ? "Revoke" : null, () => Access.Revoke(id));
                }

                var configured = TrustList.Parse(Plugin.TrustedAdmins.Value)
                    .Where(id => id.ToString() != Plugin.OwnerSteamId)
                    .OrderBy(id => id)
                    .ToList();
                AddSection(scrollView, "From the config", configured.Count > 0
                    ? "Edit the TrustedAdmins setting to change these. Use it for somebody who has never been in your lobby."
                    : "The TrustedAdmins setting is empty. Use it to add somebody who has never been in your lobby.");
                foreach (var id in configured)
                    AddRow(scrollView, NameFor(id), id, null, null);

                AddSection(scrollView, "Mod author", "Always an admin, on every install; see the README.");
                if (ulong.TryParse(Plugin.OwnerSteamId, out var owner))
                    AddRow(scrollView, "darkharasho", owner, null, null);
            }

            void Tick(bool forceRebuild = false)
            {
                // The roster's stored form is what the tab draws, so comparing it catches a grant made on the
                // Players tab, a revoke made here, and a config edited underneath us alike.
                var current = Access.Roster.Format() + "\n" + Plugin.TrustedAdmins.Value;
                var isHost = Access.CanGrant;
                if (!forceRebuild && current == shown && isHost == shownHost)
                    return;
                shown = current;
                shownHost = isHost;
                Rebuild();
            }

            root.schedule.Execute(() =>
            {
                if (AdminMenuController.IsOpen && root.resolvedStyle.display == DisplayStyle.Flex)
                    Tick();
            }).Every(TickMilliseconds);

            refresh = () => Tick(forceRebuild: true);
            return root;
        }

        /// <summary>A player's current name if they're here to ask, so a configured ID isn't only a number.</summary>
        private static string NameFor(ulong steamId)
        {
            var player = PlayerActions.Players().FirstOrDefault(p => p != null && p.playerID == steamId);
            return player != null ? player.playerName : string.Empty;
        }

        private static void AddSection(VisualElement root, string title, string description)
        {
            var heading = new Label(title);
            heading.style.color = Theme.Gold;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginTop = root.childCount > 0 ? 16f : 0f;
            root.Add(heading);

            if (description == null)
                return;
            var label = new Label(description);
            label.style.color = Theme.Muted;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 4f;
            root.Add(label);
        }

        /// <summary>
        /// One admin: the name we have for them, their Steam ID, and a Revoke button where revoking is
        /// possible. The ID is always shown -- it is what actually grants access, and two players can share a
        /// name.
        /// </summary>
        private static void AddRow(VisualElement root, string name, ulong steamId, string action, Action onAction)
        {
            var row = new VisualElement { name = "AdminRow" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 6f;
            row.style.paddingBottom = 6f;
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = Theme.Line;

            var label = new Label(string.IsNullOrEmpty(name) ? "(unknown player)" : name);
            label.style.color = string.IsNullOrEmpty(name) ? Theme.Muted : Theme.Text;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.width = 200f;
            label.style.flexShrink = 1f;
            row.Add(label);

            var id = new Label(steamId.ToString());
            id.style.color = Theme.Muted;
            id.style.flexGrow = 1f;
            row.Add(id);

            if (action != null && onAction != null)
            {
                var button = new Button(onAction) { text = action };
                Theme.StyleButton(button);
                row.Add(button);
            }

            root.Add(row);
        }
    }
}
