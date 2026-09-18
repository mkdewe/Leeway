<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/gen.mjs -->

# Features/Player

```mermaid
classDiagram
    direction LR
    class CellMoveData {
        <<ReplicateData>>
        +float Horizontal
        +float Vertical
    }
    class CellReconcileData {
        <<ReconcileData>>
        +Vector2 Position
        +Vector2 Velocity
    }
    class CreatureMoveData {
        <<ReplicateData>>
        +float Forward
        +float Strafe
        +float Yaw
    }
    class CreatureReconcileData {
        <<ReconcileData>>
        +Vector3 Position
        +Vector3 Velocity
        +float Yaw
    }
    class PlayerCellController {
        <<NetworkBehaviour>>
        [Replicate] Move(CellMoveData, ReplicateState, Channel) void
        [Reconcile] Reconciliation(CellReconcileData, Channel) void
    }
    class PlayerCreatureController {
        <<NetworkBehaviour>>
        [SF] float _lookSensitivity
        [ServerRpc] RequestAttack() void
        [Replicate] Move(CreatureMoveData, ReplicateState, Channel) void
        [Reconcile] Reconciliation(CreatureReconcileData, Channel) void
    }
    class CellEntity {
        <<NetworkBehaviour>>
    }
    class CreatureEntity {
        <<NetworkBehaviour>>
    }
    class LocalCellChangedMessage {
        <<message>>
    }
    class LocalCreatureChangedMessage {
        <<message>>
    }

    CellEntity <|-- PlayerCellController
    PlayerCellController --> CellMoveData
    PlayerCellController --> CellReconcileData
    PlayerCellController ..> LocalCellChangedMessage
    CreatureEntity <|-- PlayerCreatureController
    PlayerCreatureController --> CreatureMoveData
    PlayerCreatureController --> CreatureReconcileData
    PlayerCreatureController ..> LocalCreatureChangedMessage
```

**Puste pudełka** to typy z innych obszarów, pokazane tylko dla kontekstu: `CellEntity`, `CreatureEntity`, `LocalCellChangedMessage`, `LocalCreatureChangedMessage`.

<details><summary>Pliki źródłowe (2)</summary>

- `Assets/_Project/Features/Player/Scripts/PlayerCellController.cs`
- `Assets/_Project/Features/Player/Scripts/PlayerCreatureController.cs`

</details>
