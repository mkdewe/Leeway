namespace Leeway.Creature
{
    /// <summary>
    /// Published through MessagePipe when the player's local cell changes
    /// (spawn → <see cref="Cell"/> != null, despawn/death → null). It lets the camera and the HUD
    /// react without depending directly on the player controller (DIP).
    /// </summary>
    public readonly struct LocalCellChangedMessage
    {
        public readonly CellEntity Cell;
        public LocalCellChangedMessage(CellEntity cell) => Cell = cell;
    }
}
