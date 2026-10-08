namespace CursorHunter.Contracts
{
    /// <summary>
    /// Movement strategy selected by the species' composed behavior profile.
    /// The values are part of the immutable run snapshot shared with Combat.
    /// </summary>
    public enum MonsterMovementMode
    {
        BoundedWander = 0,
        Stationary = 1,
        Circular = 2
    }
}
