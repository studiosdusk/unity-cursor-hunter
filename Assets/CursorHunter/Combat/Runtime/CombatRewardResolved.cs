using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>Committed kill rewards. Contains values only; never retains a dying target.</summary>
    public readonly struct CombatRewardResolved
    {
        public CombatRewardResolved(RunId runId, long sequence, Vector3 position,
            bool hasPosition, long garnet, string bonusId, long bonus)
        {
            RunId = runId; Sequence = sequence; WorldPosition = position;
            HasWorldPosition = hasPosition; Garnet = garnet;
            BonusCurrencyId = bonusId; BonusAmount = bonus;
        }
        public RunId RunId { get; }
        public long Sequence { get; }
        public Vector3 WorldPosition { get; }
        public bool HasWorldPosition { get; }
        public long Garnet { get; }
        public string BonusCurrencyId { get; }
        public long BonusAmount { get; }
    }
}
