<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/gen.mjs -->

# Features/CreatureEditor/Messages

```mermaid
classDiagram
    direction LR
    class CreatureBodyRebuiltMessage {
        <<message>>
        +CreatureBody Body
        +CreatureStats Stats
    }
    class CreatureEditorPartSelectionChangedMessage {
        <<message>>
        +int PartIndex
        +bool Mirrored
        +bool CanMirror
    }
    class CreatureEditorSelectionChangedMessage {
        <<message>>
        +int VertebraIndex
    }
    class CreatureEditorSessionChangedMessage {
        <<message>>
        +CreatureEditorSession Session
    }
    class GenomeCommitResultMessage {
        <<message>>
        +bool Accepted
        +GenomeError Error
    }
    class LocalCreatureBodyChangedMessage {
        <<message>>
        +CreatureBody Body
    }
    class CreatureBody {
        <<NetworkBehaviour>>
    }
    class CreatureEditorSession {
        <<MonoBehaviour>>
    }
    class CreatureStats {
        <<domain>>
    }
    class GenomeError {
        <<enum>>
    }

    CreatureBodyRebuiltMessage --> CreatureBody
    CreatureBodyRebuiltMessage --> CreatureStats
    CreatureEditorSessionChangedMessage --> CreatureEditorSession
    GenomeCommitResultMessage --> GenomeError
    LocalCreatureBodyChangedMessage --> CreatureBody
```

**Puste pudełka** to typy z innych obszarów, pokazane tylko dla kontekstu: `CreatureBody`, `CreatureEditorSession`, `CreatureStats`, `GenomeError`.

<details><summary>Pliki źródłowe (1)</summary>

- `Assets/_Project/Features/CreatureEditor/Scripts/Messages/CreatureEditorMessages.cs`

</details>
