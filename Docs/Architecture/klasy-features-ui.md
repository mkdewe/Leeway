<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/gen.mjs -->

# Features/UI

```mermaid
classDiagram
    direction LR
    class CellStageHUD {
        <<MonoBehaviour>>
        [SF] Slider _hpSlider
        [SF] Slider _evolutionSlider
        [SF] TextMeshProUGUI _sizeText
        [SF] TextMeshProUGUI _statusText
    }
    class CellEntity {
        <<NetworkBehaviour>>
    }
    class LocalCellChangedMessage {
        <<message>>
    }

    CellStageHUD ..> CellEntity
    CellStageHUD ..> LocalCellChangedMessage
```

**Puste pudełka** to typy z innych obszarów, pokazane tylko dla kontekstu: `CellEntity`, `LocalCellChangedMessage`.

<details><summary>Pliki źródłowe (1)</summary>

- `Assets/_Project/Features/UI/Scripts/CellStageHUD.cs`

</details>
