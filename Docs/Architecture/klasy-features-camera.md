<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/gen.mjs -->

# Features/Camera

```mermaid
classDiagram
    direction LR
    class CellCamera {
        <<MonoBehaviour>>
        [SF] float _followSpeed
        [SF] float _baseOrthoSize
        [SF] float _zoomPerSize
        [SF] float _zoomSpeed
    }
    class CreatureCamera {
        <<MonoBehaviour>>
    }
    class CellEntity {
        <<NetworkBehaviour>>
    }
    class LocalCellChangedMessage {
        <<message>>
    }
    class LocalCreatureChangedMessage {
        <<message>>
    }

    CellCamera ..> CellEntity
    CellCamera ..> LocalCellChangedMessage
    CreatureCamera ..> LocalCreatureChangedMessage
```

**Puste pudełka** to typy z innych obszarów, pokazane tylko dla kontekstu: `CellEntity`, `LocalCellChangedMessage`, `LocalCreatureChangedMessage`.

<details><summary>Pliki źródłowe (2)</summary>

- `Assets/_Project/Features/Camera/Scripts/CellCamera.cs`
- `Assets/_Project/Features/Camera/Scripts/CreatureCamera.cs`

</details>
