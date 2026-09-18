namespace Leeway.Creature
{
    /// <summary>
    /// Published through MessagePipe when the player's local creature changes
    /// (spawn → <see cref="Creature"/> != null, despawn/death → null). It lets the camera and the HUD
    /// react without depending directly on the player controller (DIP).
    /// </summary>
    public readonly struct LocalCreatureChangedMessage
    {
        public readonly CreatureEntity Creature;
        public LocalCreatureChangedMessage(CreatureEntity creature) => Creature = creature;
    }
}
