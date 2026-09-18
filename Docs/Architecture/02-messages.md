<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/gen.mjs -->

# Przepływ komunikatów (MessagePipe)

Ta zależność **nie jest widoczna w kodzie ani w grafie assembly** — nadawca i
odbiorca nie znają się nawzajem, łączy ich wyłącznie typ komunikatu. Bez tego
diagramu jest niewidzialna.

```mermaid
flowchart LR
    CreatureBodyRebuiltMessage(["CreatureBodyRebuiltMessage"])
    CreatureEditorPartSelectionChangedMessage(["CreatureEditorPartSelectionChangedMessage"])
    CreatureEditorController["CreatureEditorController"] -->|publish| CreatureEditorPartSelectionChangedMessage
    CreatureEditorPartSelectionChangedMessage -->|subscribe| CreatureEditorHud["CreatureEditorHud"]
    CreatureEditorSelectionChangedMessage(["CreatureEditorSelectionChangedMessage"])
    CreatureEditorController["CreatureEditorController"] -->|publish| CreatureEditorSelectionChangedMessage
    CreatureEditorSelectionChangedMessage -->|subscribe| CreatureEditorHud["CreatureEditorHud"]
    CreatureEditorSessionChangedMessage(["CreatureEditorSessionChangedMessage"])
    CreatureEditorController["CreatureEditorController"] -->|publish| CreatureEditorSessionChangedMessage
    CreatureEditorSessionChangedMessage -->|subscribe| CreatureEditorHud["CreatureEditorHud"]
    GenomeCommitResultMessage(["GenomeCommitResultMessage"])
    CreatureBody["CreatureBody"] -->|publish| GenomeCommitResultMessage
    GenomeCommitResultMessage -->|subscribe| CreatureEditorHud["CreatureEditorHud"]
    LocalCellChangedMessage(["LocalCellChangedMessage"])
    PlayerCellController["PlayerCellController"] -->|publish| LocalCellChangedMessage
    LocalCellChangedMessage -->|subscribe| CellCamera["CellCamera"]
    LocalCellChangedMessage -->|subscribe| CellStageHUD["CellStageHUD"]
    LocalCreatureBodyChangedMessage(["LocalCreatureBodyChangedMessage"])
    PlaygroundCreatureController["PlaygroundCreatureController"] -->|publish| LocalCreatureBodyChangedMessage
    LocalCreatureBodyChangedMessage -->|subscribe| CreatureEditorController["CreatureEditorController"]
    LocalCreatureChangedMessage(["LocalCreatureChangedMessage"])
    PlayerCreatureController["PlayerCreatureController"] -->|publish| LocalCreatureChangedMessage
    LocalCreatureChangedMessage -->|subscribe| CreatureCamera["CreatureCamera"]
```

| Komunikat | Zarejestrowany | Publikuje | Subskrybuje |
|---|---|---|---|
| `CreatureBodyRebuiltMessage` | tak | — | — |
| `CreatureEditorPartSelectionChangedMessage` | tak | CreatureEditorController | CreatureEditorHud |
| `CreatureEditorSelectionChangedMessage` | tak | CreatureEditorController | CreatureEditorHud |
| `CreatureEditorSessionChangedMessage` | tak | CreatureEditorController | CreatureEditorHud |
| `GenomeCommitResultMessage` | tak | CreatureBody | CreatureEditorHud |
| `LocalCellChangedMessage` | tak | PlayerCellController | CellCamera, CellStageHUD |
| `LocalCreatureBodyChangedMessage` | tak | PlaygroundCreatureController | CreatureEditorController |
| `LocalCreatureChangedMessage` | tak | PlayerCreatureController | CreatureCamera |

> **Zarejestrowane, ale nigdzie nieużywane:** `CreatureBodyRebuiltMessage`.
