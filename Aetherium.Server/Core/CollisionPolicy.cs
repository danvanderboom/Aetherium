namespace Aetherium.Core
{
    /// <summary>
    /// How a world resolves two flyers/characters converging on the same cell.
    /// <see cref="Separated"/> (default) just blocks the move; <see cref="Collidable"/> additionally emits a
    /// <see cref="Aetherium.WorldEventType.Collision"/> event (a hook for damage/hazard reactions).
    /// </summary>
    public enum CollisionPolicy
    {
        Separated,
        Collidable
    }
}
