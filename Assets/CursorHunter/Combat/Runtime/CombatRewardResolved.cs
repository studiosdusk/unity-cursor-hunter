using System;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>Committed kill rewards. Contains values only; never retains a dying target.</summary>
    public readonly struct CombatRewardResolved
    {
        public CombatRewardResolved(RunId runId, long sequence, Vector3 position,
            bool hasPosition, long garnet, string bonusId, long bonus)
            : this(runId, sequence, position, hasPosition, garnet, bonusId, bonus, string.Empty, 0L)
        {
        }

        public CombatRewardResolved(RunId runId, long sequence, Vector3 position,
            bool hasPosition, long garnet, string bonusId, long bonus,
            string relicAwardId, long experience)
        {
            RunId = runId; Sequence = sequence; WorldPosition = position;
            HasWorldPosition = hasPosition; Garnet = garnet;
            BonusCurrencyId = bonusId; BonusAmount = bonus;
            RelicAwardId = relicAwardId ?? string.Empty;
            Experience = Math.Max(0L, experience);
        }
        public RunId RunId { get; }
        public long Sequence { get; }
        public Vector3 WorldPosition { get; }
        public bool HasWorldPosition { get; }
        public long Garnet { get; }
        public string BonusCurrencyId { get; }
        public long BonusAmount { get; }
        public string RelicAwardId { get; }
        public long Experience { get; }
    }
}
