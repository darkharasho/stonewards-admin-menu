using UnityEngine;

namespace AdminMenu
{
    /// <summary>
    /// Keeps the equipped pickaxe, sword or staff at the speed this mod gave it. Each tool only sends its speed
    /// to its animator when it's shown or the synced speed changes, and the round trip through the host drops
    /// it when it lands before the tool counts as equipped, or after the animator was reset. So while the
    /// speed is ours (the super speed pickaxe, or a speed set in the stats editor), put it back on the local
    /// animator whenever it drifts, and resend it to the other players at most once a second.
    ///
    /// Bows aren't covered: their speed stat isn't in the stats editor.
    /// </summary>
    internal static class WeaponSpeed
    {
        private static readonly int AttackSpeedHash = Animator.StringToHash("ATTACK_SPEED");
        private const float SyncIntervalSeconds = 1f;

        private static float _nextSync;

        /// <summary>Called every frame from <see cref="Plugin.Update"/>.</summary>
        public static void Update()
        {
            var player = PlayerActions.LocalPlayer();
            var stats = player != null ? player.PlayerStats : null;
            if (stats == null || player.PlayerEquipment == null)
                return;
            var item = player.PlayerEquipment.CurrentEquippedItem;
            if (item == null || !TargetSpeed(item, stats, out var speed))
                return;

            var animator = item.ItemAnimator;
            if (animator == null || !animator.isActiveAndEnabled || Mathf.Approximately(animator.GetFloat(AttackSpeedHash), speed))
                return;
            animator.SetFloat(AttackSpeedHash, speed);
            if (Time.unscaledTime >= _nextSync && player.PlayerEquippedItemNetworkAnimator != null && item.ItemDataSO != null)
            {
                player.PlayerEquippedItemNetworkAnimator.CmdSetFloat(item.ItemDataSO.itemID, AttackSpeedHash, speed);
                _nextSync = Time.unscaledTime + SyncIntervalSeconds;
            }
        }

        /// <summary>The speed the item should run at, or false when this mod hasn't changed it.</summary>
        private static bool TargetSpeed(EquippedItem item, PlayerStats stats, out float speed)
        {
            speed = 0f;
            switch (item)
            {
                case DiggingTool _:
                    var cheat = Cheats.SuperSpeedPickaxeActive;
                    if (!cheat && !StatEditor.IsOverridden(stats.DiggingSpeed))
                        return false;
                    speed = stats.syncDiggingSpeed * (cheat ? Cheats.SpeedMultiplier : 1f);
                    return true;
                case SwordTool _:
                    if (!StatEditor.IsOverridden(stats.MeleeAttackSpeed))
                        return false;
                    speed = stats.syncMeleeAttackSpeed;
                    return true;
                case StaffTool _:
                    if (!StatEditor.IsOverridden(stats.MagicSpeed))
                        return false;
                    speed = stats.syncMagicSpeed;
                    return true;
                default:
                    return false;
            }
        }
    }
}
