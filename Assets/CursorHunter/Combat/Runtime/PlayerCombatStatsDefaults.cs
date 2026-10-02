using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Authored defaults shared by every run. Trait upgrades are represented
    /// separately as additive bonuses and never modify these values.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCombatStatsDefaults : MonoBehaviour
    {
        [Header("Cursor Attack Defaults")]
        [SerializeField, Min(1)] private long attackPower = 1L;
        [SerializeField, Min(GameInformation.BaseAttackRadiusWorldUnits * 0.01f)]
        private float attackRadiusWorldUnits = GameInformation.BaseAttackRadiusWorldUnits;
        [SerializeField, Min(CursorCombatStatLimits.MinimumAttackCooldownSeconds)]
        private float attackCooldownSeconds = 0.8f;
        [SerializeField] private bool autoAttackEnabled = true;
        [SerializeField, Range(0f, 100f)] private float criticalChancePercent;
        [SerializeField, Min(1f)] private float criticalDamageMultiplier = 2f;
        [SerializeField, Min(0.01f)] private float bossDamageMultiplier = 1f;

        public CursorCombatStatDefaultsSnapshot Snapshot =>
            new CursorCombatStatDefaultsSnapshot(
                attackPower,
                attackRadiusWorldUnits,
                attackCooldownSeconds,
                criticalChancePercent,
                criticalDamageMultiplier,
                bossDamageMultiplier,
                autoAttackEnabled);
    }
}
