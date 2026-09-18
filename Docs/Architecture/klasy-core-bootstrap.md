<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/gen.mjs -->

# Core/Bootstrap

```mermaid
classDiagram
    direction LR
    class CellPhaseLifetimeScope {
        <<LifetimeScope>>
    }
    class CreatureEditorLifetimeScope {
        <<LifetimeScope>>
    }
    class CreaturePhaseLifetimeScope {
        <<LifetimeScope>>
    }
    class LeewayMessageBrokers {
        <<static>>
        +TryInstall() bool$
    }
    class CreatureBodyRebuiltMessage {
        <<message>>
    }
    class CreatureEditorPartSelectionChangedMessage {
        <<message>>
    }
    class CreatureEditorSelectionChangedMessage {
        <<message>>
    }
    class CreatureEditorSessionChangedMessage {
        <<message>>
    }
    class GenomeCommitResultMessage {
        <<message>>
    }
    class LocalCellChangedMessage {
        <<message>>
    }
    class LocalCreatureBodyChangedMessage {
        <<message>>
    }
    class LocalCreatureChangedMessage {
        <<message>>
    }

    CellPhaseLifetimeScope ..> LeewayMessageBrokers
    CreatureEditorLifetimeScope ..> LeewayMessageBrokers
    CreaturePhaseLifetimeScope ..> LeewayMessageBrokers
    LeewayMessageBrokers ..> CreatureBodyRebuiltMessage
    LeewayMessageBrokers ..> CreatureEditorPartSelectionChangedMessage
    LeewayMessageBrokers ..> CreatureEditorSelectionChangedMessage
    LeewayMessageBrokers ..> CreatureEditorSessionChangedMessage
    LeewayMessageBrokers ..> GenomeCommitResultMessage
    LeewayMessageBrokers ..> LocalCellChangedMessage
    LeewayMessageBrokers ..> LocalCreatureBodyChangedMessage
    LeewayMessageBrokers ..> LocalCreatureChangedMessage
```

**Puste pudełka** to typy z innych obszarów, pokazane tylko dla kontekstu: `CreatureBodyRebuiltMessage`, `CreatureEditorPartSelectionChangedMessage`, `CreatureEditorSelectionChangedMessage`, `CreatureEditorSessionChangedMessage`, `GenomeCommitResultMessage`, `LocalCellChangedMessage`, `LocalCreatureBodyChangedMessage`, `LocalCreatureChangedMessage`.

<details><summary>Pliki źródłowe (4)</summary>

- `Assets/_Project/Core/Bootstrap/CellPhaseLifetimeScope.cs`
- `Assets/_Project/Core/Bootstrap/CreatureEditorLifetimeScope.cs`
- `Assets/_Project/Core/Bootstrap/CreaturePhaseLifetimeScope.cs`
- `Assets/_Project/Core/Bootstrap/LeewayMessageBrokers.cs`

</details>
