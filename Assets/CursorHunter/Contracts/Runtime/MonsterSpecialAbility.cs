namespace CursorHunter.Contracts
{
    /// <summary>
    /// Optional species ability copied into the immutable spawn snapshot.
    /// MonsterBehaviorType remains reserved for the legacy JSON field.
    /// </summary>
    public enum MonsterSpecialAbility
    {
        None = 0,
        Vanish = 1,
        Shield = 2,
        Teleport = 3,
        HealNearby = 4,
        ReviveNearby = 5
    }
}
