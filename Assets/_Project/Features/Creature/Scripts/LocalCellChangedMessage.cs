namespace Leeway.Creature
{
    /// <summary>
    /// Publikowany przez MessagePipe, gdy zmienia się lokalna komórka gracza
    /// (spawn → <see cref="Cell"/> != null, despawn/śmierć → null). Pozwala kamerze
    /// i HUD reagować bez bezpośredniej zależności od kontrolera gracza (DIP).
    /// </summary>
    public readonly struct LocalCellChangedMessage
    {
        public readonly CellEntity Cell;
        public LocalCellChangedMessage(CellEntity cell) => Cell = cell;
    }
}
